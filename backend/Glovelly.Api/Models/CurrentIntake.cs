namespace Glovelly.Api.Models;

public enum IntakeSourceType { File, Url, Text }
public enum IntakeAnalysisState { Pending, Succeeded, Failed }
public enum IntakeIntent { Unknown, Receipt, Resource }
public enum AutomaticReceiptMatching { ManualOnly, VeryHighConfidence, HighConfidence, MediumConfidence }

public sealed class CurrentIntake
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public IntakeSourceType SourceType { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? StorageKey { get; set; }
    public string? Url { get; set; }
    public string? Text { get; set; }
    public IntakeAnalysisState AnalysisState { get; set; }
    public IntakeIntent Intent { get; set; }
    public ReceiptAnalysisConfidence Confidence { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public User? User { get; set; }
    public List<IntakeAnalysisAttempt> AnalysisAttempts { get; set; } = [];
}

public sealed class IntakeAnalysisAttempt
{
    public Guid Id { get; set; }
    public Guid CurrentIntakeId { get; set; }
    public IntakeAnalysisState State { get; set; }
    public IntakeIntent Intent { get; set; }
    public ReceiptAnalysisConfidence Confidence { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public CurrentIntake? CurrentIntake { get; set; }
}
