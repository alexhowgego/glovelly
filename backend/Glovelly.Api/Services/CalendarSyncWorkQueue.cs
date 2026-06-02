using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

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
        var existingWorkItem = await dbContext.CalendarSyncWorkItems
            .FirstOrDefaultAsync(item =>
                item.UserId == userId &&
                item.GigId == gigId &&
                item.Provider == CalendarProvider.GoogleCalendar &&
                (item.Status == CalendarSyncWorkItemStatus.Pending ||
                 item.Status == CalendarSyncWorkItemStatus.Processing),
                cancellationToken);

        if (existingWorkItem is not null)
        {
            existingWorkItem.Reason = reason;
            if (existingWorkItem.Status == CalendarSyncWorkItemStatus.Pending)
            {
                existingWorkItem.NextAttemptAtUtc = existingWorkItem.NextAttemptAtUtc <= now
                    ? existingWorkItem.NextAttemptAtUtc
                    : now;
            }

            existingWorkItem.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

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
