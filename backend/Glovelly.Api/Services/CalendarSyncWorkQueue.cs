using Glovelly.Api.Data;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class CalendarSyncWorkQueue(AppDbContext dbContext) : ICalendarSyncWorkQueue
{
    public async Task EnqueueGigAsync(
        Guid userId,
        Guid gigId,
        CalendarSyncWorkItemReason reason,
        CancellationToken cancellationToken = default)
    {
        await EnqueueAsync(userId, gigId, reason, cancellationToken);
    }

    public async Task EnqueueFullSyncAsync(
        Guid userId,
        CalendarSyncWorkItemReason reason,
        CancellationToken cancellationToken = default)
    {
        await EnqueueAsync(userId, null, reason, cancellationToken);
    }

    private async Task EnqueueAsync(
        Guid userId,
        Guid? gigId,
        CalendarSyncWorkItemReason reason,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.CalendarSyncWorkItems.Add(new CalendarSyncWorkItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GigId = gigId,
            Provider = CalendarProvider.GoogleCalendar,
            Reason = reason,
            Status = CalendarSyncWorkItemStatus.Pending,
            AttemptCount = 0,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
