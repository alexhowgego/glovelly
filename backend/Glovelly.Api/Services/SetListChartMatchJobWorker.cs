namespace Glovelly.Api.Services;

internal sealed class SetListChartMatchJobWorker(
    ISetListChartMatchJobQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SetListChartMatchJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FailStaleJobsAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid jobId;
            try
            {
                jobId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<SetListChartMatchJobProcessor>();
                await processor.ProcessAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected error while processing set list chart match job {JobId}.", jobId);
            }
        }
    }

    private async Task FailStaleJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<SetListChartMatchJobProcessor>();
            await processor.FailStaleRunningJobsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to mark stale set list chart match jobs as failed during worker startup.");
        }
    }
}
