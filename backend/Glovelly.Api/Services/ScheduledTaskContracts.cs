namespace Glovelly.Api.Services;

public interface IScheduledTask<in TOptions, TResult>
{
    string Name { get; }

    // Wake gates must avoid resolving AppDbContext or other Postgres-backed services.
    // They are cheap hints that decide whether execution is worth opening the source of truth.
    Task<ExecutionDecision> ShouldRunAsync(
        ScheduledTaskContext context,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync(
        TOptions options,
        ScheduledTaskContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ScheduledTaskContext(DateTimeOffset NowUtc);

public sealed record ExecutionDecision(bool ShouldRun, string Reason)
{
    public static ExecutionDecision Run(string reason) => new(true, reason);

    public static ExecutionDecision Skip(string reason) => new(false, reason);
}

public static class ScheduledTaskNames
{
    public const string GoogleCalendarPropagation = "google-calendar-propagation";
}
