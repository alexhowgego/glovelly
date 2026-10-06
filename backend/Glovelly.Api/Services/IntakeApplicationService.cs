using System.Text;
using System.Text.Json.Serialization;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed record IntakeResourceInput(GigExternalResourceType ResourceType, GigExternalResourcePurpose Purpose, string? Title, string? Notes, bool IsPrimary);
public sealed record IntakeApplicationResponse(IntakeIntent Kind, Gig Gig, Guid? ExpenseId, Guid? AttachmentId, GigExternalResource? Resource,
    [property: JsonIgnore] string? SourceFingerprint = null)
{
    public IEnumerable<object> Candidates { get; init; } = [];
    public bool HasMoreCandidates { get; init; }
    public string? CandidateContinuation { get; init; }
    public bool HasNearbyCandidates { get; init; }
    public IReadOnlyList<Invoice> Invoices { get; init; } = [];
    public ReceiptAnalysisResult? Analysis { get; init; }
}
public sealed record IntakeApplicationResult(IntakeApplicationResponse? Application, string? Field = null, string? Error = null);

public sealed class IntakeCommandLocks
{
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    public SemaphoreSlim ForUser(Guid userId) => _locks[(userId.GetHashCode() & int.MaxValue) % _locks.Length];
}

public sealed class IntakeApplicationService(AppDbContext db, IExpenseAttachmentStore store, ReceiptSaveEffects effects,
    IOptions<ExpenseAttachmentSettings> attachmentOptions, IOptions<QuickCaptureSettings> quickCaptureOptions, TimeProvider clock)
{
    public async Task<IntakeApplicationResponse?> FindAppliedAsync(Guid intakeId, Guid userId, CancellationToken token)
    {
        var receiptGig = await db.Gigs.WhereVisibleTo(userId).AsNoTracking().IncludeGigDetails()
            .FirstOrDefaultAsync(gig => gig.Expenses.Any(expense => expense.Id == intakeId && expense.Attachments.Any(attachment => attachment.Id == intakeId)), token);
        IntakeApplicationResponse? response;
        if (receiptGig is not null)
            response = new(IntakeIntent.Receipt, receiptGig, intakeId, intakeId, null, receiptGig.Expenses.Single(value => value.Id == intakeId).SourceFingerprint);
        else
        {
            var resourceGig = await db.Gigs.WhereVisibleTo(userId).AsNoTracking().IncludeGigDetails()
                .FirstOrDefaultAsync(gig => gig.ExternalResources.Any(resource => resource.Id == intakeId), token);
            var resource = resourceGig?.ExternalResources.FirstOrDefault(value => value.Id == intakeId);
            response = resourceGig is null || resource is null ? null : new(IntakeIntent.Resource, resourceGig, null, resource.Attachments.FirstOrDefault()?.Id, resource, resource.SourceFingerprint);
        }
        if (response is null) return null;
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var settings = GigQuickCaptureSupport.NormalizeSettings(quickCaptureOptions.Value);
        var page = await GigQuickCaptureSupport.QueryCandidatesAsync(db, userId, today, settings, cancellationToken: token);
        var candidates = page.Candidates.ToList();
        var selected = response.Gig;
        if (candidates.All(value => value.Id != selected.Id))
            candidates.Insert(0, new QuickGigCandidate(selected.Id, selected.ClientId, selected.Title, selected.Date, selected.Venue, selected.Type, selected.Status, Math.Abs(selected.Date.DayNumber - today.DayNumber)));
        var invoices = response.Kind == IntakeIntent.Receipt && selected.InvoiceId.HasValue
            ? await db.Invoices.WhereVisibleTo(userId).AsNoTracking().Include(value => value.Lines).Where(value => value.Id == selected.InvoiceId).ToListAsync(token) : [];
        var latest = response.AttachmentId.HasValue && response.Kind == IntakeIntent.Receipt
            ? await db.ReceiptAnalyses.AsNoTracking().Where(value => value.ExpenseAttachmentId == response.AttachmentId).OrderByDescending(value => value.RequestedAt).ThenByDescending(value => value.CompletedAt).FirstOrDefaultAsync(token) : null;
        return response with
        {
            Candidates = GigQuickCaptureSupport.ToCandidateResponses(candidates, selected.Id),
            HasMoreCandidates = page.HasMore, CandidateContinuation = page.Continuation,
            HasNearbyCandidates = GigQuickCaptureSupport.HasNearbyCandidates(page.Candidates, selected.Id, settings),
            Invoices = invoices, Analysis = latest is null ? null : ReceiptAnalysisResult.FromAnalysis(latest),
        };
    }

    public async Task<IntakeApplicationResult> ApplyAsync(Guid intakeId, Guid userId, IntakeIntent intent, Guid gigId, IntakeResourceInput? input, CancellationToken token)
    {
        var applied = await FindAppliedAsync(intakeId, userId, token);
        if (applied is not null)
        {
            if (applied.Invoices.Any(invoice => invoice.GigLinesNeedRefresh))
            {
                await effects.CompleteAsync([applied.Gig], userId, []);
                applied = await FindAppliedAsync(intakeId, userId, token);
            }
            return new(applied);
        }
        var intake = await db.CurrentIntakes.Include(value => value.AnalysisAttempts).SingleOrDefaultAsync(value => value.Id == intakeId && value.UserId == userId, token);
        if (intake is null) return new(null, "source", "This intake is no longer available. Reopen Add to Glovelly to recover the saved item.");
        if (intent is not (IntakeIntent.Receipt or IntakeIntent.Resource)) return new(null, "intent", "Choose receipt or resource.");
        if (intent == IntakeIntent.Receipt && intake.SourceType == IntakeSourceType.Url) return new(null, "source", "Choose a file or pasted receipt text rather than a URL for a receipt.");
        if (intent == IntakeIntent.Receipt && intake.SourceType == IntakeSourceType.File)
        {
            var file = new FormFile(Stream.Null, 0, intake.SizeBytes ?? 0, "file", intake.FileName!) { Headers = new HeaderDictionary(), ContentType = intake.ContentType! };
            if (GigEndpointSupport.ValidateReceiptAttachmentFile(file, attachmentOptions.Value) is not null) return new(null, "file", "This file cannot be used as a receipt. Attach it as a resource instead.");
        }
        if (intent == IntakeIntent.Resource)
        {
            if (input is null) return new(null, "resource", "Choose a resource type, purpose and title.");
            var errors = GigResourceValidation.Validate(input.ResourceType, input.Purpose, input.Title, intake.Url, input.Notes);
            if (errors.Count > 0) return new(null, errors.First().Key, errors.First().Value[0]);
        }
        var gig = await db.Gigs.WhereVisibleTo(userId).IncludeGigDetails().FirstOrDefaultAsync(value => value.Id == gigId, token);
        if (gig is null) return new(null, "gigId", "Gig does not exist.");
        var now = clock.GetUtcNow();
        var fileName = intake.FileName ?? "Pasted text.txt";
        var contentType = intake.ContentType ?? "text/plain";
        var size = intake.SizeBytes ?? Encoding.UTF8.GetByteCount(intake.Text ?? "");
        string? key = null;
        try
        {
            if (intent == IntakeIntent.Receipt)
            {
                var analysis = ReceiptAnalysisEvidence.Read(intake.EvidenceJson);
                var expense = new GigExpense
                {
                    Id = intake.Id, GigId = gig.Id, SourceFingerprint = intake.SourceFingerprint,
                    Description = analysis?.MerchantConfidence == ReceiptAnalysisConfidence.High ? analysis.Merchant ?? "Receipt draft" : "Receipt draft",
                    Amount = analysis is not null && analysis.TotalAmountConfidence == ReceiptAnalysisConfidence.High && ReceiptAnalysisEvidence.HasConfirmedGbp(analysis) ? analysis.TotalAmount ?? 0 : 0,
                    Category = analysis?.SuggestedCategoryConfidence == ReceiptAnalysisConfidence.High && Enum.TryParse<GigExpenseCategory>(analysis.SuggestedCategory, true, out var category) && Enum.IsDefined(category) ? category : null,
                    SortOrder = gig.Expenses.Count == 0 ? 1 : gig.Expenses.Max(value => value.SortOrder) + 1,
                };
                key = GigEndpointSupport.BuildAttachmentStorageKey(userId, gig.Id, expense.Id, intake.Id);
                await CopySourceAsync(intake, key, contentType, token);
                var attachment = new ExpenseAttachment { Id = intake.Id, GigExpenseId = expense.Id, FileName = fileName, ContentType = contentType, SizeBytes = size, StorageKey = key, CreatedAt = now };
                foreach (var attempt in intake.AnalysisAttempts)
                {
                    var result = ReceiptAnalysisEvidence.Read(attempt.EvidenceJson);
                    if (result is null) continue;
                    result.ExpenseAttachmentId = attachment.Id;
                    attachment.Analyses.Add(result);
                }
                expense.Attachments.Add(attachment);
                db.GigExpenses.Add(expense);
                await effects.PrepareAsync([gig], userId, token);
            }
            else
            {
                var resource = GigResourceMutations.Create(intake.Id, gig.Id, input!.ResourceType, input.Purpose, input.Title!, intake.Url, input.Notes, input.IsPrimary, now);
                resource.SourceFingerprint = intake.SourceFingerprint;
                if (intake.SourceType != IntakeSourceType.Url)
                {
                    key = GigEndpointSupport.BuildExternalResourceAttachmentStorageKey(userId, gig.Id, resource.Id, intake.Id);
                    await CopySourceAsync(intake, key, contentType, token);
                    resource.Attachments.Add(new GigExternalResourceAttachment { Id = intake.Id, GigExternalResourceId = resource.Id, FileName = fileName, ContentType = contentType, SizeBytes = size, StorageKey = key, CreatedAt = now });
                }
                if (resource.IsPrimary) GigResourceMutations.ClearPrimary(gig.ExternalResources, resource.Purpose, resource.Id);
                db.GigExternalResources.Add(resource);
            }
        }
        catch (FileNotFoundException) { return new(null, "source", "The retained source is unavailable. Choose a new upload."); }
        db.CurrentIntakes.Remove(intake);
        EndpointSupport.StampUpdate(gig, userId);
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var original = await FindAppliedAsync(intakeId, userId, token);
            if (original is not null) return new(original);
            if (key is not null) await store.DeleteAsync(key, token);
            throw;
        }
        await effects.CompleteAsync([gig], userId, intake.StorageKey is null ? [] : [intake.StorageKey], intent == IntakeIntent.Receipt);
        return new(await FindAppliedAsync(intakeId, userId, token));
    }

    private async Task CopySourceAsync(CurrentIntake intake, string key, string contentType, CancellationToken token)
    {
        if (intake.SourceType == IntakeSourceType.Text)
        {
            await using var text = new MemoryStream(Encoding.UTF8.GetBytes(intake.Text!));
            await store.SaveAsync(key, text, contentType, token);
        }
        else
        {
            var source = await store.OpenReadAsync(intake.StorageKey!, token);
            await using (source.Content) await store.SaveAsync(key, source.Content, contentType, token);
        }
    }
}
