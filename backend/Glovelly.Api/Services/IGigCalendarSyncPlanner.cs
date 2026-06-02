using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IGigCalendarSyncPlanner
{
    CalendarSyncPlan Plan(
        Gig gig,
        Client client,
        GoogleCalendarIntegrationSettings settings,
        GigCalendarSyncState? syncState);
}
