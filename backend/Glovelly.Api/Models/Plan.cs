namespace Glovelly.Api.Models;

public sealed class Plan
{
    public const string Free = "free";
    public const string Solo = "solo";
    public const string Pro = "pro";

    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? MonthlyFirstIssueLimit { get; set; }
    public bool PaidIncomeSummary { get; set; }
}
