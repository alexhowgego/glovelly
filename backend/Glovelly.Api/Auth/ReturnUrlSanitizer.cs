namespace Glovelly.Api.Auth;

public static class ReturnUrlSanitizer
{
    public static string BuildSafeLocalReturnPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (returnUrl.Contains('\r') || returnUrl.Contains('\n'))
        {
            return "/";
        }

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }
}
