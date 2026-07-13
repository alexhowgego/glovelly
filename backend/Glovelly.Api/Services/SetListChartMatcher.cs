using System.Text.RegularExpressions;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Matching;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public interface ISetListChartMatcher
{
    Task<IReadOnlyList<SetListChartMatchResult>> MatchAsync(Guid? userId, IReadOnlyList<SetListChartMatchInput> items, CancellationToken cancellationToken = default, bool useConfiguredRanker = true);
}

public interface ISetListChartContextualRanker
{
    Task<IReadOnlyList<SetListChartRankingDecision>> RankAsync(SetListChartRankingRequest request, CancellationToken cancellationToken = default);
}

public sealed partial class SetListChartMatcher(
    AppDbContext db,
    ISetListChartContextualRanker contextualRanker,
    DeterministicSetListChartContextualRanker deterministicRanker,
    ILogger<SetListChartMatcher> logger) : ISetListChartMatcher
{
    public async Task<IReadOnlyList<SetListChartMatchResult>> MatchAsync(
        Guid? userId,
        IReadOnlyList<SetListChartMatchInput> items,
        CancellationToken cancellationToken = default,
        bool useConfiguredRanker = true)
    {
        if (items.Count == 0)
        {
            logger.LogDebug("Skipping set list chart matching because there are no rows.");
            return [];
        }

        var songInputs = items.Where(item => item.Kind == GigSetListItemKind.Song && item.Include).ToList();
        if (songInputs.Count == 0)
        {
            logger.LogInformation("Set list chart matching skipped: {RowCount} rows supplied but no included song rows.", items.Count);
            return items.Select(NotApplicable).ToList();
        }

        if (!userId.HasValue)
        {
            logger.LogInformation("Set list chart matching skipped: no authenticated user id for {SongCount} included song rows.", songInputs.Count);
            return items.Select(item => item.Kind == GigSetListItemKind.Song && item.Include
                ? NoActiveLibrary(item)
                : NotApplicable(item)).ToList();
        }

        var snapshot = await db.ForScoreLibrarySnapshots
            .AsNoTracking()
            .Include(value => value.Charts)
            .Where(value => value.CreatedByUserId == userId && value.IsActive)
            .OrderByDescending(value => value.ImportedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            logger.LogInformation("Set list chart matching skipped: no active forScore library snapshot for {SongCount} included song rows.", songInputs.Count);
            return items.Select(item => item.Kind == GigSetListItemKind.Song && item.Include
                ? NoActiveLibrary(item)
                : NotApplicable(item)).ToList();
        }

        var charts = snapshot.Charts.OrderBy(chart => chart.SortOrder).ToList();
        var candidateSets = items.Select(item => RetrieveCandidates(item, charts)).ToList();
        var candidateCount = candidateSets.Sum(candidateSet => candidateSet.Candidates.Count);
        var rowsWithCandidates = candidateSets.Count(candidateSet => candidateSet.Candidates.Count > 0);
        logger.LogInformation(
            "Set list chart matching retrieved {CandidateCount} candidates across {RowsWithCandidates}/{SongCount} included song rows from snapshot {SnapshotId} containing {ChartCount} charts.",
            candidateCount,
            rowsWithCandidates,
            songInputs.Count,
            snapshot.Id,
            charts.Count);

        var ranker = useConfiguredRanker ? contextualRanker : deterministicRanker;
        logger.LogInformation(
            "Set list chart matching ranker selected for snapshot {SnapshotId}: {RankerMode}.",
            snapshot.Id,
            useConfiguredRanker ? "configured" : "deterministic");
        var decisions = await ranker.RankAsync(new SetListChartRankingRequest(snapshot.Id, items, candidateSets), cancellationToken);
        var decisionsByRow = decisions.ToDictionary(decision => decision.SourceRowNumber);

        var results = candidateSets.Select(candidateSet => ToResult(candidateSet, decisionsByRow.GetValueOrDefault(candidateSet.Input.SourceRowNumber))).ToList();
        var suggested = results.Count(result => result.Status == ForScoreMappingStatus.Suggested);
        var needsReview = results.Count(result => result.Status == ForScoreMappingStatus.NeedsReview);
        var missing = results.Count(result => result.Status == ForScoreMappingStatus.MissingFromLatestLibrary);
        logger.LogInformation(
            "Set list chart matching completed for snapshot {SnapshotId}: {SuggestedCount} suggested, {NeedsReviewCount} needs review, {MissingCount} missing from latest library.",
            snapshot.Id,
            suggested,
            needsReview,
            missing);

        return results;
    }

    private static SetListChartCandidateSet RetrieveCandidates(SetListChartMatchInput item, IReadOnlyList<ForScoreChart> charts)
    {
        if (item.Kind != GigSetListItemKind.Song || !item.Include)
        {
            return new SetListChartCandidateSet(item, []);
        }

        var inputText = MatchTextNormalizer.Normalize(item.Title);
        if (string.IsNullOrWhiteSpace(inputText.Compact))
        {
            return new SetListChartCandidateSet(item, []);
        }

        var inputNumbers = ExtractNumbers(item.PadNumber, item.Title, item.Key).ToHashSet();
        var byChart = new Dictionary<Guid, CandidateBuilder>();

        foreach (var chart in charts)
        {
            var builder = new CandidateBuilder(chart);
            AddChartNumberEvidence(builder, chart, inputNumbers);
            AddTitleEvidence(builder, chart, inputText);
            AddKeywordEvidence(builder, chart, inputText);
            if (builder.Score > 0)
            {
                byChart[chart.Id] = builder;
            }
        }

        var candidates = byChart.Values
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Chart.SortOrder)
            .Take(15)
            .Select(value => value.Build())
            .ToList();

        return new SetListChartCandidateSet(item, candidates);
    }

    private static void AddChartNumberEvidence(CandidateBuilder builder, ForScoreChart chart, IReadOnlySet<int> inputNumbers)
    {
        if (inputNumbers.Count == 0)
        {
            return;
        }

        var chartNumbers = ExtractNumbers(chart.PrintNumber?.ToString(), chart.FilePath, chart.Title, chart.Keywords).ToHashSet();
        foreach (var inputNumber in inputNumbers)
        {
            if (chartNumbers.Contains(inputNumber))
            {
                builder.Add(120, "exact_chart_number", $"Chart number {inputNumber} matches.");
            }
            else if (chartNumbers.Contains(inputNumber - 1) || chartNumbers.Contains(inputNumber + 1))
            {
                builder.Add(35, "nearby_chart_number", $"Nearby chart number for {inputNumber}.");
            }
        }
    }

    private static void AddTitleEvidence(CandidateBuilder builder, ForScoreChart chart, MatchText inputText)
    {
        var titleText = MatchTextNormalizer.Normalize(chart.Title);
        var fileNameText = MatchTextNormalizer.Normalize(Path.GetFileNameWithoutExtension(chart.FilePath));
        AddTextEvidence(builder, StringSimilarity.Compare(inputText, titleText), "title", chart.Title);
        AddTextEvidence(builder, StringSimilarity.Compare(inputText, fileNameText), "file_name", Path.GetFileNameWithoutExtension(chart.FilePath));

        if (!string.IsNullOrWhiteSpace(fileNameText.Compact)
            && (fileNameText.Compact.Contains(inputText.Compact, StringComparison.Ordinal)
                || inputText.Compact.Contains(fileNameText.Compact, StringComparison.Ordinal)))
        {
            builder.Add(60, "file_path_title_contains", "File name contains the set list title.");
        }
    }

    private static void AddKeywordEvidence(CandidateBuilder builder, ForScoreChart chart, MatchText inputText)
    {
        if (string.IsNullOrWhiteSpace(chart.Keywords))
        {
            return;
        }

        var keywordText = MatchTextNormalizer.Normalize(chart.Keywords);
        var score = StringSimilarity.Compare(inputText, keywordText);
        if (score.TokenOverlapScore >= 0.6 || keywordText.Compact.Contains(inputText.Compact, StringComparison.Ordinal))
        {
            builder.Add(50, "keyword_title_similarity", "Keywords overlap with the set list title.");
        }
    }

    private static void AddTextEvidence(CandidateBuilder builder, StringSimilarityScore score, string source, string comparedValue)
    {
        if (score.CompactScore >= 1)
        {
            builder.Add(100, $"compact_{source}_match", $"Compact {source.Replace('_', ' ')} matches '{comparedValue}'.");
            return;
        }

        if (score.TokenOverlapScore >= 0.85)
        {
            builder.Add(80, $"token_{source}_similarity", $"{source.Replace('_', ' ')} has strong token overlap.");
        }
        else if (score.EditDistanceScore >= 0.82)
        {
            builder.Add(70, $"edit_{source}_similarity", $"{source.Replace('_', ' ')} is textually similar.");
        }
        else if (score.BestScore >= 0.6)
        {
            builder.Add(40, $"partial_{source}_similarity", $"{source.Replace('_', ' ')} is plausibly related.");
        }
    }

    private static SetListChartMatchResult ToResult(SetListChartCandidateSet candidateSet, SetListChartRankingDecision? decision)
    {
        var item = candidateSet.Input;
        if (item.Kind != GigSetListItemKind.Song || !item.Include)
        {
            return NotApplicable(item);
        }

        if (candidateSet.Candidates.Count == 0)
        {
            return Missing(item, string.IsNullOrWhiteSpace(item.Title)
                ? "Add a song title before choosing a chart."
                : "No chart in the latest forScore library looks like this song.");
        }

        var candidateById = candidateSet.Candidates.ToDictionary(candidate => candidate.Chart.Id);
        if (decision?.SelectedChartId is Guid invalidSelectedId && !candidateById.ContainsKey(invalidSelectedId))
        {
            return new SetListChartMatchResult(
                item.ItemId,
                item.SourceRowNumber,
                ForScoreMappingStatus.NeedsReview,
                ForScoreMappingConfidence.Low,
                "Choose the matching forScore chart.",
                null,
                candidateSet.Candidates);
        }

        var selected = decision?.SelectedChartId is Guid selectedId && candidateById.TryGetValue(selectedId, out var selectedCandidate)
            ? selectedCandidate.Chart
            : null;

        return new SetListChartMatchResult(
            item.ItemId,
            item.SourceRowNumber,
            decision?.Status ?? ForScoreMappingStatus.NeedsReview,
            decision?.Confidence ?? ForScoreMappingConfidence.Low,
            decision?.Reason ?? "Choose the matching forScore chart.",
            selected,
            candidateSet.Candidates);
    }

    private static IEnumerable<int> ExtractNumbers(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (Match match in ChartNumberRegex().Matches(value))
            {
                if (int.TryParse(match.Groups[1].Value, out var number))
                {
                    yield return number;
                }
            }
        }
    }

    private static SetListChartMatchResult NotApplicable(SetListChartMatchInput item) => new(
        item.ItemId,
        item.SourceRowNumber,
        ForScoreMappingStatus.NotApplicable,
        ForScoreMappingConfidence.None,
        "Only included song rows can be linked to forScore charts.",
        null,
        []);

    private static SetListChartMatchResult NoActiveLibrary(SetListChartMatchInput item) => new(
        item.ItemId,
        item.SourceRowNumber,
        ForScoreMappingStatus.NoActiveLibrary,
        ForScoreMappingConfidence.None,
        "Import a forScore library snapshot to match charts.",
        null,
        []);

    private static SetListChartMatchResult Missing(SetListChartMatchInput item, string reason) => new(
        item.ItemId,
        item.SourceRowNumber,
        ForScoreMappingStatus.MissingFromLatestLibrary,
        ForScoreMappingConfidence.None,
        reason,
        null,
        []);

    private static ForScoreChartReference ToChart(ForScoreChart chart) => new(
        chart.Id,
        chart.ForScoreLibrarySnapshotId,
        chart.Title,
        chart.FilePath,
        chart.NormalizedTitle);

    [GeneratedRegex(@"(?<!\d)(\d{1,4})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex ChartNumberRegex();

    private sealed class CandidateBuilder(ForScoreChart chart)
    {
        private readonly List<string> _evidence = [];
        private readonly List<string> _reasons = [];

        public ForScoreChart Chart { get; } = chart;

        public int Score { get; private set; }

        public void Add(int score, string evidence, string reason)
        {
            Score = Math.Max(Score, score);
            if (!_evidence.Contains(evidence, StringComparer.Ordinal))
            {
                _evidence.Add(evidence);
            }

            if (!_reasons.Contains(reason, StringComparer.Ordinal))
            {
                _reasons.Add(reason);
            }
        }

        public SetListChartMatchCandidate Build() => new(
            ToChart(Chart),
            Score,
            _reasons.FirstOrDefault() ?? "Plausible chart candidate.",
            _evidence);
    }
}

public sealed class DeterministicSetListChartContextualRanker : ISetListChartContextualRanker
{
    public Task<IReadOnlyList<SetListChartRankingDecision>> RankAsync(SetListChartRankingRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SetListChartRankingDecision>>(request.CandidateSets.Select(Rank).ToList());
    }

    private static SetListChartRankingDecision Rank(SetListChartCandidateSet candidateSet)
    {
        var item = candidateSet.Input;
        if (item.Kind != GigSetListItemKind.Song || !item.Include)
        {
            return new SetListChartRankingDecision(item.SourceRowNumber, null, ForScoreMappingStatus.NotApplicable, ForScoreMappingConfidence.None, "Only included song rows can be linked to forScore charts.");
        }

        if (candidateSet.Candidates.Count == 0)
        {
            return new SetListChartRankingDecision(item.SourceRowNumber, null, ForScoreMappingStatus.MissingFromLatestLibrary, ForScoreMappingConfidence.None, "No chart in the latest forScore library looks like this song.");
        }

        var exactNumberCandidates = candidateSet.Candidates
            .Where(candidate => candidate.Evidence.Contains("exact_chart_number", StringComparer.Ordinal))
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
        if (exactNumberCandidates.Count == 1)
        {
            return Suggested(item, exactNumberCandidates[0], "Suggested by chart number.");
        }

        var topScore = candidateSet.Candidates[0].Score;
        var topCandidates = candidateSet.Candidates.Where(candidate => candidate.Score == topScore).ToList();
        if (topCandidates.Count == 1 && topScore >= 95)
        {
            return Suggested(item, topCandidates[0], "Suggested by title similarity.");
        }

        return new SetListChartRankingDecision(
            item.SourceRowNumber,
            null,
            ForScoreMappingStatus.NeedsReview,
            ConfidenceFor(topScore),
            "Choose the matching forScore chart.");
    }

    private static SetListChartRankingDecision Suggested(SetListChartMatchInput item, SetListChartMatchCandidate candidate, string reason) => new(
        item.SourceRowNumber,
        candidate.Chart.Id,
        ForScoreMappingStatus.Suggested,
        ConfidenceFor(candidate.Score),
        reason);

    private static ForScoreMappingConfidence ConfidenceFor(int score) => score switch
    {
        >= 95 => ForScoreMappingConfidence.High,
        >= 70 => ForScoreMappingConfidence.Medium,
        > 0 => ForScoreMappingConfidence.Low,
        _ => ForScoreMappingConfidence.None,
    };
}

public sealed record SetListChartMatchInput(
    Guid? ItemId,
    int SourceRowNumber,
    GigSetListItemKind Kind,
    bool Include,
    string Title,
    string? PadNumber = null,
    string? Key = null);

public sealed record SetListChartCandidateSet(
    SetListChartMatchInput Input,
    IReadOnlyList<SetListChartMatchCandidate> Candidates);

public sealed record SetListChartRankingRequest(
    Guid SnapshotId,
    IReadOnlyList<SetListChartMatchInput> Items,
    IReadOnlyList<SetListChartCandidateSet> CandidateSets);

public sealed record SetListChartRankingDecision(
    int SourceRowNumber,
    Guid? SelectedChartId,
    ForScoreMappingStatus Status,
    ForScoreMappingConfidence Confidence,
    string Reason);

public sealed record SetListChartMatchResult(
    Guid? ItemId,
    int SourceRowNumber,
    ForScoreMappingStatus Status,
    ForScoreMappingConfidence Confidence,
    string Reason,
    ForScoreChartReference? SelectedChart,
    IReadOnlyList<SetListChartMatchCandidate> Candidates);

public sealed record SetListChartMatchCandidate(
    ForScoreChartReference Chart,
    int Score,
    string Reason,
    IReadOnlyList<string> Evidence);

public sealed record ForScoreChartReference(
    Guid Id,
    Guid SnapshotId,
    string Title,
    string FilePath,
    string NormalizedTitle);
