namespace Glovelly.Api.Services;

public sealed class GoogleCalendarPropagationScheduledTask(
    IScheduledTaskStateStore stateStore,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<GoogleCalendarPropagationScheduledTask> logger) : IScheduledTask<CalendarSyncDrainOptions, CalendarSyncDrainResult>
{
    private static readonly TimeSpan SafetyInterval = TimeSpan.FromHours(24);

    public string Name => ScheduledTaskNames.GoogleCalendarPropagation;

    public async Task<ExecutionDecision> ShouldRunAsync(
        ScheduledTaskContext context,
        CancellationToken cancellationToken = default)
    {
        // This wake gate deliberately reads only task state storage. Do not resolve EF-backed services here.
        ScheduledTaskStateEnvelope<GoogleCalendarPropagationTaskState>? envelope;
        try
        {
            envelope = await stateStore.ReadAsync<GoogleCalendarPropagationTaskState>(Name, cancellationToken);
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

        var state = envelope.State;
        if (state.HasPendingCalendarChanges)
        {
            return ExecutionDecision.Run($"Task is stale from {state.LastMarkedStaleReason ?? "unknown reason"}.");
        }

        if (state.LastSuccessfulPropagationUtc is null)
        {
            return ExecutionDecision.Run("Task has no recorded successful propagation.");
        }

        if (context.NowUtc - state.LastSuccessfulPropagationUtc.Value >= SafetyInterval)
        {
            return ExecutionDecision.Run("Safety interval elapsed since last successful propagation.");
        }

        return ExecutionDecision.Skip("No pending calendar changes and safety interval has not elapsed.");
    }

    public async Task<CalendarSyncDrainResult> ExecuteAsync(
        CalendarSyncDrainOptions options,
        ScheduledTaskContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(options, cancellationToken);

        if (result.CanConcludeQueueIsFullyDrained)
        {
            try
            {
                await MarkSuccessfulFullyDrainedRunAsync(context.NowUtc, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Scheduled task {TaskName} fully drained but stale state could not be cleared. The next execution will check Postgres again.",
                    Name);
            }
        }
        else
        {
            logger.LogInformation(
                "Scheduled task {TaskName} left stale because calendar sync drain completed with {CompletionReason}. Retried: {Retried}; Failed: {Failed}.",
                Name,
                result.CompletionReason,
                result.Retried,
                result.Failed);
        }

        return result;
    }

    private async Task MarkSuccessfulFullyDrainedRunAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var envelope = await stateStore.ReadAsync<GoogleCalendarPropagationTaskState>(Name, cancellationToken)
            ?? new ScheduledTaskStateEnvelope<GoogleCalendarPropagationTaskState>
            {
                TaskName = Name
            };

        envelope.TaskName = Name;
        envelope.LastDecisionUtc = now;
        envelope.LastSuccessfulRunUtc = now;
        envelope.State.HasPendingCalendarChanges = false;
        envelope.State.LastSuccessfulPropagationUtc = now;

        await stateStore.WriteAsync(envelope, cancellationToken);
    }
}

public sealed class GoogleCalendarPropagationTaskState
{
    public bool HasPendingCalendarChanges { get; set; }

    public DateTimeOffset? LastMarkedStaleUtc { get; set; }

    public string? LastMarkedStaleReason { get; set; }

    public DateTimeOffset? LastSuccessfulPropagationUtc { get; set; }
}
