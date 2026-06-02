using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class GigCalendarSyncPlanner(
    IGigCalendarEventMapper eventMapper,
    ICalendarEventPayloadHasher payloadHasher) : IGigCalendarSyncPlanner
{
    public CalendarSyncPlan Plan(
        Gig gig,
        Client client,
        GoogleCalendarIntegrationSettings settings,
        GigCalendarSyncState? syncState)
    {
        if (!eventMapper.ShouldExistInCalendar(gig, settings))
        {
            return HasProviderEvent(syncState)
                ? new CalendarSyncPlan(CalendarSyncOperation.Delete, CalendarProvider.GoogleCalendar, null, null, syncState)
                : new CalendarSyncPlan(CalendarSyncOperation.None, CalendarProvider.GoogleCalendar, null, null, syncState);
        }

        var payload = eventMapper.Map(gig, client, settings);
        var payloadHash = payloadHasher.Hash(payload);
        if (!HasProviderEvent(syncState))
        {
            return new CalendarSyncPlan(CalendarSyncOperation.Create, CalendarProvider.GoogleCalendar, payload, payloadHash, syncState);
        }

        return string.Equals(syncState!.LastSyncHash, payloadHash, StringComparison.Ordinal)
            ? new CalendarSyncPlan(CalendarSyncOperation.None, CalendarProvider.GoogleCalendar, payload, payloadHash, syncState)
            : new CalendarSyncPlan(CalendarSyncOperation.Update, CalendarProvider.GoogleCalendar, payload, payloadHash, syncState);
    }

    private static bool HasProviderEvent(GigCalendarSyncState? syncState)
    {
        return syncState is not null &&
            syncState.DeletedAtUtc is null &&
            !string.IsNullOrWhiteSpace(syncState.ProviderEventId);
    }
}
