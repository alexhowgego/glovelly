using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed class IntakeWorkflowService(
    AppDbContext db,
    IExpenseAttachmentStore store,
    IntakeAnalysisService analysis,
    IntakeApplicationService application,
    IOptions<QuickCaptureSettings> options,
    TimeProvider clock,
    ILogger<IntakeWorkflowService> logger)
{
    public Task<CurrentIntake?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        db.CurrentIntakes.Include(value => value.AnalysisAttempts).SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);

    public async Task ReplaceAsync(CurrentIntake next, CancellationToken cancellationToken)
    {
        // Delete then insert for the unique UserId index, inside one relational
        // transaction so a failed replacement cannot erase the previous intake.
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var previous = await FindAsync(next.UserId, cancellationToken);
        if (previous is not null)
        {
            db.CurrentIntakes.Remove(previous);
            await db.SaveChangesAsync(cancellationToken);
        }
        db.CurrentIntakes.Add(next);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        if (previous?.StorageKey is not null)
        {
            try { await store.DeleteAsync(previous.StorageKey, cancellationToken); }
            catch (Exception) { logger.LogWarning("Replaced-source cleanup did not complete for intake {IntakeId}.", previous.Id); }
        }
    }

    public async Task DiscardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var intake = await FindAsync(userId, cancellationToken);
        if (intake is null) return;
        db.CurrentIntakes.Remove(intake);
        await db.SaveChangesAsync(cancellationToken);
        if (intake.StorageKey is not null) await store.DeleteAsync(intake.StorageKey, cancellationToken);
    }

    public async Task<object> AnalyseAndApplyAsync(CurrentIntake intake, CancellationToken cancellationToken)
    {
        await analysis.AnalyzeAsync(intake, cancellationToken);
        // The parent was persisted before provider work. Explicitly add the new
        // attempt: EF otherwise treats its preassigned ID as an existing child.
        db.IntakeAnalysisAttempts.Add(intake.AnalysisAttempts.Last());
        await db.SaveChangesAsync(cancellationToken);
        return await CompleteAsync(intake, cancellationToken);
    }

    public async Task<object> CompleteAsync(CurrentIntake intake, CancellationToken cancellationToken)
    {
        var candidates = await CandidatesAsync(intake.UserId, cancellationToken);
        var preference = await db.Users.Where(value => value.Id == intake.UserId)
            .Select(value => value.AutomaticReceiptMatching).SingleAsync(cancellationToken);
        IntakeApplicationResponse? saved = null;
        string? applicationError = null;
        if (intake.AnalysisState == IntakeAnalysisState.Succeeded && intake.Intent == IntakeIntent.Receipt && candidates.Candidates.Count > 0 && AllowsAutomaticApplication(preference, intake.Confidence))
        {
            try
            {
                var result = await application.ApplyAsync(intake.Id, intake.UserId, IntakeIntent.Receipt, candidates.Candidates[0].Id, null, cancellationToken);
                saved = result.Application;
                applicationError = result.Error;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                db.ChangeTracker.Clear();
                saved = await application.FindAppliedAsync(intake.Id, intake.UserId, cancellationToken);
                if (saved is null) applicationError = "The receipt could not be attached. Your source is retained; try attaching it again.";
                logger.LogWarning("Automatic application could not finish normally for intake {IntakeId}.", intake.Id);
            }
        }
        return Response(intake, candidates, saved, applicationError);
    }

    public async Task<object> ReadAsync(CurrentIntake intake, CancellationToken cancellationToken) =>
        Response(intake, await CandidatesAsync(intake.UserId, cancellationToken), null, null);

    public static bool AllowsAutomaticApplication(AutomaticReceiptMatching preference, ReceiptAnalysisConfidence confidence) => preference switch
    {
        AutomaticReceiptMatching.MediumConfidence => confidence is ReceiptAnalysisConfidence.Medium or ReceiptAnalysisConfidence.High,
        AutomaticReceiptMatching.HighConfidence or AutomaticReceiptMatching.VeryHighConfidence => confidence == ReceiptAnalysisConfidence.High,
        _ => false,
    };

    private Task<QuickGigCandidateResult> CandidatesAsync(Guid userId, CancellationToken cancellationToken) =>
        GigQuickCaptureSupport.QueryCandidatesAsync(db, userId, DateOnly.FromDateTime(clock.GetLocalNow().DateTime), GigQuickCaptureSupport.NormalizeSettings(options.Value), cancellationToken: cancellationToken);

    private static object Response(CurrentIntake intake, QuickGigCandidateResult candidates, IntakeApplicationResponse? saved, string? applicationError)
    {
        var suggestedType = intake.Url is not null ? GigExternalResourceEndpoints.InferResourceType(intake.Url) : (GigExternalResourceType?)null;
        return new
        {
            intake.Id, intake.SourceType, sourceName = intake.FileName ?? intake.Url ?? "Pasted text",
            sourceUrl = intake.Url, sourceText = intake.Text, intake.AnalysisState, intake.FailureMessage,
            proposedIntent = intake.Intent, intake.Confidence, suggestedResourceType = suggestedType,
            evidence = Evidence(intake, suggestedType),
            candidates = GigQuickCaptureSupport.ToCandidateResponses(candidates.Candidates, saved?.Gig.Id ?? candidates.Candidates.FirstOrDefault()?.Id),
            hasMoreCandidates = candidates.HasMore, candidateContinuation = candidates.Continuation,
            application = saved, applicationError,
        };
    }

    private static IEnumerable<object> Evidence(CurrentIntake intake, GigExternalResourceType? suggestedType)
    {
        if (intake.Url is not null && Uri.TryCreate(intake.Url, UriKind.Absolute, out var uri))
            return [new { label = "Link", value = uri.Host }, new { label = "Suggested type", value = suggestedType?.ToString() ?? "Url" }];
        try
        {
            using var document = JsonDocument.Parse(intake.EvidenceJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            var facts = new List<object>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("field", out var field) || !item.TryGetProperty("value", out var value)) continue;
                var label = field.GetString() switch
                {
                    "merchant" => "Merchant", "transactionDate" => "Date", "totalAmount" => "Total",
                    "currency" => "Currency", "suggestedCategory" => "Suggested category", "warning" => "Check", _ => null,
                };
                if (label is not null && !string.IsNullOrWhiteSpace(value.ToString())) facts.Add(new { label, value = value.ToString() });
            }
            return facts;
        }
        catch (JsonException) { return []; }
    }
}
