using Glovelly.Api.Models;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace Glovelly.Api.Services;

public sealed class VertexAiSetListChartContextualRanker : ISetListChartContextualRanker
{
    private readonly Func<GenerateContentRequest, CancellationToken, Task<GenerateContentResponse>> _generateContentAsync;
    private readonly SetListChartRankingSettings _settings;
    private readonly DeterministicSetListChartContextualRanker _fallback;
    private readonly ILogger<VertexAiSetListChartContextualRanker> _logger;

    public VertexAiSetListChartContextualRanker(
        IOptions<SetListChartRankingSettings> options,
        DeterministicSetListChartContextualRanker fallback,
        ILogger<VertexAiSetListChartContextualRanker> logger)
        : this(CreateGenerateContentAsync(options.Value), options, fallback, logger)
    {
    }

    public VertexAiSetListChartContextualRanker(
        Func<GenerateContentRequest, CancellationToken, Task<GenerateContentResponse>> generateContentAsync,
        IOptions<SetListChartRankingSettings> options,
        DeterministicSetListChartContextualRanker fallback,
        ILogger<VertexAiSetListChartContextualRanker> logger)
    {
        _generateContentAsync = generateContentAsync;
        _settings = options.Value;
        _fallback = fallback;
        _logger = logger;
    }

