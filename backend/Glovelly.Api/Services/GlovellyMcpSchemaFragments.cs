using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public static class GlovellyMcpSchemaFragments
{
    public static object ContactSelector => new Dictionary<string, object>
    {
        ["contactId"] = McpSchema.Uuid("Exact Glovelly contact ID. Use this when a previous contact lookup returned an unambiguous match."),
        ["contactQuery"] = McpSchema.String("Name or email text used to look up a contact when contactId is not known."),
    };

    public static object DateRange => new Dictionary<string, object>
    {
        ["fromDate"] = McpSchema.Date("Inclusive start date in YYYY-MM-DD format."),
        ["toDate"] = McpSchema.Date("Inclusive end date in YYYY-MM-DD format."),
    };

    public static object Confidence => McpSchema.Enum<GigImportDraftConfidence>(
        "How confident the agent is that the draft row values were interpreted correctly.");
}
