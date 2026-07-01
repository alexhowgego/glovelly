using System.Text.RegularExpressions;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public interface ISetListChartMatcher
{
    Task<IReadOnlyList<SetListChartMatchResult>> MatchAsync(Guid? userId, IReadOnlyList<SetListChartMatchInput> items, CancellationToken cancellationToken = default);
}

public sealed class SetListChartMatcher(AppDbContext db) : ISetListChartMatcher
{
    private const int MaxCandidates = 5;
    private static readonly Regex NonWordRegex = new("[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<SetListChartMatchResult>> MatchAsync(
        Guid? userId,
        IReadOnlyList<SetListChartMatchInput> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var songInputs = items.Where(item => item.Kind == GigSetListItemKind.Song && item.Include).ToList();
        if (songInputs.Count == 0)
        {
            return items.Select(NotApplicable).ToList();
        }

        if (!userId.HasValue)
        {
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
            return items.Select(item => item.Kind == GigSetListItemKind.Song && item.Include
                ? NoActiveLibrary(item)
                : NotApplicable(item)).ToList();
        }

        var charts = snapshot.Charts.OrderBy(chart => chart.SortOrder).ToList();
        return items.Select(item => Match(item, snapshot.Id, charts)).ToList();
    }

    private static SetListChartMatchResult Match(SetListChartMatchInput item, Guid snapshotId, IReadOnlyList<ForScoreChart> charts)
    {
        if (item.Kind != GigSetListItemKind.Song || !item.Include)
        {
            return NotApplicable(item);
        }

        var normalizedTitle = Normalize(item.Title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return Missing(item, "Add a song title before choosing a chart.");
        }

        var scored = charts
            .Select(chart => Score(chart, normalizedTitle, item.Title))
            .Where(value => value.Score > 0)
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Chart.SortOrder)
            .Take(MaxCandidates)
            .ToList();

        if (scored.Count == 0)
        {
            return Missing(item, "No chart in the latest forScore library looks like this song.");
        }

        var topScore = scored[0].Score;
        var tiedTop = scored.Where(value => value.Score == topScore).ToList();
        var candidates = scored.Select(value => ToCandidate(value.Chart, value.Score, value.Reason)).ToList();

        if (tiedTop.Count == 1 && topScore >= 90)
        {
            return new SetListChartMatchResult(
                item.ItemId,
                item.SourceRowNumber,
                ForScoreMappingStatus.Suggested,
                ConfidenceFor(topScore),
                "Suggested chart from the latest forScore library.",
                ToChart(tiedTop[0].Chart),
                candidates);
        }

        return new SetListChartMatchResult(
            item.ItemId,
            item.SourceRowNumber,
            ForScoreMappingStatus.NeedsReview,
            ConfidenceFor(topScore),
            "Choose the matching forScore chart.",
            null,
            candidates);
    }

    private static (ForScoreChart Chart, int Score, string Reason) Score(ForScoreChart chart, string normalizedTitle, string rawTitle)
    {
        if (string.Equals(chart.NormalizedTitle, normalizedTitle, StringComparison.OrdinalIgnoreCase))
        {
            return (chart, 100, "Exact title match");
        }

        var fileName = Path.GetFileNameWithoutExtension(chart.FilePath);
        var normalizedFileName = Normalize(fileName);
        if (string.Equals(normalizedFileName, normalizedTitle, StringComparison.OrdinalIgnoreCase))
        {
            return (chart, 95, "Exact file name match");
        }

        if (!string.IsNullOrWhiteSpace(chart.Keywords) && Normalize(chart.Keywords).Contains(normalizedTitle, StringComparison.Ordinal))
        {
            return (chart, 80, "Keyword match");
        }

        if (chart.NormalizedTitle.Contains(normalizedTitle, StringComparison.OrdinalIgnoreCase)
            || normalizedTitle.Contains(chart.NormalizedTitle, StringComparison.OrdinalIgnoreCase))
        {
            return (chart, 70, "Similar title match");
        }

        if (normalizedFileName.Contains(normalizedTitle, StringComparison.Ordinal)
            || normalizedTitle.Contains(normalizedFileName, StringComparison.Ordinal))
        {
            return (chart, 65, "Similar file name match");
        }

        return (chart, 0, string.Empty);
    }

    public static string Normalize(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        var normalized = NonWordRegex.Replace(lower, " ").Trim();
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static ForScoreMappingConfidence ConfidenceFor(int score) => score switch
    {
        >= 90 => ForScoreMappingConfidence.High,
        >= 70 => ForScoreMappingConfidence.Medium,
        > 0 => ForScoreMappingConfidence.Low,
        _ => ForScoreMappingConfidence.None,
    };

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

    private static SetListChartMatchCandidate ToCandidate(ForScoreChart chart, int score, string reason) => new(ToChart(chart), score, reason);

    private static ForScoreChartReference ToChart(ForScoreChart chart) => new(
        chart.Id,
        chart.ForScoreLibrarySnapshotId,
        chart.Title,
        chart.FilePath,
        chart.NormalizedTitle);
}

public sealed record SetListChartMatchInput(
    Guid? ItemId,
    int SourceRowNumber,
    GigSetListItemKind Kind,
    bool Include,
    string Title);

public sealed record SetListChartMatchResult(
    Guid? ItemId,
    int SourceRowNumber,
    ForScoreMappingStatus Status,
    ForScoreMappingConfidence Confidence,
    string Reason,
    ForScoreChartReference? SelectedChart,
    IReadOnlyList<SetListChartMatchCandidate> Candidates);

public sealed record SetListChartMatchCandidate(ForScoreChartReference Chart, int Score, string Reason);

public sealed record ForScoreChartReference(
    Guid Id,
    Guid SnapshotId,
    string Title,
    string FilePath,
    string NormalizedTitle);
