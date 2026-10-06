using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

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
    string? FailureMessage)
{
    public static ReceiptAnalysisResult FromAnalysis(ReceiptAnalysis value) => new(
        value.Id, value.Status, value.Provider, value.Model, value.PromptVersion, value.RequestedAt, value.CompletedAt,
        new ReceiptAnalysisField<string?>(value.Merchant, value.MerchantConfidence),
        new ReceiptAnalysisField<DateOnly?>(value.TransactionDate, value.TransactionDateConfidence),
        new ReceiptAnalysisField<decimal?>(value.TotalAmount, value.TotalAmountConfidence),
        new ReceiptAnalysisField<string?>(value.Currency, value.CurrencyConfidence),
        new ReceiptAnalysisField<string?>(value.SuggestedCategory, value.SuggestedCategoryConfidence),
        value.Warnings, value.FailureCode, value.FailureMessage);
}

public interface IReceiptAnalysisService
{
    Task<ReceiptAnalysisResult> AnalyzeAsync(ExpenseAttachment attachment, CancellationToken cancellationToken = default, Guid? userId = null);
}
