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
    private const int MaxErrorDetailLength = 4000;
    private static readonly TimeSpan DefaultProcessingTimeout = TimeSpan.FromMinutes(10);

    public async Task<CalendarSyncDrainResult> DrainAsync(
        CalendarSyncDrainOptions options,
        CancellationToken cancellationToken = default)
    {
        var maxItems = Math.Max(1, options.MaxItems);
        var ownerId = string.IsNullOrWhiteSpace(options.OwnerId)
            ? $"{Environment.MachineName}:{Guid.NewGuid():N}"
            : options.OwnerId.Trim();
        var processingTimeout = options.ProcessingTimeout ?? DefaultProcessingTimeout;
        var stopAt = options.MaxDuration is { } maxDuration
            ? DateTimeOffset.UtcNow.Add(maxDuration)
            : (DateTimeOffset?)null;
        var recovered = await RecoverStaleProcessingItemsAsync(processingTimeout, cancellationToken);
        var processed = 0;
        var succeeded = 0;
        var retried = 0;
        var failed = 0;

        while (processed < maxItems &&
               (!stopAt.HasValue || DateTimeOffset.UtcNow < stopAt.Value))
        {
            var outcome = await ProcessNextAsync(ownerId, cancellationToken);
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

        return new CalendarSyncDrainResult(processed, succeeded, retried, failed, Skipped: 0, recovered);
    }

    private async Task<int> RecoverStaleProcessingItemsAsync(
        TimeSpan processingTimeout,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Subtract(processingTimeout);
        var staleItems = await dbContext.CalendarSyncWorkItems
            .Where(item =>
                item.Status == CalendarSyncWorkItemStatus.Processing &&
                item.ProcessingStartedAtUtc != null &&
                item.ProcessingStartedAtUtc <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var item in staleItems)
        {
            var previousOwnerId = item.ProcessingOwnerId;
            var previousStartedAtUtc = item.ProcessingStartedAtUtc;
            item.Status = CalendarSyncWorkItemStatus.Pending;
            item.ProcessingOwnerId = null;
            item.ProcessingStartedAtUtc = null;
            item.NextAttemptAtUtc = now;
            item.LastError = "Previous processing attempt timed out or was interrupted.";
            item.LastErrorType = "ProcessingTimeout";
            item.LastErrorDetail =
                $"Previous owner: {previousOwnerId ?? "(unknown)"}; started at: {previousStartedAtUtc?.ToString("O") ?? "(unknown)"}.";
            item.UpdatedAtUtc = now;
        }

        if (staleItems.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return staleItems.Count;
    }

    private async Task<CalendarSyncDrainItemOutcome> ProcessNextAsync(
        string ownerId,
        CancellationToken cancellationToken)
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
        workItem.ProcessingOwnerId = ownerId;
        workItem.ProcessingStartedAtUtc = now;
        workItem.LastAttemptedAtUtc = now;
        workItem.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await processor.ProcessAsync(workItem, cancellationToken);
            workItem.Status = CalendarSyncWorkItemStatus.Succeeded;
            workItem.ProcessingOwnerId = null;
            workItem.ProcessingStartedAtUtc = null;
            workItem.LastError = null;
            workItem.LastErrorType = null;
            workItem.LastErrorDetail = null;
            workItem.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return CalendarSyncDrainItemOutcome.Succeeded;
        }
        catch (Exception ex)
        {
            workItem.AttemptCount += 1;
            workItem.LastError = ex.Message;
            workItem.LastErrorType = ex.GetType().FullName;
            workItem.LastErrorDetail = Truncate(ex.ToString(), MaxErrorDetailLength);
            workItem.Status = workItem.AttemptCount >= MaxAttempts
                ? CalendarSyncWorkItemStatus.Failed
                : CalendarSyncWorkItemStatus.Pending;
            workItem.NextAttemptAtUtc = DateTimeOffset.UtcNow.Add(GetRetryDelay(workItem.AttemptCount));
            workItem.ProcessingOwnerId = null;
            workItem.ProcessingStartedAtUtc = null;
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

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private enum CalendarSyncDrainItemOutcome
    {
        None,
        Succeeded,
        Retried,
        Failed
    }
}
