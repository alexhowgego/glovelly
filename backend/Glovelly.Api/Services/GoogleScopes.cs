namespace Glovelly.Api.Services;

public static class GoogleScopes
{
    public const string OpenId = "openid";
    public const string Email = "email";
    public const string Profile = "profile";
    public const string DriveFile = "https://www.googleapis.com/auth/drive.file";
    public const string SpreadsheetsReadonly = "https://www.googleapis.com/auth/spreadsheets.readonly";
    public const string CalendarEvents = "https://www.googleapis.com/auth/calendar.events";
    public const string CalendarAppCreated = "https://www.googleapis.com/auth/calendar.app.created";

    public static readonly string[] ManagedIntegrationScopes =
    [
        DriveFile,
        SpreadsheetsReadonly,
        CalendarAppCreated,
    ];

    public static string Join(params string[] scopes)
    {
        return string.Join(' ', scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)));
    }

    public static bool Contains(string grantedScopes, string requiredScope)
    {
        return Split(grantedScopes).Contains(requiredScope, StringComparer.Ordinal);
    }

    public static bool ContainsAll(string grantedScopes, IEnumerable<string> requiredScopes)
    {
        var granted = Split(grantedScopes).ToHashSet(StringComparer.Ordinal);
        return requiredScopes.All(granted.Contains);
    }

    public static IReadOnlyList<string> MergeManagedIntegrationScopes(string grantedScopes, string requiredScope)
    {
        var granted = Split(grantedScopes).ToHashSet(StringComparer.Ordinal);
        granted.Add(requiredScope);

        return ManagedIntegrationScopes
            .Where(granted.Contains)
            .ToList();
    }

    public static string Remove(string grantedScopes, string scopeToRemove)
    {
        return string.Join(
            ' ',
            Split(grantedScopes)
                .Where(scope => !string.Equals(scope, scopeToRemove, StringComparison.Ordinal)));
    }

    private static IEnumerable<string> Split(string grantedScopes)
    {
        return grantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
