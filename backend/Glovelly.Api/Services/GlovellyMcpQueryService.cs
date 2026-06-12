using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public interface IGlovellyMcpQueryService
{
    Task<ContactSearchResult> SearchContactsAsync(Guid userId, string? query, CancellationToken cancellationToken);
    Task<InvoiceListResult> ListInvoicesAsync(Guid userId, InvoiceListRequest request, CancellationToken cancellationToken);
    Task<InvoiceDetail?> GetInvoiceAsync(Guid userId, Guid invoiceId, CancellationToken cancellationToken);
    Task<ReceiptListResult> ListReceiptsAsync(Guid userId, ReceiptListRequest request, CancellationToken cancellationToken);
    Task<BusinessSummaryResult> GetBusinessSummaryAsync(Guid userId, BusinessSummaryRequest request, CancellationToken cancellationToken);
    Task<GigImportBatchCreateResult> CreateGigImportBatchAsync(Guid userId, GigImportBatchCreateRequest request, CancellationToken cancellationToken);
    Task<GigImportBatchListResult> ListGigImportBatchesAsync(Guid userId, CancellationToken cancellationToken);
    Task<GigImportBatchGetResult> GetGigImportBatchAsync(Guid userId, Guid batchId, CancellationToken cancellationToken);
    Task<GigImportDraftAddResult> AddGigImportDraftAsync(Guid userId, GigImportDraftAddRequest request, CancellationToken cancellationToken);
    Task<GigImportDraftBulkAddResult> AddGigImportDraftsAsync(Guid userId, GigImportDraftBulkAddRequest request, CancellationToken cancellationToken);
}

