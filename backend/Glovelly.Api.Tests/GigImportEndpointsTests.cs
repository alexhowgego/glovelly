using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GigImportEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly Guid OtherClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public GigImportEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CommitAcceptedRows_CreatesLinkedGigAndMarksDraftCommitted()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedImportBatchAsync(batchId, draftId);

        var response = await _client.PostAsJsonAsync($"/gig-imports/{batchId}/commit", new
        {
            draftIds = Array.Empty<Guid>(),
            commitAccepted = true,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(1, payload.GetProperty("createdCount").GetInt32());
        var gigId = payload.GetProperty("gigIds")[0].GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gig = await db.Gigs.Include(value => value.Expenses).SingleAsync(value => value.Id == gigId, TestContext.Current.CancellationToken);
        var draftStatus = await db.GigImportDrafts
            .Where(value => value.Id == draftId)
            .Select(value => value.Status)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TestData.FoxAndFinchId, gig.ClientId);
        Assert.Equal("Swing Into Christmas", gig.Title);
        Assert.Equal(new DateOnly(2026, 11, 28), gig.Date);
        Assert.Equal("Music Hall, Aberdeen, AB10 1AA", gig.Venue);
        Assert.Equal(250m, gig.Fee);
        Assert.Equal(batchId, gig.SourceImportBatchId);
        Assert.Equal(draftId, gig.SourceImportDraftId);
        Assert.Equal(GigImportDraftStatus.Committed, draftStatus);
        Assert.Collection(gig.Expenses, expense =>
        {
            Assert.Equal("Per diem", expense.Description);
            Assert.Equal(30m, expense.Amount);
        });
    }

    [Fact]
    public async Task CommitSelectedRows_WithMissingRequiredFields_ReturnsValidationProblem()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedImportBatchAsync(batchId, draftId, includeRequiredFields: false);

        var response = await _client.PostAsJsonAsync($"/gig-imports/{batchId}/commit", new
        {
            draftIds = new[] { draftId },
            commitAccepted = false,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var errors = problem.GetProperty("errors").GetProperty($"drafts.{draftId}");
        Assert.Contains(errors.EnumerateArray(), value => value.GetString() == "Client is required.");
        Assert.Contains(errors.EnumerateArray(), value => value.GetString() == "Title is required.");
        Assert.Equal(0, await CountGigsAsync());
    }

    [Fact]
    public async Task CommitAcceptedRows_DeletesRejectedDraftsAndKeepsPendingRows()
    {
        var batchId = Guid.NewGuid();
        var acceptedDraftId = Guid.NewGuid();
        var rejectedDraftId = Guid.NewGuid();
        var pendingDraftId = Guid.NewGuid();
        await SeedImportBatchWithDecisionStatesAsync(batchId, acceptedDraftId, rejectedDraftId, pendingDraftId);

        var response = await _client.PostAsJsonAsync($"/gig-imports/{batchId}/commit", new
        {
            draftIds = Array.Empty<Guid>(),
            commitAccepted = true,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var drafts = await db.GigImportDrafts
            .Where(value => value.BatchId == batchId)
            .OrderBy(value => value.ProposedTitle)
            .Select(value => new { value.Id, value.Status })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(drafts, draft => draft.Id == rejectedDraftId);
        Assert.Contains(drafts, draft => draft.Id == acceptedDraftId && draft.Status == GigImportDraftStatus.Committed);
        Assert.Contains(drafts, draft => draft.Id == pendingDraftId && draft.Status == GigImportDraftStatus.Pending);
        Assert.Equal(GigImportBatchStatus.Draft, await db.GigImportBatches.Where(value => value.Id == batchId).Select(value => value.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetBatch_WithSameDateAndVenueExistingGig_ReturnsDuplicateWarning()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedExistingGigAsync("Different show", new DateOnly(2026, 11, 28), "Music Hall, Aberdeen, AB10 1AA", OtherClientId);
        await SeedImportBatchAsync(batchId, draftId);

        var response = await _client.GetAsync($"/gig-imports/{batchId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warnings = await ReadSingleDraftWarningsAsync(response);
        Assert.Contains(warnings, warning => warning == "Possible duplicate: existing gig found on 2026-11-28 at Music Hall, Aberdeen, AB10 1AA.");
    }

    [Fact]
    public async Task GetBatch_WithSameClientAndDateExistingGig_ReturnsDuplicateWarning()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedExistingGigAsync("Different show", new DateOnly(2026, 11, 28), "Other venue", TestData.FoxAndFinchId);
        await SeedImportBatchAsync(batchId, draftId);

        var response = await _client.GetAsync($"/gig-imports/{batchId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warnings = await ReadSingleDraftWarningsAsync(response);
        Assert.Contains(warnings, warning => warning == "Possible duplicate: existing gig found for this client on 2026-11-28.");
    }

    [Fact]
    public async Task GetBatch_WithSimilarTitleAndDateExistingGig_ReturnsDuplicateWarning()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedExistingGigAsync("Swing into Christmas", new DateOnly(2026, 11, 28), "Other venue", OtherClientId);
        await SeedImportBatchAsync(batchId, draftId);

        var response = await _client.GetAsync($"/gig-imports/{batchId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warnings = await ReadSingleDraftWarningsAsync(response);
        Assert.Contains(warnings, warning => warning == "Possible duplicate: similar existing gig found on 2026-11-28: Swing into Christmas.");
    }

    [Fact]
    public async Task GetBatch_WithPreviouslyImportedSourceFingerprint_ReturnsDuplicateWarning()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedPriorImportBatchAsync("sha256:tour-bible");
        await SeedImportBatchAsync(batchId, draftId, sourceFingerprint: "sha256:tour-bible");

        var response = await _client.GetAsync($"/gig-imports/{batchId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warnings = await ReadSingleDraftWarningsAsync(response);
        Assert.Contains(warnings, warning => warning == "Possible duplicate: this source fingerprint has been imported before.");
    }

    [Fact]
    public async Task GetBatch_WithOtherUsersMatchingGig_DoesNotReturnDuplicateWarning()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedExistingGigAsync(
            "Swing Into Christmas",
            new DateOnly(2026, 11, 28),
            "Music Hall, Aberdeen, AB10 1AA",
            TestData.FoxAndFinchId,
            TestAuthContext.AlternateUserId);
        await SeedImportBatchAsync(batchId, draftId);

        var response = await _client.GetAsync($"/gig-imports/{batchId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warnings = await ReadSingleDraftWarningsAsync(response);
        Assert.DoesNotContain(warnings, warning => warning.StartsWith("Possible duplicate:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateDraft_WhenEditedIntoDuplicate_ReturnsUpdatedWarning()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedExistingGigAsync("Different show", new DateOnly(2026, 12, 8), "Bath Forum", OtherClientId);
        await SeedImportBatchAsync(batchId, draftId);

        var response = await _client.PutAsJsonAsync($"/gig-imports/{batchId}/drafts/{draftId}", new
        {
            proposedClientId = TestData.FoxAndFinchId,
            clientName = "Fox & Finch",
            title = "Edited row",
            date = "2026-12-08",
            venueName = "Bath Forum",
            venueAddress = (string?)null,
            postcode = (string?)null,
            fee = 250m,
            perDiem = 30m,
            confidence = "High",
            warnings = Array.Empty<string>(),
            status = "Pending",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        var warnings = payload.GetProperty("warnings").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("Possible duplicate: existing gig found on 2026-12-08 at Bath Forum.", warnings);
    }

    [Fact]
    public async Task CommitAcceptedRows_WithDuplicateWarning_StillCreatesGig()
    {
        var batchId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        await SeedExistingGigAsync("Different show", new DateOnly(2026, 11, 28), "Music Hall, Aberdeen, AB10 1AA", OtherClientId);
        await SeedImportBatchAsync(batchId, draftId);

        var response = await _client.PostAsJsonAsync($"/gig-imports/{batchId}/commit", new
        {
            draftIds = Array.Empty<Guid>(),
            commitAccepted = true,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(1, payload.GetProperty("createdCount").GetInt32());
    }

    private async Task SeedImportBatchAsync(
        Guid batchId,
        Guid draftId,
        bool includeRequiredFields = true,
        string? sourceFingerprint = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.GigImportBatches.Add(new GigImportBatch
        {
            Id = batchId,
            SourceName = "Swing Into Christmas 2026",
            SourceFingerprint = sourceFingerprint,
            Status = GigImportBatchStatus.Draft,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero),
            CreatedByUserId = TestAuthContext.UserId,
            Drafts =
            {
                new GigImportDraft
                {
                    Id = draftId,
                    ProposedClientId = includeRequiredFields ? TestData.FoxAndFinchId : null,
                    ProposedTitle = includeRequiredFields ? "Swing Into Christmas" : null,
                    ProposedDate = includeRequiredFields ? new DateOnly(2026, 11, 28) : null,
                    ProposedVenueName = includeRequiredFields ? "Music Hall" : null,
                    ProposedVenueAddress = includeRequiredFields ? "Aberdeen" : null,
                    ProposedVenuePostcode = includeRequiredFields ? "AB10 1AA" : null,
                    ProposedFee = 250m,
                    ProposedPerDiem = 30m,
                    Status = GigImportDraftStatus.Accepted,
                    Confidence = GigImportDraftConfidence.High,
                    WarningsJson = "[]",
                },
            },
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedExistingGigAsync(
        string title,
        DateOnly date,
        string venue,
        Guid clientId,
        Guid? createdByUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Gigs.Add(new Gig
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            CreatedByUserId = createdByUserId ?? TestAuthContext.UserId,
            UpdatedByUserId = createdByUserId ?? TestAuthContext.UserId,
            Title = title,
            Date = date,
            Venue = venue,
            Fee = 200m,
            TravelMiles = 0m,
            WasDriving = false,
            Status = GigStatus.Confirmed,
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedPriorImportBatchAsync(string sourceFingerprint)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.GigImportBatches.Add(new GigImportBatch
        {
            Id = Guid.NewGuid(),
            SourceName = "Earlier tour bible",
            SourceFingerprint = sourceFingerprint,
            Status = GigImportBatchStatus.Committed,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 19, 10, 0, 0, TimeSpan.Zero),
            CreatedByUserId = TestAuthContext.UserId,
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string[]> ReadSingleDraftWarningsAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        return payload
            .GetProperty("drafts")[0]
            .GetProperty("warnings")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }

    private async Task SeedImportBatchWithDecisionStatesAsync(
        Guid batchId,
        Guid acceptedDraftId,
        Guid rejectedDraftId,
        Guid pendingDraftId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.GigImportBatches.Add(new GigImportBatch
        {
            Id = batchId,
            SourceName = "Multi-pass import",
            Status = GigImportBatchStatus.Draft,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero),
            CreatedByUserId = TestAuthContext.UserId,
            Drafts =
            {
                CreateDraft(acceptedDraftId, "Accepted row", GigImportDraftStatus.Accepted),
                CreateDraft(rejectedDraftId, "Rejected row", GigImportDraftStatus.Rejected),
                CreateDraft(pendingDraftId, "Pending row", GigImportDraftStatus.Pending),
            },
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static GigImportDraft CreateDraft(Guid draftId, string title, GigImportDraftStatus status)
    {
        return new GigImportDraft
        {
            Id = draftId,
            ProposedClientId = TestData.FoxAndFinchId,
            ProposedTitle = title,
            ProposedDate = new DateOnly(2026, 11, 28),
            ProposedVenueName = "Music Hall",
            ProposedFee = 250m,
            Status = status,
            Confidence = GigImportDraftConfidence.High,
            WarningsJson = "[]",
        };
    }

    private async Task<int> CountGigsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Gigs.CountAsync(TestContext.Current.CancellationToken);
    }
}
