namespace Glovelly.Api.Models;

public sealed class AccessRequest
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Subject { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public string? RequestIpHash { get; set; }
    public DateTimeOffset? NotificationSentAtUtc { get; set; }
    public string? NotificationSuppressionReason { get; set; }
}
