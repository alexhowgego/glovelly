using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IGigCalendarEventMapper
{
    bool ShouldExistInCalendar(Gig gig, GoogleCalendarIntegrationSettings settings);

    CalendarEventPayload Map(
        Gig gig,
        Client client,
        GoogleCalendarIntegrationSettings settings);
}
