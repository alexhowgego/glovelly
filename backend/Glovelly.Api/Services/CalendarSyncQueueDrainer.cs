using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class CalendarSyncQueueDrainer(
    AppDbContext dbContext,
    IGoogleCalendarSyncProcessor processor,
    ILogger<CalendarSyncQueueDrainer> logger) : ICalendarSyncQueueDrainer
{
    private const int MaxAttempts = 5;

    public async Task<CalendarSyncDrainResult> DrainAsync(
        CalendarSyncDrainOptions options,
        CancellationToken cancellationToken = default)
    {
        var maxItems = Math.Max(1, options.MaxItems);
        var stopAt = options.MaxDuration is { } maxDuration
            ? DateTimeOffset.UtcNow.Add(maxDuration)
            : (DateTimeOffset?)null;
        var processed = 0;
        var succeeded = 0;
        var retried = 0;
        var failed = 0;

        while (processed < maxItems &&
               (!stopAt.HasValue || DateTimeOffset.UtcNow < stopAt.Value))
        {
            var outcome = await ProcessNextAsync(cancellationToken);
            if (outcome == CalendarSyncDrainItemOutcome.None)
            {
                break;
            }

            processed += 1;
            switch (outcome)
            {
                case CalendarSyncDrainItemOutcome.Succeeded:
                    succeeded += 1;
                    break;
                case CalendarSyncDrainItemOutcome.Retried:
                    retried += 1;
                    break;
                case CalendarSyncDrainItemOutcome.Failed:
                    failed += 1;
                    break;
            }
        }

        return new CalendarSyncDrainResult(processed, succeeded, retried, failed, Skipped: 0);
    }

    private async Task<CalendarSyncDrainItemOutcome> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var workItem = await dbContext.CalendarSyncWorkItems
            .OrderBy(item => item.NextAttemptAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item =>
                item.Status == CalendarSyncWorkItemStatus.Pending &&
                item.NextAttemptAtUtc <= now,
                cancellationToken);
        if (workItem is null)
        {
            return CalendarSyncDrainItemOutcome.None;
        }

        workItem.Status = CalendarSyncWorkItemStatus.Processing;
        workItem.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await processor.ProcessAsync(workItem, cancellationToken);
            workItem.Status = CalendarSyncWorkItemStatus.Succeeded;
            workItem.LastError = null;
            workItem.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return CalendarSyncDrainItemOutcome.Succeeded;
        }
        catch (Exception ex)
        {
            workItem.AttemptCount += 1;
            workItem.LastError = ex.Message;
            workItem.Status = workItem.AttemptCount >= MaxAttempts
                ? CalendarSyncWorkItemStatus.Failed
                : CalendarSyncWorkItemStatus.Pending;
            workItem.NextAttemptAtUtc = DateTimeOffset.UtcNow.Add(GetRetryDelay(workItem.AttemptCount));
            workItem.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                ex,
                "Google Calendar sync work item {WorkItemId} failed on attempt {AttemptCount}.",
                workItem.Id,
                workItem.AttemptCount);

            return workItem.Status == CalendarSyncWorkItemStatus.Failed
                ? CalendarSyncDrainItemOutcome.Failed
                : CalendarSyncDrainItemOutcome.Retried;
        }
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        return attemptCount switch
        {
            <= 1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1),
        };
    }

    private enum CalendarSyncDrainItemOutcome
    {
        None,
        Succeeded,
        Retried,
        Failed
    }
}
