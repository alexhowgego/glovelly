using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed record ContactSearchResult(string Query, IReadOnlyList<ContactMatch> Matches);
public sealed record ContactMatch(Guid ContactId, string Name, string Email);
public sealed record ContactGetResult(bool Found, ContactDetail? Contact);
public sealed record ContactDetail(
    Guid ContactId,
    string Name,
    string Email,
    AddressDetail BillingAddress,
    decimal? MileageRate,
    decimal? PassengerMileageRate,
    string? InvoiceFilenamePattern,
    string? InvoiceEmailSubjectPattern,
    int GigCount,
    int InvoiceCount);
public sealed record AddressDetail(string Line1, string? Line2, string City, string? StateOrCounty, string PostalCode, string Country);
public sealed record InvoiceListRequest(Guid? ContactId, string? ContactQuery, string? Status, DateOnly? FromDate, DateOnly? ToDate, InvoiceDateBasis? DateBasis);

public enum InvoiceDateBasis
{
    IssueDate,
    DueDate,
}

public sealed record InvoiceListResult(bool Ambiguous, string? Message, IReadOnlyList<ContactMatch> Matches, IReadOnlyList<InvoiceSummary> Invoices, decimal TotalOutstanding, string Currency)
{
    public static InvoiceListResult Success(IReadOnlyList<InvoiceSummary> invoices, decimal totalOutstanding, string currency)
    {
        return new InvoiceListResult(false, null, [], invoices, totalOutstanding, currency);
    }

    public static InvoiceListResult WithAmbiguity(string message, IReadOnlyList<ContactMatch> matches)
    {
        return new InvoiceListResult(true, message, matches, [], 0m, "GBP");
    }
}

public sealed record InvoiceSummary(Guid InvoiceId, string InvoiceNumber, Guid ContactId, string ContactName, DateOnly IssueDate, DateOnly DueDate, string Status, decimal Total, decimal OutstandingAmount, string Currency);
public sealed record InvoiceDetail(Guid InvoiceId, string InvoiceNumber, Guid ContactId, string ContactName, DateOnly IssueDate, DateOnly DueDate, string Status, string? Description, decimal Total, decimal OutstandingAmount, string Currency, IReadOnlyList<InvoiceLineDetail> Lines);
public sealed record InvoiceLineDetail(Guid InvoiceLineId, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal, string Type, Guid? GigId);
public sealed record ReceiptListRequest(DateOnly? FromDate, DateOnly? ToDate, string? Status);
public sealed record ReceiptListResult(IReadOnlyList<ReceiptSummary> Receipts, int ReceiptCount, int UnmatchedReceiptCount, decimal TotalAmount, string Currency);
public sealed record ReceiptSummary(Guid ReceiptId, Guid GigId, string GigTitle, DateOnly ReceiptDate, Guid? ContactId, string? ContactName, string Description, decimal Amount, string Status, int AttachmentCount, IReadOnlyList<string> AttachmentFileNames, string Currency);
public sealed record GigListRequest(Guid? ContactId, string? ContactQuery, string? Status, DateOnly? FromDate, DateOnly? ToDate, string? InvoicingState);
public sealed record GigListResult(bool Ambiguous, string? Message, IReadOnlyList<ContactMatch> Matches, IReadOnlyList<GigSummary> Gigs, decimal TotalFees, string Currency)
{
    public static GigListResult Success(IReadOnlyList<GigSummary> gigs, decimal totalFees, string currency)
    {
        return new GigListResult(false, null, [], gigs, totalFees, currency);
    }

    public static GigListResult WithAmbiguity(string message, IReadOnlyList<ContactMatch> matches)
    {
        return new GigListResult(true, message, matches, [], 0m, "GBP");
    }
}
public sealed record GigGetResult(bool Found, GigDetail? Gig);
public sealed record GigSummary(Guid GigId, string Title, DateOnly Date, string Venue, Guid ContactId, string ContactName, string Status, decimal Fee, bool IsInvoiced, Guid? InvoiceId, string Currency);
public sealed record GigDetail(
    Guid GigId,
    string Title,
    DateOnly Date,
    string Venue,
    Guid ContactId,
    string ContactName,
    string Status,
    decimal Fee,
    decimal TravelMiles,
    int? PassengerCount,
    bool WasDriving,
    string? Notes,
    bool IsInvoiced,
    InvoiceSummary? Invoice,
    IReadOnlyList<GigExpenseSummary> Expenses,
    IReadOnlyList<GigResourceSummary> Resources,
    string Currency);
