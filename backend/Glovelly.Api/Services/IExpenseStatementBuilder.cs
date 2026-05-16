namespace Glovelly.Api.Services;

public interface IExpenseStatementBuilder
{
    Task<ExpenseStatementProjection> BuildAsync(
        ExpenseStatementRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default);
}

public interface IExpenseStatementPdfRenderer
{
    byte[] Render(ExpenseStatementProjection statement, bool includeReceiptAppendix);
}

public sealed record ExpenseStatementRequest(
    Guid ClientId,
    IReadOnlyList<Guid>? GigIds,
    IReadOnlyList<Guid>? ExpenseIds,
    bool IncludeReceiptAttachments,
    bool IncludeReceiptAppendix,
    bool IncludeReimbursedExpenses = false);

public sealed record ExpenseStatementProjection(
    Guid ClientId,
    string ClientName,
    DateOnly StatementDate,
    IReadOnlyList<ExpenseStatementGig> Gigs,
    decimal Total,
    int ExpenseCount,
    int ReceiptAttachmentCount);

public sealed record ExpenseStatementGig(
    Guid GigId,
    string Title,
    DateOnly Date,
    string Venue,
    bool IsInvoiced,
    IReadOnlyList<ExpenseStatementExpense> Expenses,
    decimal Total);

public sealed record ExpenseStatementExpense(
    Guid ExpenseId,
    string Description,
    decimal Amount,
    int SortOrder,
    IReadOnlyList<ExpenseStatementAttachment> Attachments);

public sealed record ExpenseStatementAttachment(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);

public sealed class ExpenseStatementValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Expense statement request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
