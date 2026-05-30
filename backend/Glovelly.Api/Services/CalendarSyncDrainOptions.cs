namespace Glovelly.Api.Services;

public sealed record CalendarSyncDrainOptions(
    int MaxItems = 50,
    TimeSpan? MaxDuration = null);
