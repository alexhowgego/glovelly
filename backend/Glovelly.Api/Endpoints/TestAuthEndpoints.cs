using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Glovelly.Api.Endpoints;

internal static class TestAuthEndpoints
{
    private const string SecretHeaderName = "X-Glovelly-Uat-Secret";

    public static IEndpointRouteBuilder MapTestAuthEndpoints(this IEndpointRouteBuilder app, StartupSettings settings)
    {
        if (!settings.IsStaging)
        {
            return app;
        }

        var group = app.MapGroup("/test-auth").AllowAnonymous();

        group.MapPost("/login", async (
            HttpContext httpContext,
            AppDbContext dbContext,
            IConfiguration configuration,
            string? returnUrl) =>
        {
            var suppliedSecret = httpContext.Request.Headers[SecretHeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(suppliedSecret))
            {
                return Results.Unauthorized();
            }

            var configuredSecret = configuration["GLOVELLY_UAT_SECRET"] ?? configuration["Uat:Secret"];
            if (string.IsNullOrWhiteSpace(configuredSecret))
            {
                return Results.Problem(
                    detail: "Staging UAT authentication is not configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!SecretsMatch(suppliedSecret, configuredSecret))
            {
                return Results.Forbid();
            }

            await UatRegressionDataSeeder.SeedAsync(dbContext);

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstAsync(value => value.Id == UatRegressionDataSeeder.UserId && value.IsActive);

            var claims = new[]
            {
                new Claim(GlovellyClaimTypes.UserId, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("email", user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("role", user.Role.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
                new Claim("name", user.DisplayName ?? user.Email),
                new Claim("sub", user.GoogleSubject ?? UatRegressionDataSeeder.GoogleSubject),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    IssuedUtc = DateTimeOffset.UtcNow,
                });

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return Results.Redirect(AuthFlowSupport.BuildSafeRedirectUri(httpContext, returnUrl));
            }

            return Results.Ok(new
            {
                userId = user.Id,
                email = user.Email,
                name = user.DisplayName,
                role = user.Role.ToString(),
            });
        });

        return app;
    }

    private static bool SecretsMatch(string suppliedSecret, string configuredSecret)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedSecret);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredSecret);

        return suppliedBytes.Length == configuredBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }
}
