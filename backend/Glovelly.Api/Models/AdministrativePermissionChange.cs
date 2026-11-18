namespace Glovelly.Api.Models;

public sealed class AdministrativePermissionChange
{
    public Guid Id { get; set; }
    public Guid TargetUserId { get; set; }
    public string TargetIdentity { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string ActorIdentity { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string PreviousSets { get; set; } = string.Empty;
    public string NewSets { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
