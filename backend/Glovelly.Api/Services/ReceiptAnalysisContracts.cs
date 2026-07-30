using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed record ReceiptAnalysisTarget(Guid GigId, Guid ExpenseId, Guid AttachmentId);

public sealed record ReceiptAnalysisField<T>(T Value, ReceiptAnalysisConfidence Confidence);

public sealed record ReceiptAnalysisResult(
    Guid Id,
    ReceiptAnalysisStatus Status,
    string Provider,
    string Model,
    string PromptVersion,
    DateTimeOffset RequestedAt,
    DateTimeOffset CompletedAt,
    ReceiptAnalysisField<string?> Merchant,
    ReceiptAnalysisField<DateOnly?> TransactionDate,
    ReceiptAnalysisField<decimal?> TotalAmount,
    ReceiptAnalysisField<string?> Currency,
    ReceiptAnalysisField<string?> SuggestedCategory,
    IReadOnlyList<string> Warnings,
    string? FailureCode,
    string? FailureMessage);

public interface IReceiptAnalysisService
{
    Task<ReceiptAnalysisResult> AnalyzeAsync(ExpenseAttachment attachment, CancellationToken cancellationToken = default);
}
