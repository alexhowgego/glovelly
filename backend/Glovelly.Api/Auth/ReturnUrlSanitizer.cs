namespace Glovelly.Api.Auth;

public static class ReturnUrlSanitizer
{
    public static string BuildSafeLocalReturnPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (returnUrl.Contains('\r') || returnUrl.Contains('\n') || returnUrl.Contains('\0'))
        {
            return "/";
        }

        if (!returnUrl.StartsWith('/', StringComparison.Ordinal) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            returnUrl.Contains('\\'))
        {
            return "/";
        }

        return returnUrl;
    }
}
