namespace Glovelly.Api.Services;

public sealed class BusinessLifecycleAdvancementScheduledTask(
    IScheduledTaskStateStore stateStore,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<BusinessLifecycleAdvancementScheduledTask> logger) : IScheduledTask<BusinessLifecycleAdvancementOptions, BusinessLifecycleAdvancementResult>
{
    private static readonly TimeSpan SafetyInterval = TimeSpan.FromDays(1);

    public string Name => ScheduledTaskNames.BusinessLifecycleAdvancement;

    public async Task<ExecutionDecision> ShouldRunAsync(
        ScheduledTaskContext context,
        CancellationToken cancellationToken = default)
    {
        ScheduledTaskStateEnvelope<BusinessLifecycleAdvancementTaskState>? envelope;
        try
        {
            envelope = await stateStore.ReadAsync<BusinessLifecycleAdvancementTaskState>(Name, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Scheduled task {TaskName} wake-gate state could not be read; running so Postgres remains the source of truth.",
                Name);
            return ExecutionDecision.Run("Task state could not be read.");
        }

        if (envelope is null)
        {
            return ExecutionDecision.Run("Task state is missing.");
        }

        var today = DateOnly.FromDateTime(context.NowUtc.UtcDateTime);
        var nextDate = Earliest(envelope.State.NextGigCompletionDate, envelope.State.NextInvoiceOverdueDate);
        if (nextDate.HasValue && nextDate.Value <= today)
        {
            return ExecutionDecision.Run($"Business lifecycle work is due from {nextDate:yyyy-MM-dd}.");
        }

        if (envelope.State.LastSuccessfulAdvancementUtc is null)
        {
            return ExecutionDecision.Run("Task has no recorded successful advancement.");
        }

        if (context.NowUtc - envelope.State.LastSuccessfulAdvancementUtc.Value >= SafetyInterval)
        {
            return ExecutionDecision.Run("Safety interval elapsed since last successful advancement.");
        }

        return nextDate.HasValue
            ? ExecutionDecision.Skip($"Next business lifecycle work is due on {nextDate:yyyy-MM-dd}.")
            : ExecutionDecision.Skip("No pending business lifecycle work is known.");
    }

    public async Task<BusinessLifecycleAdvancementResult> ExecuteAsync(
        BusinessLifecycleAdvancementOptions options,
        ScheduledTaskContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IBusinessLifecycleAdvancementProcessor>();
        var result = await processor.AdvanceAsync(context.NowUtc, cancellationToken);

        try
        {
            await MarkSuccessfulRunAsync(context.NowUtc, result, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Scheduled task {TaskName} completed but wake-gate state could not be updated. A future safety run can recover from Postgres.",
                Name);
        }

        return result;
    }

    private async Task MarkSuccessfulRunAsync(
        DateTimeOffset nowUtc,
        BusinessLifecycleAdvancementResult result,
        CancellationToken cancellationToken)
    {
        var envelope = await stateStore.ReadAsync<BusinessLifecycleAdvancementTaskState>(Name, cancellationToken)
            ?? new ScheduledTaskStateEnvelope<BusinessLifecycleAdvancementTaskState>
            {
                TaskName = Name
            };

        envelope.TaskName = Name;
        envelope.LastDecisionUtc = nowUtc;
        envelope.LastSuccessfulRunUtc = nowUtc;
        envelope.State.NextGigCompletionDate = result.NextGigCompletionDate;
        envelope.State.NextInvoiceOverdueDate = result.NextInvoiceOverdueDate;
        envelope.State.LastSuccessfulAdvancementUtc = nowUtc;

        await stateStore.WriteAsync(envelope, cancellationToken);
    }

    private static DateOnly? Earliest(DateOnly? first, DateOnly? second)
    {
        return (first, second) switch
        {
            ({ } firstValue, { } secondValue) => firstValue <= secondValue ? firstValue : secondValue,
            ({ } firstValue, null) => firstValue,
            (null, { } secondValue) => secondValue,
            _ => null
        };
    }
}
