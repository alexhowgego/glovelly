namespace Glovelly.Api.Models;

public sealed class UserSubscription
{
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = Plan.Free;
    public SubscriptionState State { get; set; } = SubscriptionState.Active;
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public string Source { get; set; } = "Manual";
    public string? Reason { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public enum SubscriptionState
{
    Active,
    Cancelled,
}
