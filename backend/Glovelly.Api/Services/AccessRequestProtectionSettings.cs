namespace Glovelly.Api.Services;

public sealed class AccessRequestProtectionSettings
{
    public const string SectionName = "AccessRequests";

    public int PerIpShortWindowPermitLimit { get; set; } = 5;
    public TimeSpan PerIpShortWindow { get; set; } = TimeSpan.FromMinutes(10);
    public int PerIpDailyPermitLimit { get; set; } = 20;
    public TimeSpan PerIpDailyWindow { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan EmailNotificationSuppressionWindow { get; set; } = TimeSpan.FromHours(12);
    public int GlobalNotificationDailyCap { get; set; } = 50;
    public TimeSpan GlobalNotificationWindow { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan RetentionWindow { get; set; } = TimeSpan.FromDays(180);
    public TimeSpan CleanupSlack { get; set; } = TimeSpan.FromDays(2);
}
