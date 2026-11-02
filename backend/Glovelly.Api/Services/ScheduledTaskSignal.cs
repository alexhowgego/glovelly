namespace Glovelly.Api.Services;

public interface IScheduledTaskSignal
{
    Task MarkStaleAsync(
        string taskName,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ScheduledTaskSignal(
    IScheduledTaskStateStore stateStore,
    TimeProvider timeProvider) : IScheduledTaskSignal
{
    public async Task MarkStaleAsync(
        string taskName,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (taskName == ScheduledTaskNames.IntakePreparation)
        {
            var intake = await stateStore.ReadAsync<IntakePreparationTaskState>(taskName, cancellationToken)
                ?? new ScheduledTaskStateEnvelope<IntakePreparationTaskState> { TaskName = taskName };
            intake.State.HasPendingWork = true;
            intake.State.LastMarkedStaleUtc = timeProvider.GetUtcNow();
            await stateStore.WriteAsync(intake, cancellationToken);
            return;
        }
        if (taskName != ScheduledTaskNames.GoogleCalendarPropagation)
        {
            throw new InvalidOperationException($"Scheduled task '{taskName}' does not support stale signalling.");
        }

        var now = timeProvider.GetUtcNow();
        var envelope = await stateStore.ReadAsync<GoogleCalendarPropagationTaskState>(taskName, cancellationToken)
            ?? new ScheduledTaskStateEnvelope<GoogleCalendarPropagationTaskState>
            {
                TaskName = taskName
            };

        if (envelope.State.HasPendingCalendarChanges)
        {
            return;
        }

        envelope.TaskName = taskName;
        envelope.State.HasPendingCalendarChanges = true;
        envelope.State.LastMarkedStaleUtc = now;
        envelope.State.LastMarkedStaleReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await stateStore.WriteAsync(envelope, cancellationToken);
    }
}

public sealed class IntakePreparationTaskState
{
    public bool HasPendingWork { get; set; }
    public DateTimeOffset? LastMarkedStaleUtc { get; set; }
}
