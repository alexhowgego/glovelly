namespace Glovelly.Api.Services;

internal sealed class SetListInterpretationJobWorker(ISetListInterpretationJobQueue queue, IServiceScopeFactory scopes, ILogger<SetListInterpretationJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = scopes.CreateScope()) await scope.ServiceProvider.GetRequiredService<SetListInterpretationRetentionService>().CleanupAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { var id = await queue.DequeueAsync(stoppingToken); using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<SetListInterpretationJobProcessor>().ProcessAsync(id, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Unexpected error while processing a set-list interpretation job."); }
        }
    }
}
