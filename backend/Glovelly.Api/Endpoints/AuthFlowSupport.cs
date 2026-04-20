using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class AuthFlowSupport
{
    public static string BuildSafeRedirectUri(HttpContext httpContext, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        var sanitizedReturnUrl = returnUrl.Trim();

        if (IsSafeLocalPath(sanitizedReturnUrl))
        {
            return sanitizedReturnUrl;
        }

        if (Uri.TryCreate(sanitizedReturnUrl, UriKind.Absolute, out var absoluteUri))
        {
            var request = httpContext.Request;
            var sameScheme = string.Equals(absoluteUri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase);
            var sameHost = string.Equals(absoluteUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
            var samePort = (absoluteUri.IsDefaultPort && !request.Host.Port.HasValue) ||
                           absoluteUri.Port == request.Host.Port;

            if (sameScheme && sameHost && samePort)
            {
                var localPath = absoluteUri.PathAndQuery + absoluteUri.Fragment;
                return IsSafeLocalPath(localPath) ? localPath : "/";
            }
        }

        return "/";
    }

    private static bool IsSafeLocalPath(string value)
    {
        return value.StartsWith('/', StringComparison.Ordinal) &&
               !value.StartsWith("//", StringComparison.Ordinal) &&
               value.IndexOf('\\') < 0;
    }

    public static bool IsApiRequest(HttpRequest request)
    {
        var path = request.Path;

        return path.StartsWithSegments("/auth/me") ||
               path.StartsWithSegments("/admin") ||
               path.StartsWithSegments("/clients") ||
               path.StartsWithSegments("/gigs") ||
               path.StartsWithSegments("/invoices") ||
               path.StartsWithSegments("/invoice-lines");
    }

    public static string GetAuthenticationFailureCode(Exception? exception)
    {
        var message = exception?.GetBaseException().Message ?? string.Empty;

        if (message.Contains("inactive", StringComparison.OrdinalIgnoreCase))
        {
            return "inactive_user";
        }

        if (message.Contains("subject", StringComparison.OrdinalIgnoreCase))
        {
            return "missing_subject";
        }

        if (message.Contains("email address is not verified", StringComparison.OrdinalIgnoreCase))
        {
            return "email_not_verified";
        }

        if (message.Contains("include an email address", StringComparison.OrdinalIgnoreCase))
        {
            return "missing_email";
        }

        if (message.Contains("access", StringComparison.OrdinalIgnoreCase))
        {
            return "not_authorized";
        }

        return "sign_in_failed";
    }

    public static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        foreach (var claim in identity.FindAll(claimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(claimType, value));
    }

    public static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    public static bool? TryGetEmailVerified(ClaimsPrincipal principal)
    {
        var rawValue = principal.FindFirstValue("email_verified");
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (bool.TryParse(rawValue, out var parsed))
        {
            return parsed;
        }

        return rawValue switch
        {
            "1" => true,
            "0" => false,
            _ => null,
        };
    }

    public static string RenderUnauthorizedPage(string failureCode)
    {
        var copy = GetUnauthorizedPageCopy(failureCode);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{{copy.Title}}</title>
                <style>
                    :root {
                        color-scheme: light;
                        --ink: #1f2c35;
                        --muted: #6f706c;
                        --accent: #8c4920;
                        --line: rgba(140, 73, 32, 0.18);
                        --panel: rgba(255, 250, 245, 0.94);
                        --page-top: #f8efe4;
                        --page-bottom: #efe6dd;
                    }

                    * { box-sizing: border-box; }

                    body {
                        margin: 0;
                        min-height: 100vh;
                        display: grid;
                        place-items: center;
                        padding: 24px;
                        font-family: "Instrument Sans", "Avenir Next", "Segoe UI", sans-serif;
                        color: var(--ink);
                        background:
                            radial-gradient(circle at top left, rgba(236, 170, 94, 0.28), transparent 30%),
                            radial-gradient(circle at top right, rgba(37, 90, 122, 0.14), transparent 26%),
                            linear-gradient(180deg, var(--page-top) 0%, var(--page-bottom) 100%);
                    }

                    .card {
                        width: min(100%, 760px);
                        border: 1px solid var(--line);
                        border-radius: 28px;
                        padding: 32px;
                        background:
                            linear-gradient(180deg, rgba(255, 255, 255, 0.96), var(--panel)),
                            rgba(255, 255, 255, 0.84);
                        box-shadow: 0 24px 60px rgba(54, 50, 45, 0.08);
                    }

                    .eyebrow {
                        margin: 0 0 14px;
                        font-size: 0.75rem;
                        letter-spacing: 0.18em;
                        text-transform: uppercase;
                        color: var(--accent);
                    }

                    h1 {
                        margin: 0 0 14px;
                        font-family: "Fraunces", Georgia, serif;
                        font-size: clamp(2rem, 4vw, 3.2rem);
                        line-height: 0.98;
                        letter-spacing: -0.04em;
                        max-width: 12ch;
                    }

                    p {
                        margin: 0;
                        max-width: 56ch;
                        font-size: 1rem;
                        line-height: 1.6;
                        color: var(--muted);
                    }

                    .note {
                        margin-top: 22px;
                        padding: 16px 18px;
                        border-radius: 18px;
                        background: rgba(140, 73, 32, 0.08);
                        border: 1px solid var(--line);
                        color: var(--ink);
                    }

                    .actions {
                        margin-top: 24px;
                        display: flex;
                        gap: 12px;
                        flex-wrap: wrap;
                    }

                    a {
                        display: inline-flex;
                        align-items: center;
                        justify-content: center;
                        min-height: 46px;
                        padding: 0 18px;
                        border-radius: 999px;
                        text-decoration: none;
                        font-weight: 600;
                    }

                    .primary {
                        color: white;
                        background: linear-gradient(135deg, #1b3f61, #255a7a);
                        box-shadow: 0 18px 30px rgba(33, 74, 107, 0.22);
                    }

                    .secondary {
                        color: var(--ink);
                        background: rgba(255, 255, 255, 0.88);
                        border: 1px solid rgba(34, 42, 52, 0.08);
                    }

                    @media (max-width: 640px) {
                        .card {
                            padding: 24px;
                            border-radius: 22px;
                        }

                        .actions {
                            flex-direction: column;
                        }

                        a {
                            width: 100%;
                        }
                    }
                </style>
            </head>
            <body>
                <main class="card">
                    <p class="eyebrow">{{copy.Eyebrow}}</p>
                    <h1>{{copy.Title}}</h1>
                    <p>{{copy.Body}}</p>
                    <p class="note">{{copy.Note}}</p>
                    <div class="actions">
                        <a class="primary" href="/auth/login">Try again</a>
                        <a class="secondary" href="/">Back to Glovelly</a>
                    </div>
                </main>
            </body>
            </html>
            """;
    }

    private static UnauthorizedPageCopy GetUnauthorizedPageCopy(string failureCode)
    {
        return failureCode switch
        {
            "inactive_user" => new UnauthorizedPageCopy(
                "User Inactive",
                "Your Glovelly user is currently inactive.",
                "Authentication succeeded, but this Glovelly user has been disabled and cannot sign in right now.",
                "Ask an admin to re-enable your user, then try signing in again."),
            "missing_subject" => new UnauthorizedPageCopy(
                "Sign-In Issue",
                "Glovelly could not verify this Google identity.",
                "Google signed you in, but the subject identifier needed to match your Glovelly user was missing from the response.",
                "If this happens repeatedly, it is likely a configuration issue rather than a permissions issue."),
            "missing_email" => new UnauthorizedPageCopy(
                "Sign-In Issue",
                "Google did not provide a usable email address.",
                "Glovelly can only bootstrap a pre-provisioned account when Google returns an email claim.",
                "Check the Google account and OpenID scopes, then try signing in again."),
            "email_not_verified" => new UnauthorizedPageCopy(
                "Email Not Verified",
                "This Google email address is not verified.",
                "Glovelly only allows first-time enrolment binding when Google indicates the email address is verified.",
                "Verify the Google account email first, then try again."),
            "sign_in_failed" => new UnauthorizedPageCopy(
                "Sign-In Issue",
                "Google sign-in did not complete.",
                "The authentication flow was interrupted before Glovelly could create an application session.",
                "Try again first. If it keeps happening, check the Google OIDC configuration and callback settings."),
            _ => new UnauthorizedPageCopy(
                "User Not Authorised",
                "This Google account is not enrolled for Glovelly access.",
                "Authentication succeeded, but Glovelly could not find an active local user linked to this Google account.",
                "Ask an admin to enrol this user in Glovelly, then try signing in again."),
        };
    }

    private sealed record UnauthorizedPageCopy(string Eyebrow, string Title, string Body, string Note);
}
