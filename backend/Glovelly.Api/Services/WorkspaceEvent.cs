namespace Glovelly.Api.Services;

public sealed record WorkspaceEvent(
    string Scope,
    string Action,
    Guid? EntityId,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyDictionary<string, string>? Metadata = null);
