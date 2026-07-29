using System.Text.Json.Serialization;

namespace Glovelly.Api.Models;

public enum ReceiptAnalysisStatus
{
    Succeeded,
    Failed,
}

public enum ReceiptAnalysisConfidence
{
    None,
    Low,
    Medium,
    High,
}

public sealed class ReceiptAnalysis
{
    public Guid Id { get; set; }
    public Guid ExpenseAttachmentId { get; set; }
    public ReceiptAnalysisStatus Status { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? Merchant { get; set; }
    public DateOnly? TransactionDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Currency { get; set; }
    public string? SuggestedCategory { get; set; }
    public ReceiptAnalysisConfidence MerchantConfidence { get; set; }
    public ReceiptAnalysisConfidence TransactionDateConfidence { get; set; }
    public ReceiptAnalysisConfidence TotalAmountConfidence { get; set; }
    public ReceiptAnalysisConfidence CurrencyConfidence { get; set; }
    public ReceiptAnalysisConfidence SuggestedCategoryConfidence { get; set; }
    public List<string> Warnings { get; set; } = [];
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }

    [JsonIgnore]
    public ExpenseAttachment? ExpenseAttachment { get; set; }
}
