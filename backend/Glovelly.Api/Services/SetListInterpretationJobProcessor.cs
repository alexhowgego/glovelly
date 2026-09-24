using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class SetListInterpretationJobProcessor(AppDbContext db, ISetListInterpreter interpreter, IWorkspaceEventPublisher events, TimeProvider clock, ILogger<SetListInterpretationJobProcessor> logger)
{
    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.SetListInterpretationJobs.FirstOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || job.Status is SetListInterpretationJobStatus.Completed or SetListInterpretationJobStatus.Failed or SetListInterpretationJobStatus.Cancelled) return;
        job.Status = SetListInterpretationJobStatus.Running; job.StartedAtUtc ??= clock.GetUtcNow(); job.UpdatedAtUtc = clock.GetUtcNow(); job.SafeErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken); await PublishAsync(job, "started", cancellationToken);
        try
        {
            var grid = JsonSerializer.Deserialize<GoogleSheetGrid>(job.SourceGridJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("The saved worksheet grid could not be read.");
            var result = await interpreter.InterpretAsync(grid, cancellationToken);
            job.ResultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            job.Status = SetListInterpretationJobStatus.Completed; job.CompletedAtUtc = clock.GetUtcNow(); job.UpdatedAtUtc = job.CompletedAtUtc.Value;
            await db.SaveChangesAsync(cancellationToken); await PublishAsync(job, "completed", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Status = SetListInterpretationJobStatus.Cancelled; job.SafeErrorMessage = "Set-list interpretation was cancelled."; job.CompletedAtUtc = clock.GetUtcNow(); job.UpdatedAtUtc = job.CompletedAtUtc.Value;
            await db.SaveChangesAsync(CancellationToken.None); await PublishAsync(job, "failed", CancellationToken.None); throw;
        }
        catch (Exception exception)
        {
            job.Status = SetListInterpretationJobStatus.Failed; job.SafeErrorMessage = "Set-list interpretation failed. Try again or create the draft manually from the retained worksheet grid."; job.CompletedAtUtc = clock.GetUtcNow(); job.UpdatedAtUtc = job.CompletedAtUtc.Value;
            await db.SaveChangesAsync(CancellationToken.None); await PublishAsync(job, "failed", CancellationToken.None);
            logger.LogWarning(exception, "Set-list interpretation job failed: {JobId}, {GigId}, {CorrelationId}.", job.Id, job.GigId, job.CorrelationId);
        }
    }

    private Task PublishAsync(SetListInterpretationJob job, string action, CancellationToken token) => events.PublishAsync(job.UserId, new WorkspaceEvent("setlist-interpretation", action, job.Id, clock.GetUtcNow(), new Dictionary<string, string> { ["gigId"] = job.GigId.ToString(), ["status"] = job.Status.ToString() }), token);
}
