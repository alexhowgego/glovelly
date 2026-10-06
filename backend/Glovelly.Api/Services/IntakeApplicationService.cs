using System.Text;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed record IntakeResourceInput(GigExternalResourceType ResourceType, GigExternalResourcePurpose Purpose, string? Title, string? Notes, bool IsPrimary);
public sealed record IntakeApplicationResponse(IntakeIntent Kind, Gig Gig, Guid? ExpenseId, Guid? AttachmentId, GigExternalResource? Resource);
public sealed record IntakeApplicationResult(IntakeApplicationResponse? Application, string? Field = null, string? Error = null);

// Serializes commands for a user in this process. Stable destination IDs and the
// atomic insert/delete in SaveChanges also protect application across replicas.
public sealed class IntakeCommandLocks
{
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    public SemaphoreSlim ForUser(Guid userId) => _locks[(userId.GetHashCode() & int.MaxValue) % _locks.Length];
}

public sealed class IntakeApplicationService(
    AppDbContext db,
    IExpenseAttachmentStore store,
    IInvoiceWorkflowService invoices,
    IWorkspaceEventPublisher events,
    IOptions<ExpenseAttachmentSettings> attachmentOptions,
    TimeProvider clock,
    ILogger<IntakeApplicationService> logger)
{
    public async Task<IntakeApplicationResponse?> FindAppliedAsync(Guid intakeId, Guid userId, CancellationToken cancellationToken)
    {
        var receiptGig = await db.Gigs.WhereVisibleTo(userId).IncludeGigDetails()
            .FirstOrDefaultAsync(gig => gig.Expenses.Any(expense => expense.Id == intakeId && expense.Attachments.Any(attachment => attachment.Id == intakeId)), cancellationToken);
        if (receiptGig is not null)
            return new(IntakeIntent.Receipt, receiptGig, intakeId, intakeId, null);

        var resourceGig = await db.Gigs.WhereVisibleTo(userId).IncludeGigDetails()
            .FirstOrDefaultAsync(gig => gig.ExternalResources.Any(resource => resource.Id == intakeId), cancellationToken);
        var resource = resourceGig?.ExternalResources.FirstOrDefault(value => value.Id == intakeId);
        return resourceGig is null || resource is null ? null : new(IntakeIntent.Resource, resourceGig, null, resource.Attachments.FirstOrDefault()?.Id, resource);
    }

    public async Task<IntakeApplicationResult> ApplyAsync(Guid intakeId, Guid userId, IntakeIntent intent, Guid gigId, IntakeResourceInput? input, CancellationToken cancellationToken)
    {
        try { return await ApplyCoreAsync(intakeId, userId, intent, gigId, input, cancellationToken); }
        catch (FileNotFoundException)
        {
            db.ChangeTracker.Clear();
            var original = await FindAppliedAsync(intakeId, userId, cancellationToken);
            return original is not null ? new(original) : new(null, "source", "The retained source is unavailable. Choose a new upload.");
        }
    }

    private async Task<IntakeApplicationResult> ApplyCoreAsync(Guid intakeId, Guid userId, IntakeIntent intent, Guid gigId, IntakeResourceInput? input, CancellationToken cancellationToken)
    {
        var applied = await FindAppliedAsync(intakeId, userId, cancellationToken);
        if (applied is not null) return new(applied);

        var intake = await db.CurrentIntakes.Include(value => value.AnalysisAttempts)
            .SingleOrDefaultAsync(value => value.Id == intakeId && value.UserId == userId, cancellationToken);
        if (intake is null) return new(null, "source", "This intake is no longer available. Reopen Add to Glovelly to recover the saved item.");
        if (intent is not (IntakeIntent.Receipt or IntakeIntent.Resource)) return new(null, "intent", "Choose receipt or resource.");
        if (intent == IntakeIntent.Receipt && intake.SourceType == IntakeSourceType.Url)
            return new(null, "source", "Choose a file or pasted receipt text rather than a URL for a receipt.");
        if (intent == IntakeIntent.Receipt && intake.SourceType == IntakeSourceType.File)
        {
            var file = new FormFile(Stream.Null, 0, intake.SizeBytes ?? 0, "file", intake.FileName!) { Headers = new HeaderDictionary(), ContentType = intake.ContentType! };
            if (GigEndpointSupport.ValidateReceiptAttachmentFile(file, attachmentOptions.Value) is not null)
                return new(null, "file", "This file cannot be used as a receipt. Attach it as a resource instead.");
        }
        if (intent == IntakeIntent.Resource)
        {
            if (input is null) return new(null, "resource", "Choose a resource type, purpose and title.");
            var errors = GigResourceValidation.Validate(input.ResourceType, input.Purpose, input.Title, intake.Url, input.Notes);
            if (errors.Count > 0) return new(null, errors.First().Key, errors.First().Value[0]);
        }

        var gig = await db.Gigs.WhereVisibleTo(userId).IncludeGigDetails().FirstOrDefaultAsync(value => value.Id == gigId, cancellationToken);
        if (gig is null) return new(null, "gigId", "Gig does not exist.");

        var now = clock.GetUtcNow();
        var fileName = intake.FileName ?? "Pasted text.txt";
        var contentType = intake.ContentType ?? "text/plain";
        var size = intake.SizeBytes ?? Encoding.UTF8.GetByteCount(intake.Text ?? "");
        string? destinationKey = null;
        if (intent == IntakeIntent.Receipt)
        {
            var fields = ReadFields(intake.EvidenceJson);
            var expense = new GigExpense
            {
                Id = intake.Id, GigId = gig.Id,
                Description = fields.Merchant ?? "Receipt draft", Amount = fields.Total ?? 0,
                Category = fields.Category,
                SortOrder = gig.Expenses.Count == 0 ? 1 : gig.Expenses.Max(value => value.SortOrder) + 1,
            };
            destinationKey = GigEndpointSupport.BuildAttachmentStorageKey(userId, gig.Id, expense.Id, intake.Id);
            await CopySourceAsync(intake, destinationKey, contentType, cancellationToken);
            expense.Attachments.Add(new ExpenseAttachment { Id = intake.Id, GigExpenseId = expense.Id, FileName = fileName, ContentType = contentType, SizeBytes = size, StorageKey = destinationKey, CreatedAt = now });
            db.GigExpenses.Add(expense);
        }
        else
        {
            var resource = new GigExternalResource
            {
                Id = intake.Id, GigId = gig.Id, ResourceType = input!.ResourceType, Purpose = input.Purpose,
                Title = input.Title!.Trim(), Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                Url = intake.Url, IsPrimary = input.IsPrimary, CreatedAt = now, UpdatedAt = now,
            };
            if (intake.SourceType != IntakeSourceType.Url)
            {
                destinationKey = GigEndpointSupport.BuildExternalResourceAttachmentStorageKey(userId, gig.Id, resource.Id, intake.Id);
                await CopySourceAsync(intake, destinationKey, contentType, cancellationToken);
                resource.Attachments.Add(new GigExternalResourceAttachment { Id = intake.Id, GigExternalResourceId = resource.Id, FileName = fileName, ContentType = contentType, SizeBytes = size, StorageKey = destinationKey, CreatedAt = now });
            }
            if (resource.IsPrimary)
                foreach (var existing in gig.ExternalResources.Where(value => value.Purpose == resource.Purpose)) existing.IsPrimary = false;
            db.GigExternalResources.Add(resource);
        }

        db.CurrentIntakes.Remove(intake);
        EndpointSupport.StampUpdate(gig, userId);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var original = await FindAppliedAsync(intakeId, userId, cancellationToken);
            if (original is not null)
            {
                var originalKeys = original.Gig.Expenses.SelectMany(value => value.Attachments).Select(value => value.StorageKey)
                    .Concat(original.Gig.ExternalResources.SelectMany(value => value.Attachments).Select(value => value.StorageKey));
                if (destinationKey is not null && !originalKeys.Contains(destinationKey)) await store.DeleteAsync(destinationKey, cancellationToken);
                return new(original);
            }
            if (destinationKey is not null) await store.DeleteAsync(destinationKey, cancellationToken);
            throw;
        }

        // Once committed, cleanup/refresh errors must not masquerade as a failed
        // attachment save and encourage the user to upload again.
        try
        {
            if (intake.StorageKey is not null) await store.DeleteAsync(intake.StorageKey, cancellationToken);
            await events.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gig.Id, now));
            if (intent == IntakeIntent.Receipt)
            {
                var refreshed = await invoices.RefreshDraftInvoicesForGigsAsync([gig], userId, cancellationToken);
                foreach (var invoice in refreshed) await events.PublishAsync(userId, new WorkspaceEvent("invoices", "updated", invoice.Id, now));
            }
        }
        catch (Exception)
        {
            logger.LogWarning("Post-application cleanup or refresh did not complete for intake {IntakeId}.", intakeId);
        }
        return new(await FindAppliedAsync(intakeId, userId, cancellationToken));
    }

    private async Task CopySourceAsync(CurrentIntake intake, string key, string contentType, CancellationToken cancellationToken)
    {
        if (intake.SourceType == IntakeSourceType.Text)
        {
            await using var text = new MemoryStream(Encoding.UTF8.GetBytes(intake.Text!));
            await store.SaveAsync(key, text, contentType, cancellationToken);
            return;
        }
        var source = await store.OpenReadAsync(intake.StorageKey!, cancellationToken);
        await using (source.Content) await store.SaveAsync(key, source.Content, contentType, cancellationToken);
    }

    private static (string? Merchant, decimal? Total, GigExpenseCategory? Category) ReadFields(string evidence)
    {
        string? merchant = null;
        decimal? total = null;
        GigExpenseCategory? category = null;
        try
        {
            using var document = JsonDocument.Parse(evidence);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return default;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("confidence", out var confidence) || !IsHigh(confidence) || !item.TryGetProperty("field", out var field) || !item.TryGetProperty("value", out var value)) continue;
                var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                switch (field.GetString())
                {
                    case "merchant" when !string.IsNullOrWhiteSpace(text): merchant = text.Trim(); break;
                    case "totalAmount" when decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) && amount >= 0: total = amount; break;
                    case "suggestedCategory" when Enum.TryParse<GigExpenseCategory>(text, true, out var parsed) && Enum.IsDefined(parsed): category = parsed; break;
                }
            }
        }
        catch (JsonException) { }
        return (merchant, total, category);
    }

    private static bool IsHigh(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? string.Equals(value.GetString(), "High", StringComparison.OrdinalIgnoreCase)
        : value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number == (int)ReceiptAnalysisConfidence.High;
}
