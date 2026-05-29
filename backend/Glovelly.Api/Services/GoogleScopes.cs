namespace Glovelly.Api.Services;

public static class GoogleScopes
{
    public const string OpenId = "openid";
    public const string Email = "email";
    public const string Profile = "profile";
    public const string DriveFile = "https://www.googleapis.com/auth/drive.file";
    public const string CalendarEvents = "https://www.googleapis.com/auth/calendar.events";

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

    private static IEnumerable<string> Split(string grantedScopes)
    {
        return grantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