public sealed class GlovellyMcpQueryService(
    AppDbContext db,
    IWorkspaceEventPublisher workspaceEventPublisher) : IGlovellyMcpQueryService
{
    private const string DefaultCurrency = "GBP";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ContactSearchResult> SearchContactsAsync(Guid userId, string? query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        var contactsQuery = db.Clients
            .WhereVisibleTo(userId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var lowered = normalizedQuery.ToLowerInvariant();
            contactsQuery = contactsQuery.Where(contact =>
                contact.Name.ToLower().Contains(lowered) ||
                contact.Email.ToLower().Contains(lowered));
        }

        var matches = await contactsQuery
            .OrderBy(contact => contact.Name)
            .ThenBy(contact => contact.Email)
            .Take(10)
            .Select(contact => new ContactMatch(contact.Id, contact.Name, contact.Email))
            .ToListAsync(cancellationToken);

        return new ContactSearchResult(normalizedQuery ?? string.Empty, matches);
    }

    public async Task<InvoiceListResult> ListInvoicesAsync(Guid userId, InvoiceListRequest request, CancellationToken cancellationToken)
    {
        var contactId = request.ContactId;
        if (!contactId.HasValue && !string.IsNullOrWhiteSpace(request.ContactQuery))
        {
            var contactMatches = await SearchContactsAsync(userId, request.ContactQuery, cancellationToken);
            if (contactMatches.Matches.Count == 0)
            {
                return InvoiceListResult.Success([], 0m, DefaultCurrency);
            }

            if (contactMatches.Matches.Count > 1)
            {
                return InvoiceListResult.WithAmbiguity(
                    $"Contact query matched {contactMatches.Matches.Count} contacts.",
                    contactMatches.Matches);
            }

            contactId = contactMatches.Matches[0].ContactId;
        }

        var query = db.Invoices
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Include(invoice => invoice.Client)
            .Include(invoice => invoice.Lines)
            .AsQueryable();

        if (contactId.HasValue)
        {
            query = query.Where(invoice => invoice.ClientId == contactId.Value);
        }

        query = ApplyInvoiceStatusFilter(query, request.Status);
        query = ApplyInvoiceDateFilter(query, request);

        var invoices = await query
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ThenBy(invoice => invoice.InvoiceNumber)
            .Select(invoice => ToInvoiceSummary(invoice))
            .ToListAsync(cancellationToken);

        return InvoiceListResult.Success(invoices, invoices.Sum(invoice => invoice.OutstandingAmount), DefaultCurrency);
    }

    public async Task<InvoiceDetail?> GetInvoiceAsync(Guid userId, Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Include(value => value.Client)
            .Include(value => value.Lines)
            .FirstOrDefaultAsync(value => value.Id == invoiceId, cancellationToken);

        return invoice is null ? null : ToInvoiceDetail(invoice);
    }

    public async Task<ReceiptListResult> ListReceiptsAsync(Guid userId, ReceiptListRequest request, CancellationToken cancellationToken)
    {
        var query = db.GigExpenses
            .AsNoTracking()
            .Include(expense => expense.Attachments)
            .Include(expense => expense.Gig)
                .ThenInclude(gig => gig!.Client)
            .Where(expense => expense.Gig != null)
            .Where(expense => expense.Gig!.CreatedByUserId == null || expense.Gig.CreatedByUserId == userId);

        if (request.FromDate.HasValue)
        {
            query = query.Where(expense => expense.Gig!.Date >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(expense => expense.Gig!.Date <= request.ToDate.Value);
        }

        query = ApplyReceiptStatusFilter(query, request.Status);

        var receipts = await query
            .OrderByDescending(expense => expense.Gig!.Date)
            .ThenBy(expense => expense.Description)
            .Select(expense => ToReceiptSummary(expense))
            .ToListAsync(cancellationToken);

        return new ReceiptListResult(
            receipts,
            receipts.Count,
            receipts.Count(receipt => receipt.Status == ReceiptStatusValues.Unmatched),
            receipts.Sum(receipt => receipt.Amount),
            DefaultCurrency);
    }

    public async Task<BusinessSummaryResult> GetBusinessSummaryAsync(Guid userId, BusinessSummaryRequest request, CancellationToken cancellationToken)
    {
        var invoiceQuery = ApplyInvoiceDateFilter(
            db.Invoices
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(invoice => invoice.Lines)
                .AsQueryable(),
            new InvoiceListRequest(null, null, null, request.FromDate, request.ToDate, InvoiceDateBasis.IssueDate));

        var invoices = await invoiceQuery.ToListAsync(cancellationToken);
        var receipts = await ListReceiptsAsync(userId, new ReceiptListRequest(request.FromDate, request.ToDate, null), cancellationToken);

        return new BusinessSummaryResult(
            request.FromDate,
            request.ToDate,
            invoices.Sum(invoice => invoice.Total),
            invoices.Where(invoice => invoice.Status == InvoiceStatus.Paid).Sum(invoice => invoice.Total),
            invoices.Sum(CalculateOutstandingAmount),
            receipts.TotalAmount,
            receipts.ReceiptCount,
            receipts.UnmatchedReceiptCount,
            DefaultCurrency);
    }

    public async Task<GigImportBatchCreateResult> CreateGigImportBatchAsync(
        Guid userId,
        GigImportBatchCreateRequest request,
        CancellationToken cancellationToken)
    {
        var validationErrors = new List<string>();
        var sourceName = NormalizeForStorage(request.SourceName);
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            validationErrors.Add("sourceName is required.");
        }

        if (sourceName?.Length > 300)
        {
            validationErrors.Add("sourceName must be 300 characters or fewer.");
        }

        var notes = NormalizeForStorage(request.Notes);
        if (notes?.Length > 4000)
        {
            validationErrors.Add("notes must be 4000 characters or fewer.");
        }

        var sourceFingerprint = NormalizeForStorage(request.SourceFingerprint);
        if (sourceFingerprint?.Length > 200)
        {
            validationErrors.Add("sourceFingerprint must be 200 characters or fewer.");
        }

        if (validationErrors.Count > 0)
        {
            return new GigImportBatchCreateResult(false, validationErrors, null);
        }

        var batch = new GigImportBatch
        {
            Id = Guid.NewGuid(),
            SourceName = sourceName!,
            SourceFingerprint = sourceFingerprint,
            Notes = notes,
            Status = GigImportBatchStatus.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = userId,
        };

        db.GigImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gig-imports", "created", batch.Id, DateTimeOffset.UtcNow), cancellationToken);

        return new GigImportBatchCreateResult(true, [], ToGigImportBatchSummary(batch, 0));
    }

    public async Task<GigImportBatchListResult> ListGigImportBatchesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var batches = await db.GigImportBatches
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Select(batch => new
            {
                Batch = batch,
                DraftCount = batch.Drafts.Count,
            })
            .OrderByDescending(value => value.Batch.CreatedAtUtc)
            .ThenBy(value => value.Batch.SourceName)
            .ToListAsync(cancellationToken);

        return new GigImportBatchListResult(
            batches
                .Select(value => ToGigImportBatchSummary(value.Batch, value.DraftCount))
                .ToList());
    }

    public async Task<GigImportBatchGetResult> GetGigImportBatchAsync(Guid userId, Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.GigImportBatches
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Include(value => value.Drafts)
            .FirstOrDefaultAsync(value => value.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return new GigImportBatchGetResult(false, null);
        }

        return new GigImportBatchGetResult(
            true,
            new GigImportBatchDetail(
                batch.Id,
                batch.SourceName,
                batch.SourceFingerprint,
                batch.Status.ToString().ToLowerInvariant(),
                batch.CreatedAtUtc,
                batch.Notes,
                batch.Drafts.Count,
                batch.Drafts
                    .OrderBy(draft => draft.ProposedDate ?? DateOnly.MaxValue)
                    .ThenBy(draft => draft.ProposedTitle)
                    .ThenBy(draft => draft.SourceReference)
                    .Select(ToGigImportDraftDetail)
                    .ToList()));
    }

    public async Task<GigImportDraftAddResult> AddGigImportDraftAsync(
        Guid userId,
        GigImportDraftAddRequest request,
        CancellationToken cancellationToken)
    {
        var result = await AddGigImportDraftCoreAsync(userId, request, 0, cancellationToken);
        if (result.Created)
        {
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gig-imports", "updated", request.BatchId, DateTimeOffset.UtcNow), cancellationToken);
        }

        return result;
    }

    public async Task<GigImportDraftBulkAddResult> AddGigImportDraftsAsync(
        Guid userId,
        GigImportDraftBulkAddRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BatchId == Guid.Empty)
        {
            return new GigImportDraftBulkAddResult(false, 0, 0, [new GigImportDraftAddResult(false, false, 0, ["batchId is required."], [], null)]);
        }

        var drafts = request.Drafts ?? [];
        if (drafts.Count == 0)
        {
            var batchFound = await db.GigImportBatches
                .WhereVisibleTo(userId)
                .AnyAsync(value => value.Id == request.BatchId, cancellationToken);
            return new GigImportDraftBulkAddResult(batchFound, 0, 0, []);
        }

        var results = new List<GigImportDraftAddResult>(drafts.Count);

        for (var index = 0; index < drafts.Count; index++)
        {
            var draftRequest = drafts[index] with { BatchId = request.BatchId };
            results.Add(await AddGigImportDraftCoreAsync(userId, draftRequest, index, cancellationToken));
        }

        if (results.Any(result => result.Created))
        {
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gig-imports", "updated", request.BatchId, DateTimeOffset.UtcNow), cancellationToken);
        }

        return new GigImportDraftBulkAddResult(
            results.All(result => result.BatchFound),
            drafts.Count,
            results.Count(result => result.Created),
            results);
    }

    private static IQueryable<Invoice> ApplyInvoiceStatusFilter(IQueryable<Invoice> query, string? status)
    {
        return Normalize(status) switch
        {
            null or "" or "all" => query,
            "outstanding" => query.Where(invoice => invoice.Status == InvoiceStatus.Issued || invoice.Status == InvoiceStatus.Overdue),
            "sent" or "issued" => query.Where(invoice => invoice.Status == InvoiceStatus.Issued),
            "draft" => query.Where(invoice => invoice.Status == InvoiceStatus.Draft),
            "paid" => query.Where(invoice => invoice.Status == InvoiceStatus.Paid),
            "overdue" => query.Where(invoice => invoice.Status == InvoiceStatus.Overdue),
            "cancelled" or "canceled" => query.Where(invoice => invoice.Status == InvoiceStatus.Cancelled),
            _ => query.Where(invoice => invoice.Status.ToString().ToLower() == Normalize(status)),
        };
    }

    private static IQueryable<Invoice> ApplyInvoiceDateFilter(IQueryable<Invoice> query, InvoiceListRequest request)
    {
        var dateBasis = request.DateBasis ?? InvoiceDateBasis.IssueDate;
        return dateBasis switch
        {
            InvoiceDateBasis.DueDate => ApplyDateFilter(query, request.FromDate, request.ToDate, invoice => invoice.DueDate),
            _ => ApplyDateFilter(query, request.FromDate, request.ToDate, invoice => invoice.InvoiceDate),
        };
    }

    private static IQueryable<Invoice> ApplyDateFilter(
        IQueryable<Invoice> query,
        DateOnly? fromDate,
        DateOnly? toDate,
        System.Linq.Expressions.Expression<Func<Invoice, DateOnly>> dateSelector)
    {
        if (fromDate.HasValue)
        {
            var from = fromDate.Value;
            query = query.Where(ReplaceComparison(dateSelector, from, greaterThanOrEqual: true));
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value;
            query = query.Where(ReplaceComparison(dateSelector, to, greaterThanOrEqual: false));
        }

        return query;
    }

    private static System.Linq.Expressions.Expression<Func<Invoice, bool>> ReplaceComparison(
        System.Linq.Expressions.Expression<Func<Invoice, DateOnly>> selector,
        DateOnly value,
        bool greaterThanOrEqual)
    {
        var comparison = greaterThanOrEqual
            ? System.Linq.Expressions.Expression.GreaterThanOrEqual(selector.Body, System.Linq.Expressions.Expression.Constant(value))
            : System.Linq.Expressions.Expression.LessThanOrEqual(selector.Body, System.Linq.Expressions.Expression.Constant(value));

        return System.Linq.Expressions.Expression.Lambda<Func<Invoice, bool>>(comparison, selector.Parameters);
    }

    private static IQueryable<GigExpense> ApplyReceiptStatusFilter(IQueryable<GigExpense> query, string? status)
    {
        return Normalize(status) switch
        {
            null or "" or "all" => query,
            ReceiptStatusValues.Unmatched => query.Where(expense =>
                expense.Amount == 0m ||
                expense.Description.ToLower().Contains("receipt draft")),
            ReceiptStatusValues.Matched => query.Where(expense =>
                expense.Amount > 0m &&
                !expense.Description.ToLower().Contains("receipt draft")),
            _ => query,
        };
    }

    private static InvoiceSummary ToInvoiceSummary(Invoice invoice)
    {
        return new InvoiceSummary(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.ClientId,
            invoice.Client?.Name ?? string.Empty,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Status.ToString().ToLowerInvariant(),
            invoice.Total,
            CalculateOutstandingAmount(invoice),
            DefaultCurrency);
    }

    private static InvoiceDetail ToInvoiceDetail(Invoice invoice)
    {
        var lines = invoice.Lines
            .OrderBy(line => line.SortOrder)
            .ThenBy(line => line.Description)
            .Select(line => new InvoiceLineDetail(
                line.Id,
                line.Description,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal,
                line.Type.ToString().ToLowerInvariant(),
                line.GigId))
            .ToList();

        return new InvoiceDetail(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.ClientId,
            invoice.Client?.Name ?? string.Empty,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Status.ToString().ToLowerInvariant(),
            invoice.Description,
            invoice.Total,
            CalculateOutstandingAmount(invoice),
            DefaultCurrency,
            lines);
    }

    private static ReceiptSummary ToReceiptSummary(GigExpense expense)
    {
        return new ReceiptSummary(
            expense.Id,
            expense.GigId,
            expense.Gig?.Title ?? string.Empty,
            expense.Gig?.Date ?? default,
            expense.Gig?.ClientId,
            expense.Gig?.Client?.Name,
            expense.Description,
            expense.Amount,
            IsUnmatchedReceipt(expense) ? ReceiptStatusValues.Unmatched : ReceiptStatusValues.Matched,
            expense.Attachments.Count,
            expense.Attachments.Select(attachment => attachment.FileName).Order().ToList(),
            DefaultCurrency);
    }

    private static bool IsUnmatchedReceipt(GigExpense expense)
    {
        return expense.Amount == 0m ||
            expense.Description.Contains("receipt draft", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<GigImportDraftAddResult> AddGigImportDraftCoreAsync(
        Guid userId,
        GigImportDraftAddRequest request,
        int index,
        CancellationToken cancellationToken)
    {
        var validationErrors = new List<string>();
        var contactMatches = new List<ContactMatch>();

        if (request.BatchId == Guid.Empty)
        {
            validationErrors.Add("batchId is required.");
            return new GigImportDraftAddResult(false, false, index, validationErrors, contactMatches, null);
        }

        var batch = await db.GigImportBatches
            .WhereVisibleTo(userId)
            .FirstOrDefaultAsync(value => value.Id == request.BatchId, cancellationToken);

        if (batch is null)
        {
            validationErrors.Add("batch was not found.");
            return new GigImportDraftAddResult(false, false, index, validationErrors, contactMatches, null);
        }

        if (batch.Status != GigImportBatchStatus.Draft)
        {
            validationErrors.Add("drafts can only be added to draft import batches.");
        }

        ValidateDraftRequest(request, validationErrors);

        Guid? proposedClientId = null;
        var proposedClientName = NormalizeForStorage(request.ClientName);
        var contactQuery = NormalizeForStorage(request.ContactQuery);
        if (!string.IsNullOrWhiteSpace(contactQuery))
        {
            var matches = await SearchContactsAsync(userId, contactQuery, cancellationToken);
            contactMatches.AddRange(matches.Matches);
            if (matches.Matches.Count == 1)
            {
                proposedClientId = matches.Matches[0].ContactId;
                proposedClientName ??= matches.Matches[0].Name;
            }
            else if (matches.Matches.Count == 0)
            {
                validationErrors.Add("contactQuery did not match any contacts.");
                proposedClientName ??= contactQuery;
            }
            else
            {
                validationErrors.Add($"contactQuery matched {matches.Matches.Count} contacts.");
                proposedClientName ??= contactQuery;
            }
        }

        if (validationErrors.Count > 0)
        {
            return new GigImportDraftAddResult(true, false, index, validationErrors, contactMatches, null);
        }

        var draft = new GigImportDraft
        {
            Id = Guid.NewGuid(),
            BatchId = request.BatchId,
            ProposedClientId = proposedClientId,
            ProposedClientName = proposedClientName,
            ProposedContactName = NormalizeForStorage(request.ContactName),
            ProposedContactEmail = NormalizeForStorage(request.ContactEmail),
            ProposedProjectName = NormalizeForStorage(request.ProjectName),
            ProposedTitle = NormalizeForStorage(request.Title),
            ProposedDate = request.Date,
            ProposedArrivalTime = request.ArrivalTime,
            ProposedRehearsalStartTime = request.RehearsalStartTime,
            ProposedRehearsalEndTime = request.RehearsalEndTime,
            ProposedShowStartTime = request.ShowStartTime,
            ProposedShowEndTime = request.ShowEndTime,
            ProposedVenueName = NormalizeForStorage(request.VenueName),
            ProposedVenueAddress = NormalizeForStorage(request.VenueAddress),
            ProposedVenuePostcode = NormalizeForStorage(request.Postcode),
            ProposedFee = request.Fee,
            ProposedPerDiem = request.PerDiem,
            ProposedNotes = NormalizeForStorage(request.Notes),
            AccommodationNotes = NormalizeForStorage(request.AccommodationNotes),
            TravelNotes = NormalizeForStorage(request.TravelNotes),
            SourceReference = NormalizeForStorage(request.SourceReference),
            Confidence = request.Confidence ?? GigImportDraftConfidence.Medium,
            WarningsJson = JsonSerializer.Serialize(NormalizeWarnings(request.Warnings), JsonOptions),
            Status = GigImportDraftStatus.Pending,
        };

        db.GigImportDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        return new GigImportDraftAddResult(true, true, index, [], contactMatches, ToGigImportDraftDetail(draft));
    }

    private static void ValidateDraftRequest(GigImportDraftAddRequest request, List<string> validationErrors)
    {
        ValidateLength(request.ClientName, "clientName", 200, validationErrors);
        ValidateLength(request.ContactName, "contactName", 200, validationErrors);
        ValidateLength(request.ContactEmail, "contactEmail", 320, validationErrors);
        ValidateLength(request.ProjectName, "projectName", 200, validationErrors);
        ValidateLength(request.Title, "title", 200, validationErrors);
        ValidateLength(request.VenueName, "venueName", 200, validationErrors);
        ValidateLength(request.VenueAddress, "venueAddress", 1000, validationErrors);
        ValidateLength(request.Postcode, "postcode", 20, validationErrors);
        ValidateLength(request.Notes, "notes", 4000, validationErrors);
        ValidateLength(request.AccommodationNotes, "accommodationNotes", 4000, validationErrors);
        ValidateLength(request.TravelNotes, "travelNotes", 4000, validationErrors);
        ValidateLength(request.SourceReference, "sourceReference", 500, validationErrors);

        if (request.Fee.HasValue && request.Fee.Value < 0)
        {
            validationErrors.Add("fee must be zero or greater.");
        }

        if (request.PerDiem.HasValue && request.PerDiem.Value < 0)
        {
            validationErrors.Add("perDiem must be zero or greater.");
        }
    }

    private static void ValidateLength(string? value, string fieldName, int maxLength, List<string> validationErrors)
    {
        if (NormalizeForStorage(value)?.Length > maxLength)
        {
            validationErrors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    private static IReadOnlyList<string> NormalizeWarnings(IReadOnlyList<string>? warnings)
    {
        return warnings?
            .Select(NormalizeForStorage)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Take(25)
            .ToList() ?? [];
    }

    private static GigImportBatchSummary ToGigImportBatchSummary(GigImportBatch batch, int draftCount)
    {
        return new GigImportBatchSummary(
            batch.Id,
            batch.SourceName,
            batch.SourceFingerprint,
            batch.Status.ToString().ToLowerInvariant(),
            batch.CreatedAtUtc,
            batch.Notes,
            draftCount);
    }

    private static GigImportDraftDetail ToGigImportDraftDetail(GigImportDraft draft)
    {
        return new GigImportDraftDetail(
            draft.Id,
            draft.BatchId,
            draft.ProposedClientId,
            draft.ProposedClientName,
            draft.ProposedContactName,
            draft.ProposedContactEmail,
            draft.ProposedProjectName,
            draft.ProposedTitle,
            draft.ProposedDate,
            draft.ProposedArrivalTime,
            draft.ProposedRehearsalStartTime,
            draft.ProposedRehearsalEndTime,
            draft.ProposedShowStartTime,
            draft.ProposedShowEndTime,
            draft.ProposedVenueName,
            draft.ProposedVenueAddress,
            draft.ProposedVenuePostcode,
            draft.ProposedFee,
            draft.ProposedPerDiem,
            draft.ProposedNotes,
            draft.AccommodationNotes,
            draft.TravelNotes,
            draft.SourceReference,
            draft.Confidence.ToString().ToLowerInvariant(),
            ReadWarnings(draft.WarningsJson),
            draft.Status.ToString().ToLowerInvariant());
    }

    private static IReadOnlyList<string> ReadWarnings(string warningsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(warningsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static decimal CalculateOutstandingAmount(Invoice invoice)
    {
        return invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled ? 0m : invoice.Total;
    }

    private static string? NormalizeForStorage(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string? Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant();
    }
}