    private static Func<GenerateContentRequest, CancellationToken, Task<GenerateContentResponse>> CreateGenerateContentAsync(SetListChartRankingSettings settings)
    {
        var client = new PredictionServiceClientBuilder
        {
            Endpoint = $"{settings.VertexAiLocation ?? "europe-west1"}-aiplatform.googleapis.com",
        }.Build();

        return client.GenerateContentAsync;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task<IReadOnlyList<SetListChartRankingDecision>> RankAsync(
        SetListChartRankingRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(request);
        if (prompt is null)
        {
            _logger.LogInformation("Vertex AI chart ranking skipped: request contains no included song rows. Falling back to deterministic ranking.");
            return await _fallback.RankAsync(request, cancellationToken);
        }

        var songRows = request.CandidateSets.Count(candidateSet => candidateSet.Input.Kind == GigSetListItemKind.Song && candidateSet.Input.Include);
        var candidateCount = request.CandidateSets.Sum(candidateSet => candidateSet.Candidates.Count);

        var generateRequest = new GenerateContentRequest
        {
            Model = $"projects/{_settings.VertexAiProjectId}/locations/{_settings.VertexAiLocation}/publishers/google/models/{_settings.VertexAiModel ?? "gemini-2.5-flash"}",
            SystemInstruction = new Content
            {
                Parts = { new Part { Text = SystemPrompt } }
            },
            Contents =
            {
                new Content
                {
                    Role = "user",
                    Parts = { new Part { Text = prompt } }
                }
            }
        };

        try
        {
            _logger.LogInformation(
                "Calling Vertex AI chart ranker using model {Model} in {Location} for snapshot {SnapshotId}: {SongRowCount} song rows, {CandidateCount} supplied candidates, {PromptLength} prompt characters.",
                _settings.VertexAiModel ?? "gemini-2.5-flash",
                _settings.VertexAiLocation,
                request.SnapshotId,
                songRows,
                candidateCount,
                prompt.Length);

            var stopwatch = Stopwatch.StartNew();
            var response = await _generateContentAsync(generateRequest, cancellationToken);
            stopwatch.Stop();
            var text = response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "Vertex AI chart ranker returned an empty response for snapshot {SnapshotId} after {ElapsedMilliseconds} ms. Falling back to deterministic ranking.",
                    request.SnapshotId,
                    stopwatch.ElapsedMilliseconds);
                return await _fallback.RankAsync(request, cancellationToken);
            }

            var decisions = ParseDecisions(text, request);
            if (decisions is null)
            {
                _logger.LogWarning(
                    "Vertex AI chart ranker response for snapshot {SnapshotId} could not be validated. Falling back to deterministic ranking.",
                    request.SnapshotId);
                return await _fallback.RankAsync(request, cancellationToken);
            }

            _logger.LogInformation(
                "Vertex AI chart ranker returned {DecisionCount} validated decisions for snapshot {SnapshotId} in {ElapsedMilliseconds} ms.",
                decisions.Count,
                request.SnapshotId,
                stopwatch.ElapsedMilliseconds);

            return decisions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Vertex AI chart ranker failed for snapshot {SnapshotId}. Falling back to deterministic ranking.",
                request.SnapshotId);
            return await _fallback.RankAsync(request, cancellationToken);
        }
    }

    private static string? BuildPrompt(SetListChartRankingRequest request)
    {
        var rows = new List<PromptRow>();
        foreach (var candidateSet in request.CandidateSets)
        {
            var item = candidateSet.Input;
            if (item.Kind != GigSetListItemKind.Song || !item.Include)
            {
                continue;
            }

            rows.Add(new PromptRow
            {
                RowNumber = item.SourceRowNumber,
                Title = item.Title,
                PadNumber = item.PadNumber,
                Key = item.Key,
                Candidates = candidateSet.Candidates.Select(c => new PromptCandidate
                {
                    ChartId = c.Chart.Id.ToString(),
                    Title = c.Chart.Title,
                    FilePath = c.Chart.FilePath,
                    Evidence = [.. c.Evidence],
                }).ToList(),
            });
        }

        if (rows.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(new { SetListRows = rows }, JsonOptions);
    }

    private IReadOnlyList<SetListChartRankingDecision>? ParseDecisions(string text, SetListChartRankingRequest request)
    {
        var candidateSetsByRow = request.CandidateSets
            .Where(cs => cs.Input.Kind == GigSetListItemKind.Song && cs.Input.Include)
            .ToDictionary(cs => cs.Input.SourceRowNumber);

        try
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                var start = trimmed.IndexOf('\n', StringComparison.Ordinal) + 1;
                var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (end > start)
                {
                    trimmed = trimmed[start..end].Trim();
                }
            }
            else if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var start = trimmed.IndexOf('\n', StringComparison.Ordinal) + 1;
                var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (end > start)
                {
                    trimmed = trimmed[start..end].Trim();
                }
            }

            var decisions = ParseDecisionList(trimmed);
            if (decisions is null || decisions.Count == 0)
            {
                _logger.LogWarning(
                    "Vertex AI chart ranker response did not contain any decisions for snapshot {SnapshotId}. Response length: {ResponseLength}. First character: {FirstCharacter}.",
                    request.SnapshotId,
                    text.Length,
                    FirstCharacterOrNone(text));
                return null;
            }

            var result = new List<SetListChartRankingDecision>();
            foreach (var decision in decisions)
            {
                if (!candidateSetsByRow.TryGetValue(decision.RowNumber, out var candidateSet))
                {
                    _logger.LogWarning(
                        "Vertex AI chart ranker response referenced unknown row {SourceRowNumber} for snapshot {SnapshotId}.",
                        decision.RowNumber,
                        request.SnapshotId);
                    return null;
                }

                var validIds = candidateSet.Candidates.Select(c => c.Chart.Id).ToHashSet();

                Guid? selectedId = null;
                var invalidSelectedChartId = false;
                if (decision.SelectedChartId is string idStr && Guid.TryParse(idStr, out var parsed))
                {
                    if (!validIds.Contains(parsed))
                    {
                        _logger.LogWarning(
                            "Vertex AI chart ranker response selected unknown chart id for row {SourceRowNumber} in snapshot {SnapshotId}. Marking row for review.",
                            decision.RowNumber,
                            request.SnapshotId);
                        invalidSelectedChartId = true;
                    }
                    else
                    {
                        selectedId = parsed;
                    }
                }
                else if (decision.SelectedChartId is not null)
                {
                    _logger.LogWarning(
                        "Vertex AI chart ranker response selected malformed chart id for row {SourceRowNumber} in snapshot {SnapshotId}. Marking row for review.",
                        decision.RowNumber,
                        request.SnapshotId);
                    invalidSelectedChartId = true;
                }

                var status = invalidSelectedChartId ? ForScoreMappingStatus.NeedsReview : NormalizeStatus(decision.Status);
                var reason = string.IsNullOrWhiteSpace(decision.Reason)
                    ? "Suggested by contextual analysis."
                    : decision.Reason;

                result.Add(new SetListChartRankingDecision(
                    decision.RowNumber,
                    selectedId,
                    status,
                    invalidSelectedChartId ? ForScoreMappingConfidence.Low : NormalizeConfidence(decision.Confidence),
                    invalidSelectedChartId ? "Choose the matching forScore chart." : reason));
            }

            return result;
        }
        catch (JsonException)
        {
            _logger.LogWarning(
                "Vertex AI chart ranker response was not valid JSON for snapshot {SnapshotId}. Response length: {ResponseLength}. First character: {FirstCharacter}.",
                request.SnapshotId,
                text.Length,
                FirstCharacterOrNone(text));
            return null;
        }
    }

    private static List<PromptDecision>? ParseDecisionList(string text)
    {
        var candidates = new List<string> { text };

        var firstArrayStart = text.IndexOf("[", StringComparison.Ordinal);
        var lastArrayEnd = text.LastIndexOf("]", StringComparison.Ordinal);
        if (firstArrayStart >= 0 && lastArrayEnd > firstArrayStart)
        {
            candidates.Add(text[firstArrayStart..(lastArrayEnd + 1)]);
        }

        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(candidate);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return document.RootElement.Deserialize<List<PromptDecision>>(JsonOptions);
                }

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var propertyName in new[] { "decisions", "results", "rankings" })
                {
                    if (document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
                    {
                        return property.Deserialize<List<PromptDecision>>(JsonOptions);
                    }
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return null;
    }

    private static string FirstCharacterOrNone(string text)
    {
        var first = text.FirstOrDefault(value => !char.IsWhiteSpace(value));
        return first == default ? "none" : first.ToString();
    }

    private static ForScoreMappingStatus NormalizeStatus(string? status) => (status?.Trim()?.ToLowerInvariant()) switch
    {
        "suggested" => ForScoreMappingStatus.Suggested,
        "needs_review" or "needsreview" => ForScoreMappingStatus.NeedsReview,
        "missing_from_latest_library" or "missing" => ForScoreMappingStatus.MissingFromLatestLibrary,
        _ => ForScoreMappingStatus.NeedsReview,
    };

    private static ForScoreMappingConfidence NormalizeConfidence(string? confidence) => (confidence?.Trim()?.ToLowerInvariant()) switch
    {
        "high" => ForScoreMappingConfidence.High,
        "medium" => ForScoreMappingConfidence.Medium,
        "low" => ForScoreMappingConfidence.Low,
        _ => ForScoreMappingConfidence.None,
    };

    private const string SystemPrompt =
        """
        You are a forScore chart matching assistant. Given a set list song and candidate charts from the user's library, select the best matching chart.

        Rules:
        1. Chart number evidence outranks title similarity when both are available.
        2. If multiple candidates have the same title, the one matching the row's chart number wins.
        3. Nearby chart numbers (+/-1) are weaker evidence and should not override clear title or context matches.
        4. Return "selectedChartId" as the candidate's chart ID string or null if no candidate is clearly correct.
        5. Return status: "suggested" (confident), "needs_review" (ambiguous or low confidence), or "missing_from_latest_library" (no suitable candidate).
        6. Return confidence: "high", "medium", "low", or "none".
        7. You must copy selectedChartId exactly from one of the supplied candidate chartId values for the same row. Never invent, transform, truncate, or infer IDs.
        8. If no supplied candidate chartId is clearly correct for a row, return selectedChartId as null and status as "needs_review".
        9. Respond with valid JSON only, no other text or markdown formatting.
        """;

    private sealed record PromptRow
    {
        public int RowNumber { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? PadNumber { get; init; }
        public string? Key { get; init; }
        public List<PromptCandidate> Candidates { get; init; } = [];
    }

    private sealed record PromptCandidate
    {
        public string ChartId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public List<string> Evidence { get; init; } = [];
    }

    private sealed record PromptDecision
    {
        public int RowNumber { get; init; }
        public string? SelectedChartId { get; init; }
        public string? Status { get; init; }
        public string? Confidence { get; init; }
        public string? Reason { get; init; }
    }
}
