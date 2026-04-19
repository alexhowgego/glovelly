using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

const string devCorsPolicy = "FrontendDevelopment";
var googleSection = builder.Configuration.GetSection("Authentication:Google");
var googleClientId = googleSection["ClientId"];
var googleClientSecret = googleSection["ClientSecret"];
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var glovellyConnectionString = builder.Configuration.GetConnectionString("Glovelly");
var usePostgres = !string.IsNullOrWhiteSpace(glovellyConnectionString);
var isDevelopment = builder.Environment.IsDevelopment();
var isTesting = builder.Environment.IsEnvironment("Testing");
var shouldSeedDevelopmentData = !usePostgres && !isTesting;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IClaimsTransformation, GoogleOidcClaimsTransformation>();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
    {
        options.UseNpgsql(glovellyConnectionString);
        return;
    }

    options.UseInMemoryDatabase("Glovelly");
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(devCorsPolicy, policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(GlovellyPolicies.GlovellyUser, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(GlovellyClaimTypes.UserId);
    });
    options.AddPolicy(GlovellyPolicies.AdminUser, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(GlovellyClaimTypes.UserId);
        policy.RequireRole(UserRole.Admin.ToString());
    });
});
var authenticationBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/denied";
        options.Cookie.Name = "glovelly.auth";
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var localUserId = context.Principal?.FindFirstValue(GlovellyClaimTypes.UserId);

                if (!Guid.TryParse(localUserId, out var userId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == userId && value.IsActive);

                if (user is null)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            },
            OnRedirectToLogin = context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (IsApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
        };
    });

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authenticationBuilder.AddOpenIdConnect(options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authority = "https://accounts.google.com";
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.CallbackPath = "/signin-oidc";
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
                {
                    context.Fail("Google sign-in did not produce an authenticated identity.");
                    return;
                }

                if (isDevelopment &&
                    context.Properties?.Items.TryGetValue("debug_google_claims", out var debugGoogleClaims) == true &&
                    string.Equals(debugGoogleClaims, "true", StringComparison.OrdinalIgnoreCase))
                {
                    context.HandleResponse();
                    context.Response.ContentType = "application/json; charset=utf-8";

                    var payload = new
                    {
                        sub = principal.FindFirstValue("sub"),
                        email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email),
                        name = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name),
                        claims = principal.Claims
                            .OrderBy(claim => claim.Type, StringComparer.Ordinal)
                            .Select(claim => new
                            {
                                type = claim.Type,
                                value = claim.Value,
                            }),
                    };

                    await context.Response.WriteAsJsonAsync(payload);
                    return;
                }

                var googleSubject = principal.FindFirstValue("sub");
                if (string.IsNullOrWhiteSpace(googleSubject))
                {
                    context.Fail("Google sign-in did not include a subject identifier.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var normalizedGoogleSubject = googleSubject.Trim();
                var normalizedEmail = NormalizeEmail(
                    principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email));
                var emailVerified = TryGetEmailVerified(principal);

                var user = await dbContext.Users.FirstOrDefaultAsync(value => value.GoogleSubject == normalizedGoogleSubject);

                if (user is null)
                {
                    if (string.IsNullOrWhiteSpace(normalizedEmail))
                    {
                        context.Fail("Google sign-in did not include an email address.");
                        return;
                    }

                    if (emailVerified == false)
                    {
                        context.Fail("Google sign-in email address is not verified.");
                        return;
                    }

                    user = await dbContext.Users.FirstOrDefaultAsync(value =>
                        value.GoogleSubject == null &&
                        value.Email == normalizedEmail);

                    if (user is null)
                    {
                        context.Fail("You do not have access to Glovelly.");
                        return;
                    }

                    if (!user.IsActive)
                    {
                        context.Fail("Your Glovelly account is inactive.");
                        return;
                    }

                    user.GoogleSubject = normalizedGoogleSubject;
                }

                if (!user.IsActive)
                {
                    context.Fail("Your Glovelly account is inactive.");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                    !string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal))
                {
                    user.Email = normalizedEmail;
                }

                var displayName = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name);
                if (!string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(user.DisplayName))
                {
                    user.DisplayName = displayName.Trim();
                }

                user.LastLoginUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();

                ReplaceClaim(identity, GlovellyClaimTypes.UserId, user.Id.ToString());
                ReplaceClaim(identity, ClaimTypes.NameIdentifier, user.Id.ToString());
                ReplaceClaim(identity, ClaimTypes.Email, user.Email);
                ReplaceClaim(identity, "email", user.Email);
                ReplaceClaim(identity, ClaimTypes.Role, user.Role.ToString());
                ReplaceClaim(identity, "role", user.Role.ToString());
                ReplaceClaim(identity, ClaimTypes.Name, user.DisplayName ?? user.Email);
            },
            OnRemoteFailure = context =>
            {
                context.HandleResponse();

                var failureCode = GetAuthenticationFailureCode(context.Failure);
                var redirectUri = BuildSafeRedirectUri(
                    context.HttpContext,
                    $"/auth/denied?code={Uri.EscapeDataString(failureCode)}");

                context.Response.Redirect(redirectUri);
                return Task.CompletedTask;
            },
        };
    });
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (shouldSeedDevelopmentData)
    {
        await AppDbSeeder.SeedAsync(dbContext, builder.Configuration);
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();
    app.UseCors(devCorsPolicy);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (IsApiRequest(context.Request))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "Thu, 01 Jan 1970 00:00:00 GMT";
            return Task.CompletedTask;
        });
    }

    await next();
});

