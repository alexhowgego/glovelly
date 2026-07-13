using System.Text.Json;
using System.Text.Json.Serialization;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class SetListChartMatchJobProcessor(
    AppDbContext db,
    ISetListChartMatcher chartMatcher,
    IWorkspaceEventPublisher workspaceEventPublisher,
    TimeProvider timeProvider,
    ILogger<SetListChartMatchJobProcessor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan RunningJobStaleAfter = TimeSpan.FromMinutes(15);

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await db.SetListChartMatchJobs.FirstOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || job.Status is SetListChartMatchJobStatus.Completed or SetListChartMatchJobStatus.Failed or SetListChartMatchJobStatus.Cancelled)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (job.Status == SetListChartMatchJobStatus.Running && job.StartedAtUtc > now.Subtract(RunningJobStaleAfter))
        {
            return;
        }

        job.Status = SetListChartMatchJobStatus.Running;
        job.SafeErrorMessage = null;
        job.StartedAtUtc ??= now;
        job.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await PublishJobEventAsync(job, "started", cancellationToken);

        try
        {
            var input = JsonSerializer.Deserialize<IReadOnlyList<SetListChartMatchInput>>(job.InputJson, JsonOptions);
            if (input is null || input.Count == 0)
            {
                throw new InvalidOperationException("The chart matching job input could not be read.");
            }

            var matches = await chartMatcher.MatchAsync(job.UserId, input, cancellationToken, useConfiguredRanker: true);
            job.ResultJson = JsonSerializer.Serialize(matches, JsonOptions);
            job.Status = SetListChartMatchJobStatus.Completed;
            job.CompletedAtUtc = timeProvider.GetUtcNow();
            job.UpdatedAtUtc = job.CompletedAtUtc.Value;
            await db.SaveChangesAsync(cancellationToken);
            await PublishJobEventAsync(job, "completed", cancellationToken);
            logger.LogInformation(
                "Set list chart match job completed: JobId {JobId}, GigId {GigId}, CorrelationId {CorrelationId}, ResultCount {ResultCount}.",
                job.Id,
                job.GigId,
                job.CorrelationId,
                matches.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Status = SetListChartMatchJobStatus.Cancelled;
            job.SafeErrorMessage = "Chart matching was cancelled before it completed.";
            job.CompletedAtUtc = timeProvider.GetUtcNow();
            job.UpdatedAtUtc = job.CompletedAtUtc.Value;
            await db.SaveChangesAsync(CancellationToken.None);
            await PublishJobEventAsync(job, "failed", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            job.Status = SetListChartMatchJobStatus.Failed;
            job.SafeErrorMessage = "Chart matching failed. Try again, or continue reviewing deterministic candidates manually.";
            job.CompletedAtUtc = timeProvider.GetUtcNow();
            job.UpdatedAtUtc = job.CompletedAtUtc.Value;
            await db.SaveChangesAsync(CancellationToken.None);
            await PublishJobEventAsync(job, "failed", CancellationToken.None);
            logger.LogWarning(
                exception,
                "Set list chart match job failed: JobId {JobId}, GigId {GigId}, CorrelationId {CorrelationId}.",
                job.Id,
                job.GigId,
                job.CorrelationId);
        }
    }

    public async Task FailStaleRunningJobsAsync(CancellationToken cancellationToken = default)
    {
        var staleBefore = timeProvider.GetUtcNow().Subtract(RunningJobStaleAfter);
        var staleJobs = await db.SetListChartMatchJobs
            .Where(job => job.Status == SetListChartMatchJobStatus.Running && job.StartedAtUtc < staleBefore)
            .ToListAsync(cancellationToken);
        foreach (var job in staleJobs)
        {
            job.Status = SetListChartMatchJobStatus.Failed;
            job.SafeErrorMessage = "Chart matching stopped before it completed. Try again, or continue reviewing deterministic candidates manually.";
            job.CompletedAtUtc = timeProvider.GetUtcNow();
            job.UpdatedAtUtc = job.CompletedAtUtc.Value;
        }

        if (staleJobs.Count == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var job in staleJobs)
        {
            await PublishJobEventAsync(job, "failed", cancellationToken);
        }
    }

    private Task PublishJobEventAsync(SetListChartMatchJob job, string action, CancellationToken cancellationToken)
    {
        return workspaceEventPublisher.PublishAsync(
            job.UserId,
            new WorkspaceEvent(
                "setlist-chart-matching",
                action,
                job.Id,
                timeProvider.GetUtcNow(),
                new Dictionary<string, string>
                {
                    ["gigId"] = job.GigId.ToString(),
                    ["status"] = job.Status.ToString(),
                }),
            cancellationToken);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
