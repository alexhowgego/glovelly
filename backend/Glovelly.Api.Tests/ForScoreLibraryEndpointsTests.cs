using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class ForScoreLibraryEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public ForScoreLibraryEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_CreatesActiveSnapshotAndCharts()
    {
        var content = BuildUploadContent(ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Example.pdf|title"] = "Example",
            ["Example.pdf|keywords"] = "Bella",
        }));

        var response = await _client.PostAsync("/forscore-library/imports", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var importResult = await response.Content.ReadFromJsonAsync<ImportResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(importResult);
        Assert.True(importResult.Snapshot.IsActive);
        Assert.Equal(1, importResult.Snapshot.ChartCount);
        Assert.Equal(0, importResult.Impact.AffectedSetListCount);

        var chartsResponse = await _client.GetAsync("/forscore-library/active/charts", TestContext.Current.CancellationToken);
        chartsResponse.EnsureSuccessStatusCode();
        var charts = await chartsResponse.Content.ReadFromJsonAsync<ChartsResponse>(TestContext.Current.CancellationToken);
        var chart = Assert.Single(charts!.Charts);
        Assert.Equal("Example.pdf", chart.FilePath);
        Assert.Equal("Example", chart.Title);
        Assert.Equal("EXAMPLE", chart.NormalizedTitle);
    }

    [Fact]
    public async Task Upload_SupersedesPreviousActiveSnapshot()
    {
        await _client.PostAsync("/forscore-library/imports", BuildUploadContent(ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["First.pdf|title"] = "First",
        }), "first.4sb"), TestContext.Current.CancellationToken);

        var response = await _client.PostAsync("/forscore-library/imports", BuildUploadContent(ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Second.pdf|title"] = "Second",
        }), "second.4sb"), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.ForScoreLibrarySnapshots.CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.ForScoreLibrarySnapshots.Where(snapshot => snapshot.IsActive).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("second.4sb", (await db.ForScoreLibrarySnapshots.SingleAsync(snapshot => snapshot.IsActive, TestContext.Current.CancellationToken)).OriginalFileName);
    }

    [Fact]
    public async Task Upload_InvalidFileDoesNotReplaceActiveSnapshot()
    {
        await _client.PostAsync("/forscore-library/imports", BuildUploadContent(ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Valid.pdf|title"] = "Valid",
        }), "valid.4sb"), TestContext.Current.CancellationToken);

        var invalidResponse = await _client.PostAsync("/forscore-library/imports", BuildUploadContent("invalid"u8.ToArray(), "invalid.4sb"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("valid.4sb", (await db.ForScoreLibrarySnapshots.SingleAsync(snapshot => snapshot.IsActive, TestContext.Current.CancellationToken)).OriginalFileName);
    }

    [Fact]
    public async Task ActiveSnapshot_IsScopedToAuthenticatedUser()
    {
        await SeedAlternateUserAsync();
        await _client.PostAsync("/forscore-library/imports", BuildUploadContent(ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Owner.pdf|title"] = "Owner",
        }), "owner.4sb"), TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/forscore-library/active");
        request.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_NewSnapshotRelinksMappedUpcomingDraftAndConfirmedSetLists()
    {
        await _client.PostAsync("/forscore-library/imports", BuildUploadContent(ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Shared.pdf|title"] = "Shared",
            ["Missing.pdf|title"] = "Missing",
        }), "old.4sb"), TestContext.Current.CancellationToken);

        Guid oldSnapshotId;
        Guid sharedChartId;
        Guid missingChartId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var oldSnapshot = await db.ForScoreLibrarySnapshots.Include(snapshot => snapshot.Charts).SingleAsync(snapshot => snapshot.IsActive, TestContext.Current.CancellationToken);
            oldSnapshotId = oldSnapshot.Id;
            sharedChartId = oldSnapshot.Charts.Single(chart => chart.FilePath == "Shared.pdf").Id;
            missingChartId = oldSnapshot.Charts.Single(chart => chart.FilePath == "Missing.pdf").Id;

            var gigId = Guid.NewGuid();
            db.Gigs.Add(new Gig
            {
                Id = gigId,
                ClientId = TestData.FoxAndFinchId,
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
                Title = "Future draft gig",
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                Venue = "Future venue",
                Status = GigStatus.Draft,
            });

            db.GigSetListImports.Add(new GigSetListImport
            {
                Id = Guid.NewGuid(),
                GigId = gigId,
                SpreadsheetId = "spreadsheet",
                WorksheetName = "Set 1",
                IsActive = true,
                ImportedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Items =
                [
                    new GigSetListItem
                    {
                        Id = Guid.NewGuid(),
                        SortOrder = 0,
                        SourceRowNumber = 1,
                        Kind = GigSetListItemKind.Song,
                        Include = true,
                        Title = "Shared",
                        ForScoreLibrarySnapshotId = oldSnapshotId,
                        ForScoreChartId = sharedChartId,
                        ForScoreChartTitle = "Shared",
                        ForScoreChartFilePath = "Shared.pdf",
                        ForScoreMappingStatus = ForScoreMappingStatus.Linked,
                        ForScoreMappingConfidence = ForScoreMappingConfidence.Manual,
                    },
                    new GigSetListItem
                    {
                        Id = Guid.NewGuid(),
                        SortOrder = 1,
                        SourceRowNumber = 2,
                        Kind = GigSetListItemKind.Song,
                        Include = true,
                        Title = "Missing",
                        ForScoreLibrarySnapshotId = oldSnapshotId,
                        ForScoreChartId = missingChartId,
                        ForScoreChartTitle = "Missing",
                        ForScoreChartFilePath = "Missing.pdf",
                        ForScoreMappingStatus = ForScoreMappingStatus.Linked,
                        ForScoreMappingConfidence = ForScoreMappingConfidence.Manual,
                    },
                ],
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsync("/forscore-library/imports", BuildUploadContent(ForScoreBackupFixture.Build(new Dictionary<string, object>
        {
            ["Shared.pdf|title"] = "Shared",
        }), "new.4sb"), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var importResult = await response.Content.ReadFromJsonAsync<ImportResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(importResult);
        Assert.Equal(1, importResult.Impact.AffectedSetListCount);
        Assert.Equal(1, importResult.Impact.AutoRelinkedItemCount);
        Assert.Equal(1, importResult.Impact.NeedsReviewItemCount);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await verifyDb.GigSetListItems.OrderBy(item => item.SortOrder).ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(oldSnapshotId, items[0].ForScoreLibrarySnapshotId);
        Assert.Equal(ForScoreMappingStatus.Linked, items[0].ForScoreMappingStatus);
        Assert.Equal(oldSnapshotId, items[1].ForScoreLibrarySnapshotId);
        Assert.Equal(ForScoreMappingStatus.MissingFromLatestLibrary, items[1].ForScoreMappingStatus);
    }

    private async Task SeedAlternateUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User
        {
            Id = TestAuthContext.AlternateUserId,
            GoogleSubject = "alternate-subject",
            Email = "alternate@glovelly.local",
            DisplayName = "Alternate User",
            Role = UserRole.User,
            IsActive = true,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static MultipartFormDataContent BuildUploadContent(byte[] bytes, string fileName = "library.4sb")
    {
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private sealed record SnapshotResponse(Guid Id, bool IsActive, int ChartCount, string OriginalFileName, IReadOnlyList<string> Warnings);

    private sealed record ImportResponse(SnapshotResponse Snapshot, ImpactResponse Impact);

    private sealed record ImpactResponse(int AffectedSetListCount, int AutoRelinkedItemCount, int NeedsReviewItemCount);

    private sealed record ChartsResponse(Guid SnapshotId, IReadOnlyList<ChartResponse> Charts);

    private sealed record ChartResponse(Guid Id, string FilePath, string Title, string NormalizedTitle);
}
