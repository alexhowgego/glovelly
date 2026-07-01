using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public interface IForScoreLibraryImportService
{
    Task<ForScoreLibrarySnapshot> ImportAsync(Guid userId, string originalFileName, Stream stream, CancellationToken cancellationToken = default);
}

public sealed class ForScoreLibraryImportService(
    AppDbContext db,
    IForScoreLibraryParser parser,
    TimeProvider timeProvider) : IForScoreLibraryImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ForScoreLibrarySnapshot> ImportAsync(
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
        return snapshot;
    }
}
