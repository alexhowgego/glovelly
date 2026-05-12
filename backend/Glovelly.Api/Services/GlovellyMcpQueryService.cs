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
}

public sealed class GlovellyMcpQueryService(AppDbContext db) : IGlovellyMcpQueryService
{
    private const string DefaultCurrency = "GBP";

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

    private static decimal CalculateOutstandingAmount(Invoice invoice)
    {
        return invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled ? 0m : invoice.Total;
    }

    private static string? Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant();
    }
}

public sealed record ContactSearchResult(string Query, IReadOnlyList<ContactMatch> Matches);
public sealed record ContactMatch(Guid ContactId, string Name, string Email);
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

public static class ReceiptStatusValues
{
    public const string Matched = "matched";
    public const string Unmatched = "unmatched";
}

public sealed record BusinessSummaryRequest(DateOnly? FromDate, DateOnly? ToDate);
public sealed record BusinessSummaryResult(DateOnly? FromDate, DateOnly? ToDate, decimal InvoiceTotal, decimal PaidTotal, decimal OutstandingTotal, decimal ExpenseTotal, int ReceiptCount, int UnmatchedReceiptCount, string Currency);
