using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Glovelly.Api.Endpoints;

internal static class GigQuickCaptureSupport
{
    public static Guid? TryReadGigId(IFormCollection form)
    {
        var rawValue = form["gigId"].FirstOrDefault();
        return Guid.TryParse(rawValue, out var gigId) && gigId != Guid.Empty ? gigId : null;
    }

    public static QuickCaptureSettings NormalizeSettings(QuickCaptureSettings settings)
    {
        return new QuickCaptureSettings
        {
            CandidateCount = Math.Clamp(settings.CandidateCount, 1, 20),
            AutoAttachWindowDays = Math.Clamp(settings.AutoAttachWindowDays, 0, 365),
            AmbiguityWindowDays = Math.Clamp(settings.AmbiguityWindowDays, 0, 365),
        };
    }

    public static async Task<QuickGigCandidateResult> QueryCandidatesAsync(
        AppDbContext db,
        Guid? userId,
        DateOnly today,
        QuickCaptureSettings settings,
        string? continuation = null)
    {
        var gigs = await db.Gigs
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Where(value => value.Status != GigStatus.Cancelled)
            .ToListAsync();

        var eligibleCandidates = gigs
            .Select(gig => new QuickGigCandidate(
                gig.Id,
                gig.ClientId,
                gig.Title,
                gig.Date,
                gig.Venue,
                gig.Type,
                gig.Status,
                Math.Abs(gig.Date.DayNumber - today.DayNumber)))
            .OrderBy(candidate => candidate.DaysFromToday)
            .ThenBy(candidate => candidate.Date)
            .ThenBy(candidate => candidate.Title)
            .ThenBy(candidate => candidate.Id)
            .ToList();

        if (string.IsNullOrWhiteSpace(continuation))
        {
            var nearbyCandidates = eligibleCandidates
                .Where(candidate => candidate.DaysFromToday <= settings.AutoAttachWindowDays)
                .ToList();

            var cutoff = nearbyCandidates.Count >= settings.CandidateCount
                ? nearbyCandidates[settings.CandidateCount - 1].DaysFromToday
                : (int?)null;
            var candidates = nearbyCandidates
                .Where((candidate, index) => !cutoff.HasValue || index < settings.CandidateCount || candidate.DaysFromToday == cutoff.Value)
                .ToList();
            var hasMore = candidates.Count == 0
                ? eligibleCandidates.Count > 0
                : eligibleCandidates.Any(candidate => IsAfter(candidate, candidates[^1]));

            return new QuickGigCandidateResult(candidates, hasMore, hasMore ? EncodeContinuation(candidates.LastOrDefault()) : null);
        }

        if (!TryDecodeContinuation(continuation, out var cursor))
        {
            throw new InvalidOperationException("The candidate continuation is invalid.");
        }

        var page = eligibleCandidates
            .Where(candidate => cursor is null || IsAfter(candidate, cursor))
            .Take(QuickCapturePageSize + 1)
            .ToList();
        var hasNextPage = page.Count > QuickCapturePageSize;
        if (hasNextPage)
        {
            page.RemoveAt(page.Count - 1);
        }

        return new QuickGigCandidateResult(page, hasNextPage, hasNextPage ? EncodeContinuation(page[^1]) : null);
    }

    private const int QuickCapturePageSize = 20;

    private static bool IsAfter(QuickGigCandidate candidate, QuickGigCandidate cursor) =>
        candidate.DaysFromToday > cursor.DaysFromToday ||
        candidate.DaysFromToday == cursor.DaysFromToday && candidate.Date > cursor.Date ||
        candidate.DaysFromToday == cursor.DaysFromToday && candidate.Date == cursor.Date && string.CompareOrdinal(candidate.Title, cursor.Title) > 0 ||
        candidate.DaysFromToday == cursor.DaysFromToday && candidate.Date == cursor.Date && string.Equals(candidate.Title, cursor.Title, StringComparison.Ordinal) && candidate.Id.CompareTo(cursor.Id) > 0;

    private static string EncodeContinuation(QuickGigCandidate? candidate)
    {
        if (candidate is null)
        {
            return Convert.ToBase64String("{}"u8);
        }

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new QuickGigCandidateCursor(
            candidate.Id, candidate.Date, candidate.Title, candidate.DaysFromToday)));
    }

    private static bool TryDecodeContinuation(string continuation, out QuickGigCandidate? cursor)
    {
        cursor = null;
        try
        {
            var json = Convert.FromBase64String(continuation);
            if (json.Length == 2 && Encoding.UTF8.GetString(json) == "{}")
            {
                return true;
            }

            var value = JsonSerializer.Deserialize<QuickGigCandidateCursor>(json);
            if (value is null || value.Id == Guid.Empty || string.IsNullOrWhiteSpace(value.Title))
            {
                return false;
            }

            cursor = new QuickGigCandidate(value.Id, Guid.Empty, value.Title, value.Date, string.Empty, default, default, value.DaysFromToday);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool HasNearbyCandidates(
        IEnumerable<QuickGigCandidate> candidates,
        Guid selectedGigId,
        QuickCaptureSettings settings)
    {
        return candidates.Any(candidate =>
            candidate.Id != selectedGigId &&
            candidate.DaysFromToday <= settings.AmbiguityWindowDays);
    }

    public static IEnumerable<object> ToCandidateResponses(
        IEnumerable<QuickGigCandidate> candidates,
        Guid? selectedGigId)
    {
        return candidates.Select(candidate => new
        {
            candidate.Id,
            candidate.ClientId,
            candidate.Title,
            candidate.Date,
            candidate.Venue,
            candidate.Type,
            candidate.Status,
            candidate.DaysFromToday,
            IsSelected = candidate.Id == selectedGigId,
        });
    }
}

internal sealed record QuickGigCandidate(
    Guid Id,
    Guid ClientId,
    string Title,
    DateOnly Date,
    string Venue,
    GigType Type,
    GigStatus Status,
    int DaysFromToday);

internal sealed record QuickGigCandidateResult(
    IReadOnlyList<QuickGigCandidate> Candidates,
    bool HasMore,
    string? Continuation);

internal sealed record QuickGigCandidateCursor(Guid Id, DateOnly Date, string Title, int DaysFromToday);