public sealed record GigExpenseSummary(Guid ExpenseId, string Description, decimal Amount, int SortOrder, string ReimbursementStatus, int AttachmentCount, IReadOnlyList<string> AttachmentFileNames, string Currency);
public sealed record GigResourceListResult(bool Found, IReadOnlyList<GigResourceSummary> Resources);
public sealed record GigResourceSummary(Guid ResourceId, string ResourceType, string Purpose, string Title, string? Url, string? Notes, bool IsPrimary, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<GigResourceAttachmentSummary> Attachments);
public sealed record GigResourceAttachmentSummary(Guid AttachmentId, string FileName, string ContentType, long SizeBytes, DateTimeOffset CreatedAt);
public sealed record GigSetlistGetResult(bool Found, bool HasActiveSetlist, GigSetlistDetail? Setlist);
public sealed record GigSetlistDetail(Guid ImportId, Guid GigId, Guid? ResourceId, string SpreadsheetId, string? WorksheetId, string WorksheetName, string? SourceUrl, DateTimeOffset ImportedAtUtc, IReadOnlyList<GigSetlistItemDetail> Items);
public sealed record GigSetlistItemDetail(int SortOrder, int SourceRowNumber, string Kind, bool Include, string? Section, string? PadNumber, string? Key, string Title, string? Notes, string Confidence);
public sealed record ExpenseStatementPreviewRequest(Guid ContactId, IReadOnlyList<Guid>? GigIds, IReadOnlyList<Guid>? ExpenseIds, bool IncludeReceiptAttachments, bool IncludeReceiptAppendix, bool IncludeReimbursedExpenses = false);
public sealed record ExpenseStatementPreviewResult(bool Created, IReadOnlyDictionary<string, string[]> ValidationErrors, ExpenseStatementPreview? Statement);
public sealed record ExpenseStatementPreview(Guid ContactId, string ContactName, DateOnly StatementDate, IReadOnlyList<ExpenseStatementGigPreview> Gigs, decimal Total, int ExpenseCount, int ReceiptAttachmentCount, string Currency);
public sealed record ExpenseStatementGigPreview(Guid GigId, string Title, DateOnly Date, string Venue, bool IsInvoiced, IReadOnlyList<ExpenseStatementExpensePreview> Expenses, decimal Total, string Currency);
public sealed record ExpenseStatementExpensePreview(Guid ExpenseId, string Description, decimal Amount, int SortOrder, IReadOnlyList<GigResourceAttachmentSummary> Attachments, string Currency);

public static class ReceiptStatusValues
{
    public const string Matched = "matched";
    public const string Unmatched = "unmatched";
}

public sealed record BusinessSummaryRequest(DateOnly? FromDate, DateOnly? ToDate);
public sealed record BusinessSummaryResult(DateOnly? FromDate, DateOnly? ToDate, decimal InvoiceTotal, decimal PaidTotal, decimal OutstandingTotal, decimal ExpenseTotal, int ReceiptCount, int UnmatchedReceiptCount, string Currency);
public sealed record GigImportBatchCreateRequest(string? SourceName, string? Notes, string? SourceFingerprint);
public sealed record GigImportBatchCreateResult(bool Created, IReadOnlyList<string> ValidationErrors, GigImportBatchSummary? Batch);
public sealed record GigImportBatchListResult(IReadOnlyList<GigImportBatchSummary> Batches);
public sealed record GigImportBatchGetResult(bool Found, GigImportBatchDetail? Batch);
public sealed record GigImportBatchSummary(Guid BatchId, string SourceName, string? SourceFingerprint, string Status, DateTimeOffset CreatedAtUtc, string? Notes, int DraftCount);
public sealed record GigImportBatchDetail(Guid BatchId, string SourceName, string? SourceFingerprint, string Status, DateTimeOffset CreatedAtUtc, string? Notes, int DraftCount, IReadOnlyList<GigImportDraftDetail> Drafts);
public sealed record GigImportDraftBulkAddRequest(Guid BatchId, IReadOnlyList<GigImportDraftAddRequest>? Drafts);
public sealed record GigImportDraftBulkAddResult(bool BatchFound, int SubmittedCount, int CreatedCount, IReadOnlyList<GigImportDraftAddResult> Results);
public sealed record GigImportDraftAddRequest(
    Guid BatchId,
    string? Title,
    string? ClientName,
    string? ContactQuery,
    string? ContactName,
    string? ContactEmail,
    string? ProjectName,
    DateOnly? Date,
    TimeOnly? ArrivalTime,
    TimeOnly? RehearsalStartTime,
    TimeOnly? RehearsalEndTime,
    TimeOnly? ShowStartTime,
    TimeOnly? ShowEndTime,
    string? VenueName,
    string? VenueAddress,
    string? Postcode,
    decimal? Fee,
    decimal? PerDiem,
    string? Notes,
    string? AccommodationNotes,
    string? TravelNotes,
    string? SourceReference,
    GigImportDraftConfidence? Confidence,
    IReadOnlyList<string>? Warnings);
public sealed record GigImportDraftAddResult(bool BatchFound, bool Created, int Index, IReadOnlyList<string> ValidationErrors, IReadOnlyList<ContactMatch> ContactMatches, GigImportDraftDetail? Draft);
public sealed record GigImportDraftDetail(
    Guid DraftId,
    Guid BatchId,
    Guid? ProposedClientId,
    string? ClientName,
    string? ContactName,
    string? ContactEmail,
    string? ProjectName,
    string? Title,
    DateOnly? Date,
    TimeOnly? ArrivalTime,
    TimeOnly? RehearsalStartTime,
    TimeOnly? RehearsalEndTime,
    TimeOnly? ShowStartTime,
    TimeOnly? ShowEndTime,
    string? VenueName,
    string? VenueAddress,
    string? Postcode,
    decimal? Fee,
    decimal? PerDiem,
    string? Notes,
    string? AccommodationNotes,
    string? TravelNotes,
    string? SourceReference,
    string Confidence,
    IReadOnlyList<string> Warnings,
    string Status);
