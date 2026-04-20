using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, StartupSettings settings)
    {
        var auth = app.MapGroup("/auth").AllowAnonymous();

        auth.MapGet("/login", (HttpContext httpContext, string? returnUrl) =>
        {
            if (string.IsNullOrWhiteSpace(settings.GoogleClientId) ||
                string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
            {
                return Results.Problem(
                    detail: "Google OIDC is not configured. Set Authentication:Google:ClientId and ClientSecret.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var redirectUri = AuthFlowSupport.BuildSafeRedirectUri(httpContext, returnUrl);
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = redirectUri },
                authenticationSchemes: [OpenIdConnectDefaults.AuthenticationScheme]);
        });

        auth.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        auth.MapGet("/me", [Authorize(Policy = GlovellyPolicies.GlovellyUser)] async (
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var settingsResponse = userId.HasValue
                ? await dbContext.Users
                    .AsNoTracking()
                    .Where(value => value.Id == userId.Value)
                    .Select(value => new
                    {
                        value.MileageRate,
                        value.PassengerMileageRate,
                    })
                    .FirstOrDefaultAsync()
                : null;

            return Results.Ok(new
            {
                userId,
                role = currentUserAccessor.TryGetRole(user)?.ToString(),
                name = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name") ?? "Signed in user",
                email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email") ?? string.Empty,
                profileImageUrl = user.FindFirstValue("picture") ?? user.FindFirstValue("profile") ?? string.Empty,
                mileageRate = settingsResponse?.MileageRate,
                passengerMileageRate = settingsResponse?.PassengerMileageRate,
            });
        });

        auth.MapPut("/me/settings", [Authorize(Policy = GlovellyPolicies.GlovellyUser)] async (
            UserSettingsRequest request,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext) =>
        {
            if (request.MileageRate.HasValue && request.MileageRate.Value < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["mileageRate"] = ["Mileage rate cannot be negative."]
                });
            }

            if (request.PassengerMileageRate.HasValue && request.PassengerMileageRate.Value < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["passengerMileageRate"] = ["Passenger mileage rate cannot be negative."]
                });
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var localUser = await dbContext.Users.FirstOrDefaultAsync(value => value.Id == userId.Value);
            if (localUser is null)
            {
                return Results.NotFound();
            }

            localUser.MileageRate = request.MileageRate;
            localUser.PassengerMileageRate = request.PassengerMileageRate;

            await dbContext.SaveChangesAsync();

            return Results.Ok(new
            {
                mileageRate = localUser.MileageRate,
                passengerMileageRate = localUser.PassengerMileageRate,
            });
        });

        if (settings.IsDevelopment)
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
            var html = AuthFlowSupport.RenderUnauthorizedPage(failureCode);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        return app;
    }

    internal sealed record UserSettingsRequest(decimal? MileageRate, decimal? PassengerMileageRate);
}
