using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

internal sealed class GoogleCalendarSyncWorker(
    IServiceScopeFactory scopeFactory,
    StartupSettings startupSettings,
    ILogger<GoogleCalendarSyncWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (startupSettings.IsTesting)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar sync worker failed while processing queue.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<IGoogleCalendarSyncProcessor>();
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
            return false;
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
            logger.LogWarning(
                ex,
                "Google Calendar sync work item {WorkItemId} failed on attempt {AttemptCount}.",
                workItem.Id,
                workItem.AttemptCount);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
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
}
