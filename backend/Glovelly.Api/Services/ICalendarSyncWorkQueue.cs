using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface ICalendarSyncWorkQueue
{
    Task EnqueueGigAsync(
        Guid userId,
        Guid gigId,
        CalendarSyncWorkItemReason reason,
        CancellationToken cancellationToken = default);

    Task EnqueueFullSyncAsync(
        Guid userId,
        CalendarSyncWorkItemReason reason,
        CancellationToken cancellationToken = default);
}