var auth = app.MapGroup("/auth").AllowAnonymous();

auth.MapGet("/login", (HttpContext httpContext, string? returnUrl) =>
{
    if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
    {
        return Results.Problem(
            detail: "Google OIDC is not configured. Set Authentication:Google:ClientId and ClientSecret.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var redirectUri = BuildSafeRedirectUri(httpContext, returnUrl);
    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = redirectUri },
        authenticationSchemes: [OpenIdConnectDefaults.AuthenticationScheme]);
});

auth.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

auth.MapGet("/me", [Authorize(Policy = GlovellyPolicies.GlovellyUser)] (ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
{
    return Results.Ok(new
    {
        userId = currentUserAccessor.TryGetUserId(user),
        role = currentUserAccessor.TryGetRole(user)?.ToString(),
        name = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name") ?? "Signed in user",
        email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email") ?? string.Empty,
        profileImageUrl = user.FindFirstValue("picture") ?? user.FindFirstValue("profile") ?? string.Empty,
    });
});

if (app.Environment.IsDevelopment())
{
    auth.MapGet("/debug/google-claims", () =>
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/auth/debug/google-claims",
        };

        properties.Items["debug_google_claims"] = "true";

        return Results.Challenge(
            properties,
            authenticationSchemes: [OpenIdConnectDefaults.AuthenticationScheme]);
    });

    auth.MapGet("/debug/claims", [Authorize] (ClaimsPrincipal user) =>
    {
        var claims = user.Claims
            .OrderBy(claim => claim.Type, StringComparer.Ordinal)
            .Select(claim => new
            {
                type = claim.Type,
                value = claim.Value,
            });

        return Results.Ok(new
        {
            sub = user.FindFirstValue("sub"),
            claims,
        });
    });
}

auth.MapGet("/denied", (string? code) =>
{
    var failureCode = string.IsNullOrWhiteSpace(code) ? "not_authorized" : code;
    var copy = GetUnauthorizedPageCopy(failureCode);

    var html = $$"""
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

    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapCrudEndpoints();
app.MapAdminEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

static string BuildSafeRedirectUri(HttpContext httpContext, string? returnUrl)
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

static bool IsApiRequest(HttpRequest request)
{
    var path = request.Path;

    return path.StartsWithSegments("/auth/me") ||
           path.StartsWithSegments("/admin") ||
           path.StartsWithSegments("/clients") ||
           path.StartsWithSegments("/gigs") ||
           path.StartsWithSegments("/invoices") ||
           path.StartsWithSegments("/invoice-lines");
}

static string GetAuthenticationFailureCode(Exception? exception)
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

static UnauthorizedPageCopy GetUnauthorizedPageCopy(string failureCode)
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

static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
{
    foreach (var claim in identity.FindAll(claimType).ToArray())
    {
        identity.RemoveClaim(claim);
    }

    identity.AddClaim(new Claim(claimType, value));
}

static string? NormalizeEmail(string? email)
{
    return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}

static bool? TryGetEmailVerified(ClaimsPrincipal principal)
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

internal sealed record UnauthorizedPageCopy(string Eyebrow, string Title, string Body, string Note);

public partial class Program;
