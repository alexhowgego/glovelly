using System.Text;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed record IntakeGigDraftResult(Guid IntakeId, Guid BatchId);

public sealed class IntakeGigDraftService(AppDbContext db, IExpenseAttachmentStore store, IIntakeGigExtractor extractor, IWorkspaceEventPublisher events, TimeProvider clock, ILogger<IntakeGigDraftService> logger)
{
    public async Task<IntakeGigDraftResult?> FindAsync(Guid intakeId, Guid userId, CancellationToken token)
    {
        var batchId = await db.GigImportBatches.AsNoTracking()
            .Where(value => value.CreatedByUserId == userId && value.SourceIntakeId == intakeId)
            .Select(value => (Guid?)value.Id).SingleOrDefaultAsync(token);
        return batchId.HasValue ? new(intakeId, batchId.Value) : null;
    }

    public async Task<IntakeGigDraftResult?> PrepareAsync(Guid intakeId, Guid userId, bool manual, CancellationToken token)
    {
        var existing = await FindAsync(intakeId, userId, token);
        if (existing is not null) return existing;
        var intake = await db.CurrentIntakes.SingleOrDefaultAsync(value => value.Id == intakeId && value.UserId == userId, token);
        if (intake is null || intake.SourceType == IntakeSourceType.Url && intake.Text is null) return null;
        if (intake.IntentOverride is not null && intake.IntentOverride != IntakeIntent.Gigs) throw new GigPreparationException("treatment_changed", "Choose gig information before preparing drafts.");
        IReadOnlyList<GigImportDraft> drafts = manual
            ? [new GigImportDraft { Id = Guid.NewGuid(), Confidence = GigImportDraftConfidence.Low, WarningsJson = "[\"Manual draft: review and complete all fields.\"]" }]
            : await extractor.ExtractAsync(intake, token);
        if (drafts.Count == 0) throw new GigPreparationException("no_gigs", "No gigs were found. Prepare a manual draft or choose another treatment.");

        var batchId = Guid.NewGuid();
        var contentType = intake.ContentType ?? "text/plain";
        var storageKey = $"gig-imports/{userId}/{batchId:N}/source";
        try
        {
            if (intake.SourceType is IntakeSourceType.Text or IntakeSourceType.Url)
            {
                await using var content = new MemoryStream(Encoding.UTF8.GetBytes(intake.Text ?? ""));
                await store.SaveAsync(storageKey, content, contentType, token);
            }
            else
            {
                var source = await store.OpenReadAsync(intake.StorageKey!, token);
                await using (source.Content) await store.SaveAsync(storageKey, source.Content, contentType, token);
            }
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            logger.LogWarning("Gig preparation source copy failed for intake {IntakeId}: {ErrorType}.", intakeId, error.GetType().Name);
            throw new GigPreparationException("source_copy_failed", "The original source could not be saved with the import. Retry or choose another source.");
        }

        var batch = new GigImportBatch
        {
            Id = batchId, CreatedByUserId = userId, CreatedAtUtc = clock.GetUtcNow(),
            SourceIntakeId = intake.Id, SourceFingerprint = intake.SourceFingerprint,
            SourceName = intake.FileName ?? (intake.SourceType == IntakeSourceType.Url ? "Google Doc snapshot.txt" : "Pasted text.txt"), SourceType = intake.SourceType, SourceUrl = intake.Url,
            SourceContentType = contentType, SourceStorageKey = storageKey, SourceEvidenceJson = intake.IntentEvidenceJson,
            Drafts = drafts.ToList(),
        };
        db.GigImportBatches.Add(batch);
        db.CurrentIntakes.Remove(intake);
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException)
        {
            logger.LogWarning("Gig preparation database save failed for intake {IntakeId}; checking for an existing batch.", intakeId);
            db.ChangeTracker.Clear();
            var recovered = await FindAsync(intakeId, userId, token);
            if (recovered?.BatchId != batchId)
            {
                try { await store.DeleteAsync(storageKey, token); }
                catch (Exception) when (!token.IsCancellationRequested) { logger.LogWarning("Unable to clean up uncommitted batch source for intake {IntakeId}.", intakeId); }
            }
            if (recovered is null) throw;
            return recovered;
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            logger.LogWarning("Gig preparation database save failed for intake {IntakeId}: {ErrorType}.", intakeId, error.GetType().Name);
            throw;
        }
        if (intake.StorageKey is not null)
        {
            try { await store.DeleteAsync(intake.StorageKey, token); }
            catch (Exception) when (!token.IsCancellationRequested) { logger.LogWarning("Unable to clean up temporary source for intake {IntakeId}.", intakeId); }
        }
        try { await events.PublishAsync(userId, new WorkspaceEvent("gig-imports", "created", batchId, clock.GetUtcNow())); }
        catch (Exception) when (!token.IsCancellationRequested) { logger.LogWarning("Unable to publish gig import update for intake {IntakeId}.", intakeId); }
        return new(intakeId, batchId);
    }
}
