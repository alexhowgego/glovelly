namespace Glovelly.Api.Services;

public sealed class SetListInterpretationSettings
{
    public const string SectionName = "SetListInterpretation";
    public string? VertexAiProjectId { get; set; }
    public string? VertexAiLocation { get; set; } = "eu";
    public string? VertexAiModel { get; set; } = "gemini-3.1-flash-lite";
    public int MaxRows { get; set; } = 250;
    public int MaxColumns { get; set; } = 40;
    public int MaxSourcePayloadBytes { get; set; } = 200_000;
    public int SourceGridRetentionDays { get; set; } = 7;
    public bool IsVertexAiConfigured => !string.IsNullOrWhiteSpace(VertexAiProjectId) && !string.IsNullOrWhiteSpace(VertexAiLocation) && !string.IsNullOrWhiteSpace(VertexAiModel);
}
