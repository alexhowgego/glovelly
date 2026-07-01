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
        var snapshot = await response.Content.ReadFromJsonAsync<SnapshotResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsActive);
        Assert.Equal(1, snapshot.ChartCount);

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

    private sealed record ChartsResponse(Guid SnapshotId, IReadOnlyList<ChartResponse> Charts);

    private sealed record ChartResponse(Guid Id, string FilePath, string Title, string NormalizedTitle);
}
