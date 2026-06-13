using System.Text;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class GigImportDuplicateDetectionService(AppDbContext db) : IGigImportDuplicateDetectionService
{
    public const string GeneratedWarningPrefix = "Possible duplicate:";

    private delegate string? GigDuplicateRule(GigImportDraft draft, GigCandidate gig);

    private static readonly IReadOnlyList<GigDuplicateRule> GigRules =
    [
        SameDateAndVenue,
        SameClientAndDate,
        SameDateAndSimilarTitle,
    ];

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> FindWarningsAsync(
        Guid? userId,
        GigImportBatch batch,
        IReadOnlyList<GigImportDraft> drafts,
        CancellationToken cancellationToken = default)
    {
        var warnings = drafts.ToDictionary(draft => draft.Id, _ => new List<string>());
        var reviewDrafts = drafts
            .Where(draft => draft.Status != GigImportDraftStatus.Committed)
            .ToList();
        if (reviewDrafts.Count == 0)
        {
            return ToResult(warnings);
        }

        await AddExistingGigWarningsAsync(userId, reviewDrafts, warnings, cancellationToken);
        AddInBatchWarnings(reviewDrafts, warnings);
        await AddSourceFingerprintWarningsAsync(userId, batch, reviewDrafts, warnings, cancellationToken);

        return ToResult(warnings);
    }

    private async Task AddExistingGigWarningsAsync(
        Guid? userId,
        IReadOnlyList<GigImportDraft> drafts,
        Dictionary<Guid, List<string>> warnings,
        CancellationToken cancellationToken)
    {
        var dates = drafts
            .Select(draft => draft.ProposedDate)
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .Distinct()
            .ToList();

        if (dates.Count == 0)
        {
            return;
        }

        var existingGigs = await db.Gigs
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Where(gig => dates.Contains(gig.Date))
            .Select(gig => new GigCandidate(gig.ClientId, gig.Date, gig.Title, gig.Venue))
            .ToListAsync(cancellationToken);

        foreach (var draft in drafts.Where(draft => draft.ProposedDate.HasValue))
        {
            foreach (var gig in existingGigs.Where(gig => gig.Date == draft.ProposedDate!.Value))
            {
                foreach (var rule in GigRules)
                {
                    var warning = rule(draft, gig);
                    if (!string.IsNullOrWhiteSpace(warning))
                    {
                        AddWarning(warnings[draft.Id], warning);
                    }
                }
            }
        }
    }

    private static void AddInBatchWarnings(
        IReadOnlyList<GigImportDraft> drafts,
        Dictionary<Guid, List<string>> warnings)
    {
        var groups = drafts
            .Where(draft => draft.ProposedDate.HasValue)
            .Select(draft => new
            {
                Draft = draft,
                HasDuplicateSignal = !string.IsNullOrWhiteSpace(BuildVenue(draft)) ||
                    !string.IsNullOrWhiteSpace(draft.ProposedTitle),
                Key = $"{draft.ProposedDate!.Value:yyyy-MM-dd}|{NormalizeForComparison(BuildVenue(draft))}|{NormalizeForComparison(draft.ProposedTitle)}",
            })
            .Where(value => value.HasDuplicateSignal)
            .GroupBy(value => value.Key)
            .Where(group => group.Count() > 1);

        foreach (var group in groups)
        {
            foreach (var value in group)
            {
                AddWarning(
                    warnings[value.Draft.Id],
                    $"{GeneratedWarningPrefix} another staged row has the same date, venue, and title.");
            }
        }
    }

    private async Task AddSourceFingerprintWarningsAsync(
        Guid? userId,
        GigImportBatch batch,
        IReadOnlyList<GigImportDraft> drafts,
        Dictionary<Guid, List<string>> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(batch.SourceFingerprint))
        {
            return;
        }

        var sourceSeenBefore = await db.GigImportBatches
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .AnyAsync(
                other => other.Id != batch.Id &&
                    other.SourceFingerprint == batch.SourceFingerprint &&
                    other.Status != GigImportBatchStatus.Abandoned,
                cancellationToken);

        if (!sourceSeenBefore)
        {
            return;
        }

        foreach (var draft in drafts)
        {
            AddWarning(warnings[draft.Id], $"{GeneratedWarningPrefix} this source fingerprint has been imported before.");
        }
    }

    private static string? SameDateAndVenue(GigImportDraft draft, GigCandidate gig)
    {
        var draftVenue = NormalizeForComparison(BuildVenue(draft));
        var existingVenue = NormalizeForComparison(gig.Venue);
        if (draftVenue.Length == 0 || existingVenue.Length == 0)
        {
            return null;
        }

        if (!existingVenue.Contains(draftVenue, StringComparison.Ordinal) &&
            !draftVenue.Contains(existingVenue, StringComparison.Ordinal))
        {
            return null;
        }

        return $"{GeneratedWarningPrefix} existing gig found on {gig.Date:yyyy-MM-dd} at {gig.Venue}.";
    }

    private static string? SameClientAndDate(GigImportDraft draft, GigCandidate gig)
    {
        return draft.ProposedClientId == gig.ClientId
            ? $"{GeneratedWarningPrefix} existing gig found for this client on {gig.Date:yyyy-MM-dd}."
            : null;
    }

    private static string? SameDateAndSimilarTitle(GigImportDraft draft, GigCandidate gig)
    {
        var draftTitle = NormalizeForComparison(draft.ProposedTitle);
        var existingTitle = NormalizeForComparison(gig.Title);
        if (draftTitle.Length < 5 || existingTitle.Length < 5)
        {
            return null;
        }

        var titlesMatch = draftTitle == existingTitle ||
            existingTitle.Contains(draftTitle, StringComparison.Ordinal) ||
            draftTitle.Contains(existingTitle, StringComparison.Ordinal);

        return titlesMatch
            ? $"{GeneratedWarningPrefix} similar existing gig found on {gig.Date:yyyy-MM-dd}: {gig.Title}."
            : null;
    }

    private static string BuildVenue(GigImportDraft draft)
    {
        return string.Join(", ", new[]
        {
            draft.ProposedVenueName,
            draft.ProposedVenueAddress,
            draft.ProposedVenuePostcode,
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
    }

    private static string NormalizeForComparison(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static void AddWarning(List<string> warnings, string warning)
    {
        if (!warnings.Contains(warning, StringComparer.Ordinal))
        {
            warnings.Add(warning);
        }
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyList<string>> ToResult(Dictionary<Guid, List<string>> warnings)
    {
        return warnings.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Take(10).ToList());
    }

    private sealed record GigCandidate(Guid ClientId, DateOnly Date, string Title, string Venue);
}
