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
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, payload.GetProperty("createdCount").GetInt32());
        var gigId = payload.GetProperty("gigIds")[0].GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gig = await db.Gigs.Include(value => value.Expenses).SingleAsync(value => value.Id == gigId);
        var draftStatus = await db.GigImportDrafts
            .Where(value => value.Id == draftId)
            .Select(value => value.Status)
            .SingleAsync();

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
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
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
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var drafts = await db.GigImportDrafts
            .Where(value => value.BatchId == batchId)
            .OrderBy(value => value.ProposedTitle)
            .Select(value => new { value.Id, value.Status })
            .ToListAsync();

        Assert.DoesNotContain(drafts, draft => draft.Id == rejectedDraftId);
        Assert.Contains(drafts, draft => draft.Id == acceptedDraftId && draft.Status == GigImportDraftStatus.Committed);
        Assert.Contains(drafts, draft => draft.Id == pendingDraftId && draft.Status == GigImportDraftStatus.Pending);
        Assert.Equal(GigImportBatchStatus.Draft, await db.GigImportBatches.Where(value => value.Id == batchId).Select(value => value.Status).SingleAsync());
    }

    private async Task SeedImportBatchAsync(Guid batchId, Guid draftId, bool includeRequiredFields = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.GigImportBatches.Add(new GigImportBatch
        {
            Id = batchId,
            SourceName = "Swing Into Christmas 2026",
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

        await db.SaveChangesAsync();
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

        await db.SaveChangesAsync();
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
        return await db.Gigs.CountAsync();
    }
}
