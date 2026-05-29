namespace Glovelly.Api.Services;

public interface ICalendarEventPayloadHasher
{
    string Hash(CalendarEventPayload payload);
}
