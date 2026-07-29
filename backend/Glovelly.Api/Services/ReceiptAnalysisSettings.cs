namespace Glovelly.Api.Services;

public sealed class ReceiptAnalysisSettings
{
    public const string SectionName = "ReceiptAnalysis";

    public bool Enabled { get; set; }
    public string? VertexAiProjectId { get; set; }
    public string? VertexAiLocation { get; set; } = "eu";
    public string? VertexAiModel { get; set; } = "gemini-3.1-flash-lite";
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp", "application/pdf"];
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
    public int PerUserPermitLimit { get; set; } = 10;
    public TimeSpan PerUserWindow { get; set; } = TimeSpan.FromHours(1);

    public bool IsConfigured => Enabled
        && !string.IsNullOrWhiteSpace(VertexAiProjectId)
        && !string.IsNullOrWhiteSpace(VertexAiLocation)
        && !string.IsNullOrWhiteSpace(VertexAiModel);
}
