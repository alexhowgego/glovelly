namespace Glovelly.Api.Models;

public sealed class LicenceChange
{
    public Guid Id { get; set; }
    public Guid TargetUserId { get; set; }
    public string TargetIdentity { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string ActorIdentity { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; set; }
    // JSON snapshots retain the full assignment, including expiry and version.
    public string? PreviousAssignment { get; set; }
    public string NewAssignment { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
