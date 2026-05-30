namespace Glovelly.Api.Services;

public sealed record CalendarSyncDrainResult(
    int Processed,
    int Succeeded,
    int Retried,
    int Failed,
    int Skipped,
    int Recovered);
