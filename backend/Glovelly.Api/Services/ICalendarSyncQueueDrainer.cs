namespace Glovelly.Api.Services;

public interface ICalendarSyncQueueDrainer
{
    Task<CalendarSyncDrainResult> DrainAsync(
        CalendarSyncDrainOptions options,
        CancellationToken cancellationToken = default);
}
