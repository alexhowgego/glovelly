namespace Glovelly.Api.Services;

public interface IGoogleCalendarApiClient
{
    Task<GoogleCalendarCreateResult> CreateCalendarAsync(
        string accessToken,
        string summary,
        CancellationToken cancellationToken);

    Task<GoogleCalendarEventResult> CreateEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CalendarEventPayload payload,
        CancellationToken cancellationToken);

    Task<GoogleCalendarEventResult> UpdateEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CalendarEventPayload payload,
        CancellationToken cancellationToken);

    Task DeleteEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CancellationToken cancellationToken);
}
