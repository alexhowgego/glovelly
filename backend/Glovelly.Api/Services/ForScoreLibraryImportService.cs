using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public interface IForScoreLibraryImportService
{
    Task<ForScoreLibraryImportResult> ImportAsync(Guid userId, string originalFileName, Stream stream, CancellationToken cancellationToken = default);
}

public sealed class ForScoreLibraryImportService(
    AppDbContext db,
    IForScoreLibraryParser parser,
    TimeProvider timeProvider) : IForScoreLibraryImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ForScoreLibraryImportResult> ImportAsync(
        Guid userId,
        string originalFileName,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var parseResult = await parser.ParseAsync(stream, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var activeSnapshots = await db.ForScoreLibrarySnapshots
            .Where(snapshot => snapshot.CreatedByUserId == userId && snapshot.IsActive)
            .ToListAsync(cancellationToken);
        var previousActiveSnapshotIds = activeSnapshots.Select(snapshot => snapshot.Id).ToHashSet();
        foreach (var activeSnapshot in activeSnapshots)
        {
            activeSnapshot.IsActive = false;
        }

        var snapshot = new ForScoreLibrarySnapshot
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = userId,
            OriginalFileName = Path.GetFileName(originalFileName),
            SourceFormat = "FourSb",
            BackupVersion = parseResult.BackupVersion,
            IsActive = true,
            ChartCount = parseResult.Charts.Count,
            WarningsJson = JsonSerializer.Serialize(parseResult.Warnings, JsonOptions),
            ImportedAtUtc = now,
            CreatedAtUtc = now,
            Charts = parseResult.Charts.Select((chart, index) => new ForScoreChart
            {
                Id = Guid.NewGuid(),
                SortOrder = index,
                FilePath = chart.FilePath,
                Title = chart.Title,
                NormalizedTitle = chart.NormalizedTitle,
                Keywords = string.IsNullOrWhiteSpace(chart.Keywords) ? null : chart.Keywords,
                AddedAt = chart.AddedAt,
                PrintNumber = chart.PrintNumber,
                Version = chart.Version,
            }).ToList(),
        };

        db.ForScoreLibrarySnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);

        var impact = await AssessSetListImpactAsync(userId, previousActiveSnapshotIds, snapshot, now, cancellationToken);
        return new ForScoreLibraryImportResult(snapshot, impact);
    }

    private async Task<ForScoreLibraryImportImpact> AssessSetListImpactAsync(
        Guid userId,
        IReadOnlySet<Guid> previousActiveSnapshotIds,
        ForScoreLibrarySnapshot newSnapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (previousActiveSnapshotIds.Count == 0)
        {
            return new ForScoreLibraryImportImpact(0, 0, 0, 0, 0, []);
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var imports = await db.GigSetListImports
            .Include(value => value.Gig)
            .Include(value => value.Items)
            .Where(value => value.IsActive
                && value.Gig != null
                && value.Gig.CreatedByUserId == userId
                && (value.Gig.Status == GigStatus.Draft || value.Gig.Status == GigStatus.Confirmed)
                && value.Gig.Date >= today
                && value.Items.Any(item => item.ForScoreLibrarySnapshotId.HasValue
                    && previousActiveSnapshotIds.Contains(item.ForScoreLibrarySnapshotId.Value)))
            .ToListAsync(cancellationToken);

        if (imports.Count == 0)
        {
            return new ForScoreLibraryImportImpact(0, 0, 0, 0, 0, []);
        }

        var chartsByFilePath = newSnapshot.Charts
            .GroupBy(chart => chart.FilePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var checkedItems = 0;
        var autoRelinked = 0;
        var needsReview = 0;
        var impactedSetLists = new List<ForScoreLibraryImportImpactedSetList>();

        foreach (var import in imports)
        {
            var importRelinked = 0;
            var importNeedsReview = 0;
            foreach (var item in import.Items.Where(item => item.Kind == GigSetListItemKind.Song
                && item.Include
                && item.ForScoreLibrarySnapshotId.HasValue
                && previousActiveSnapshotIds.Contains(item.ForScoreLibrarySnapshotId.Value)))
            {
                checkedItems++;
                if (!string.IsNullOrWhiteSpace(item.ForScoreChartFilePath)
                    && chartsByFilePath.TryGetValue(item.ForScoreChartFilePath, out var matches)
                    && matches.Count == 1)
                {
                    var chart = matches[0];
                    item.ForScoreChartId = chart.Id;
                    item.ForScoreLibrarySnapshotId = chart.ForScoreLibrarySnapshotId;
                    item.ForScoreChartTitle = chart.Title;
                    item.ForScoreChartFilePath = chart.FilePath;
                    item.ForScoreMappingStatus = ForScoreMappingStatus.Linked;
                    item.ForScoreMappingConfidence = ForScoreMappingConfidence.High;
                    item.ForScoreMappingUpdatedAtUtc = now;
                    importRelinked++;
                    autoRelinked++;
                }
                else
                {
                    item.ForScoreMappingStatus = ForScoreMappingStatus.MissingFromLatestLibrary;
                    item.ForScoreMappingConfidence = ForScoreMappingConfidence.None;
                    item.ForScoreMappingUpdatedAtUtc = now;
                    importNeedsReview++;
                    needsReview++;
                }
            }

            if (importRelinked > 0 || importNeedsReview > 0)
            {
                impactedSetLists.Add(new ForScoreLibraryImportImpactedSetList(
                    import.GigId,
                    import.Id,
                    import.Gig?.Title ?? "Untitled gig",
                    import.Gig?.Date ?? default,
                    import.Gig?.Status.ToString() ?? string.Empty,
                    importRelinked,
                    importNeedsReview));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ForScoreLibraryImportImpact(imports.Count, impactedSetLists.Count, checkedItems, autoRelinked, needsReview, impactedSetLists);
    }
}
