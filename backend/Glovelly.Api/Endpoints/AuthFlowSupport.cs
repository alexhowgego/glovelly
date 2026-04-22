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

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
        {
            var request = httpContext.Request;
            var sameHost = string.Equals(absoluteUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
            var localhostRedirect =
                absoluteUri.IsLoopback &&
                (absoluteUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                 absoluteUri.Host.Equals("127.0.0.1"));

            if (sameHost || localhostRedirect)
            {
                return absoluteUri.ToString();
            }
        }

        return returnUrl.StartsWith('/') ? returnUrl : "/";
    }

    public static bool IsApiRequest(HttpRequest request)
    {
        var path = request.Path;

        return path.StartsWithSegments("/auth/me") ||
               path.StartsWithSegments("/admin") ||
               path.StartsWithSegments("/clients") ||
               path.StartsWithSegments("/gigs") ||
               path.StartsWithSegments("/invoices") ||
               path.StartsWithSegments("/invoice-lines") ||
               path.StartsWithSegments("/seller-profile");
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
                "Account Inactive",
                "Your account is currently inactive.",
                "You cannot sign in right now because this account has been turned off.",
                "Ask an admin to re-enable your access, then try again."),
            "missing_subject" => new UnauthorizedPageCopy(
                "Sign-In Issue",
                "We could not complete your sign-in.",
                "Your account could not be matched correctly this time.",
                "Try again. If this keeps happening, ask an admin for help."),
            "missing_email" => new UnauthorizedPageCopy(
                "Sign-In Issue",
                "We could not read your email address.",
                "Glovelly needs an email address to sign you in.",
                "Check the account you used, then try again."),
            "email_not_verified" => new UnauthorizedPageCopy(
                "Email Not Verified",
                "This email address is not verified.",
                "You need a verified email address before you can sign in.",
                "Verify the email address on your account, then try again."),
            "sign_in_failed" => new UnauthorizedPageCopy(
                "Sign-In Issue",
                "Sign-in did not complete.",
                "Something interrupted the sign-in process before it finished.",
                "Try again. If it keeps happening, ask an admin for help."),
            _ => new UnauthorizedPageCopy(
                "Access Needed",
                "This account does not currently have access to Glovelly.",
                "You have signed in successfully, but this account is not set up to use Glovelly yet.",
                "Ask an admin to grant access, then try again."),
        };
    }

    private sealed record UnauthorizedPageCopy(string Eyebrow, string Title, string Body, string Note);
}
