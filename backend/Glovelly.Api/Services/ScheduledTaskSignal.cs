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

        envelope.TaskName = taskName;
        envelope.State.HasPendingCalendarChanges = true;
        envelope.State.LastMarkedStaleUtc = now;
        envelope.State.LastMarkedStaleReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await stateStore.WriteAsync(envelope, cancellationToken);
    }
}
