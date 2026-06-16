using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

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

    public static async Task<List<QuickGigCandidate>> FindCandidatesAsync(
        AppDbContext db,
        Guid? userId,
        DateOnly today,
        QuickCaptureSettings settings)
    {
        var gigs = await db.Gigs
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Where(value => value.Status != GigStatus.Cancelled)
            .ToListAsync();

        return gigs
            .Select(gig => new QuickGigCandidate(
                gig.Id,
                gig.ClientId,
                gig.Title,
                gig.Date,
                gig.Venue,
                gig.Status,
                Math.Abs(gig.Date.DayNumber - today.DayNumber)))
            .Where(candidate => candidate.DaysFromToday <= settings.AutoAttachWindowDays)
            .OrderBy(candidate => candidate.DaysFromToday)
            .ThenBy(candidate => candidate.Date)
            .ThenBy(candidate => candidate.Title)
            .Take(settings.CandidateCount)
            .ToList();
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
    GigStatus Status,
    int DaysFromToday);
