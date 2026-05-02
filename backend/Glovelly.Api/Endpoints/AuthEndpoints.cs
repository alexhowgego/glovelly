using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var localUser = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == userId.Value && value.IsActive);

            if (localUser is null)
            {
                return Results.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var isGoogleDriveConnected = await dbContext.GoogleDriveConnections
                .AsNoTracking()
                .AnyAsync(connection =>
                    connection.UserId == userId.Value &&
                    connection.RevokedAtUtc == null &&
                    (connection.RefreshTokenExpiresAtUtc == null ||
                     connection.RefreshTokenExpiresAtUtc > now));

            return Results.Ok(new
            {
                userId,
                role = localUser.Role.ToString(),
                name = localUser.DisplayName ?? localUser.Email,
                email = localUser.Email,
                profileImageUrl = user.FindFirstValue("picture") ?? user.FindFirstValue("profile") ?? string.Empty,
                mileageRate = localUser.MileageRate,
                passengerMileageRate = localUser.PassengerMileageRate,
                invoiceFilenamePattern = localUser.InvoiceFilenamePattern,
                invoiceReplyToEmail = localUser.InvoiceReplyToEmail,
                isGoogleDriveConnected,
            });
        });

        auth.MapPut("/me/settings", [Authorize(Policy = GlovellyPolicies.GlovellyUser)] async (
            UserSettingsRequest request,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext) =>
        {
            var validationErrors = ValidateUserSettingsRequest(request);
            if (validationErrors is not null)
            {
                return Results.ValidationProblem(validationErrors);
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
            localUser.InvoiceFilenamePattern = request.InvoiceFilenamePattern?.Trim();
            localUser.InvoiceReplyToEmail = request.InvoiceReplyToEmail?.Trim();

            await dbContext.SaveChangesAsync();

            return Results.Ok(new
            {
                mileageRate = localUser.MileageRate,
                passengerMileageRate = localUser.PassengerMileageRate,
                invoiceFilenamePattern = localUser.InvoiceFilenamePattern,
                invoiceReplyToEmail = localUser.InvoiceReplyToEmail,
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

        auth.MapGet("/denied", (HttpContext httpContext, string? code) =>
        {
            var failureCode = string.IsNullOrWhiteSpace(code) ? "not_authorized" : code;
            var accessRequestToken = httpContext.Request.Query["request"].FirstOrDefault();
            var html = AuthFlowSupport.RenderUnauthorizedPage(failureCode, accessRequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        return app;
    }

    private static Dictionary<string, string[]>? ValidateUserSettingsRequest(UserSettingsRequest request)
    {
        if (EndpointSupport.TryValidateInvoiceFilenamePattern(
                request.InvoiceFilenamePattern,
                out var patternErrors))
        {
            return patternErrors;
        }

        if (request.InvoiceReplyToEmail is not null && string.IsNullOrWhiteSpace(request.InvoiceReplyToEmail))
        {
            return new Dictionary<string, string[]>
            {
                ["invoiceReplyToEmail"] = ["Reply-to email cannot be empty or whitespace."]
            };
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);
        var isValid = Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true);
        if (isValid)
        {
            return null;
        }

        return validationResults
            .GroupBy(result => ToCamelCase(result.MemberNames.FirstOrDefault() ?? string.Empty))
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping.Select(result => result.ErrorMessage ?? "Invalid value.").ToArray());
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    internal sealed record UserSettingsRequest(
        [Range(0, double.MaxValue, ErrorMessage = "Mileage rate cannot be negative.")]
        decimal? MileageRate,
        [Range(0, double.MaxValue, ErrorMessage = "Passenger mileage rate cannot be negative.")]
        decimal? PassengerMileageRate,
        string? InvoiceFilenamePattern,
        [property: EmailAddress(ErrorMessage = "Reply-to email must be a valid email address.")]
        string? InvoiceReplyToEmail);
}
