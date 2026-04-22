using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Microsoft.AspNetCore.DataProtection;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

namespace Glovelly.Api.Configuration;

internal static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddGlovellyAuthentication(this IServiceCollection services, StartupSettings settings)
    {
        var authenticationBuilder = services
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
                    OnRedirectToLogin = context => RedirectApiRequestsOrContinue(context, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = context => RedirectApiRequestsOrContinue(context, StatusCodes.Status403Forbidden),
                };
            });

        if (!string.IsNullOrWhiteSpace(settings.GoogleClientId) &&
            !string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
        {
            authenticationBuilder.AddOpenIdConnect(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority = "https://accounts.google.com";
                options.ClientId = settings.GoogleClientId;
                options.ClientSecret = settings.GoogleClientSecret;
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
                    OnTokenValidated = context => HandleTokenValidatedAsync(context, settings.IsDevelopment),
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();

                        var failureCode = AuthFlowSupport.GetAuthenticationFailureCode(context.Failure);
                        string? requestToken = null;
                        context.Properties?.Items.TryGetValue("access_request_token", out requestToken);
                        var deniedPath = AuthFlowSupport.BuildDeniedPath(failureCode, requestToken);
                        var redirectUri = AuthFlowSupport.BuildSafeRedirectUri(
                            context.HttpContext,
                            deniedPath);

                        context.Response.Redirect(redirectUri);
                        return Task.CompletedTask;
                    },
                };
            });
        }

        return services;
    }

    private static async Task HandleTokenValidatedAsync(TokenValidatedContext context, bool isDevelopment)
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
        var normalizedEmail = AuthFlowSupport.NormalizeEmail(
            principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email));
        var emailVerified = AuthFlowSupport.TryGetEmailVerified(principal);

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
                var requestToken = AuthFlowSupport.CreateAccessRequestToken(
                    principal,
                    context.HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>());
                var redirectUri = AuthFlowSupport.BuildSafeRedirectUri(
                    context.HttpContext,
                    AuthFlowSupport.BuildDeniedPath("not_authorized", requestToken));

                context.HandleResponse();
                context.Response.Redirect(redirectUri);
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

        AuthFlowSupport.ReplaceClaim(identity, GlovellyClaimTypes.UserId, user.Id.ToString());
        AuthFlowSupport.ReplaceClaim(identity, ClaimTypes.NameIdentifier, user.Id.ToString());
        AuthFlowSupport.ReplaceClaim(identity, ClaimTypes.Email, user.Email);
        AuthFlowSupport.ReplaceClaim(identity, "email", user.Email);
        AuthFlowSupport.ReplaceClaim(identity, ClaimTypes.Role, user.Role.ToString());
        AuthFlowSupport.ReplaceClaim(identity, "role", user.Role.ToString());
        AuthFlowSupport.ReplaceClaim(identity, ClaimTypes.Name, user.DisplayName ?? user.Email);
    }

    private static Task RedirectApiRequestsOrContinue(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (AuthFlowSupport.IsApiRequest(context.Request))
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
