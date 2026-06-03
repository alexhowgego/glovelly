using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GigImportModelTests
{
    [Fact]
    public async Task SaveChanges_AllowsIncompleteDraftsWithoutCreatingGigs()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "importer@glovelly.local",
            CreatedUtc = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc),
        });
        dbContext.GigImportBatches.Add(new GigImportBatch
        {
            Id = batchId,
            SourceName = "Spring tour bible.pdf",
            SourceFingerprint = "sha256:abc123",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 20, 10, 30, 0, TimeSpan.Zero),
            CreatedByUserId = userId,
            Notes = "Imported from MCP document extraction.",
            Drafts =
            {
                new GigImportDraft
                {
                    Id = Guid.NewGuid(),
                    ProposedProjectName = "Spring Tour",
                    ProposedDate = new DateOnly(2026, 6, 12),
                    ProposedVenueName = "City Hall",
                    SourceReference = "page 4, row 2",
                    Confidence = GigImportDraftConfidence.Low,
                    WarningsJson = """["missing fee","unclear call time"]""",
                },
                new GigImportDraft
                {
                    Id = Guid.NewGuid(),
                    SourceReference = "page 5, row 1",
                    WarningsJson = """["date not found"]""",
                },
            },
        });

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var savedBatch = await dbContext.GigImportBatches
            .Include(batch => batch.Drafts)
            .SingleAsync(batch => batch.Id == batchId, TestContext.Current.CancellationToken);

        Assert.Equal(GigImportBatchStatus.Draft, savedBatch.Status);
        Assert.Equal(userId, savedBatch.CreatedByUserId);
        Assert.Equal(2, savedBatch.Drafts.Count);
        Assert.All(savedBatch.Drafts, draft => Assert.Equal(GigImportDraftStatus.Pending, draft.Status));
        Assert.Equal(0, await dbContext.Gigs.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChanges_PersistsImportStatusChanges()
    {
        await using var dbContext = CreateDbContext();
        var batch = new GigImportBatch
        {
            Id = Guid.NewGuid(),
            SourceName = "Weekend schedule.xlsx",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 20, 11, 0, 0, TimeSpan.Zero),
            Drafts =
            {
                new GigImportDraft
                {
                    Id = Guid.NewGuid(),
                    ProposedTitle = "Matinee",
                    Status = GigImportDraftStatus.Accepted,
                    Confidence = GigImportDraftConfidence.High,
                    WarningsJson = "[]",
                },
                new GigImportDraft
                {
                    Id = Guid.NewGuid(),
                    ProposedTitle = "Duplicate row",
                    Status = GigImportDraftStatus.Rejected,
                    Confidence = GigImportDraftConfidence.Medium,
                    WarningsJson = """["duplicate"]""",
                },
            },
        };
        dbContext.GigImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        batch.Status = GigImportBatchStatus.Committed;
        batch.Drafts.First(draft => draft.Status == GigImportDraftStatus.Accepted).Status = GigImportDraftStatus.Committed;
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var statuses = await dbContext.GigImportDrafts
            .OrderBy(draft => draft.ProposedTitle)
            .Select(draft => draft.Status)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GigImportBatchStatus.Committed, await dbContext.GigImportBatches.Select(value => value.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal([GigImportDraftStatus.Rejected, GigImportDraftStatus.Committed], statuses);
        Assert.Equal(0, await dbContext.Gigs.CountAsync(TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"gig-import-model-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
