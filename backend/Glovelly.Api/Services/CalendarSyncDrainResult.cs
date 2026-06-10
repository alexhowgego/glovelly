namespace Glovelly.Api.Services;

public sealed record CalendarSyncDrainResult(
    int Processed,
    int Succeeded,
    int Retried,
    int Failed,
    int Skipped,
    int Recovered,
    CalendarSyncDrainCompletionReason CompletionReason = CalendarSyncDrainCompletionReason.QueueFullyDrained)
{
    public bool CanConcludeQueueIsFullyDrained =>
        CompletionReason == CalendarSyncDrainCompletionReason.QueueFullyDrained &&
        Retried == 0 &&
        Failed == 0;
}

public enum CalendarSyncDrainCompletionReason
{
    QueueFullyDrained,
    MaxItemsReached,
    MaxDurationReached
}
