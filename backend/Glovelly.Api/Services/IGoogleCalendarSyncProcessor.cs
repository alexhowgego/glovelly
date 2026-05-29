using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IGoogleCalendarSyncProcessor
{
    Task ProcessAsync(CalendarSyncWorkItem workItem, CancellationToken cancellationToken);
}
