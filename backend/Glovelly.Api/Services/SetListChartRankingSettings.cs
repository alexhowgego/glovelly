namespace Glovelly.Api.Services;

public sealed class SetListChartRankingSettings
{
    public const string SectionName = "SetListChartRanking";

    public string Provider { get; set; } = "Deterministic";

    public string? VertexAiProjectId { get; set; }

    public string? VertexAiLocation { get; set; }

    public string? VertexAiModel { get; set; }

    public bool IsVertexAiConfigured =>
        string.Equals(Provider, "VertexAi", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(VertexAiProjectId)
        && !string.IsNullOrWhiteSpace(VertexAiLocation);
}
