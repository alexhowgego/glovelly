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
            var googleDriveConnection = await dbContext.GoogleDriveConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(connection =>
                    connection.UserId == userId.Value &&
                    connection.RevokedAtUtc == null &&
                    (connection.RefreshTokenExpiresAtUtc == null ||
                     connection.RefreshTokenExpiresAtUtc > now));
            var isGoogleDriveConnected = googleDriveConnection is not null;

            return Results.Ok(new
            {
                userId,
                role = localUser.Role.ToString(),
                name = localUser.DisplayName ?? localUser.Email,
                email = localUser.Email,
                profileImageUrl = user.FindFirstValue("picture") ?? user.FindFirstValue("profile") ?? string.Empty,
                mileageRate = localUser.MileageRate,
                passengerMileageRate = localUser.PassengerMileageRate,
                defaultPaymentWindowDays = localUser.DefaultPaymentWindowDays,
                invoiceFilenamePattern = localUser.InvoiceFilenamePattern,
                invoiceEmailSubjectPattern = localUser.InvoiceEmailSubjectPattern,
                invoiceReplyToEmail = localUser.InvoiceReplyToEmail,
                invoiceUploadFolderId = googleDriveConnection?.InvoiceUploadFolderId,
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
            localUser.DefaultPaymentWindowDays = request.DefaultPaymentWindowDays;
            localUser.InvoiceFilenamePattern = request.InvoiceFilenamePattern?.Trim();
            localUser.InvoiceEmailSubjectPattern = request.InvoiceEmailSubjectPattern?.Trim();
            localUser.InvoiceReplyToEmail = request.InvoiceReplyToEmail?.Trim();

            var invoiceUploadFolderId = request.InvoiceUploadFolderId?.Trim();
            if (string.IsNullOrEmpty(invoiceUploadFolderId))
            {
                invoiceUploadFolderId = null;
            }

            var now = DateTimeOffset.UtcNow;
            var googleDriveConnection = await dbContext.GoogleDriveConnections
                .FirstOrDefaultAsync(connection =>
                    connection.UserId == userId.Value &&
                    connection.RevokedAtUtc == null &&
                    (connection.RefreshTokenExpiresAtUtc == null ||
                     connection.RefreshTokenExpiresAtUtc > now));
            if (invoiceUploadFolderId is not null && googleDriveConnection is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invoiceUploadFolderId"] = ["Connect Google Drive before setting an invoice upload folder."]
                });
            }

            if (googleDriveConnection is not null)
            {
                googleDriveConnection.InvoiceUploadFolderId = invoiceUploadFolderId;
                googleDriveConnection.UpdatedAtUtc = now;
            }

            await dbContext.SaveChangesAsync();

            return Results.Ok(new
            {
                mileageRate = localUser.MileageRate,
                passengerMileageRate = localUser.PassengerMileageRate,
                defaultPaymentWindowDays = localUser.DefaultPaymentWindowDays,
                invoiceFilenamePattern = localUser.InvoiceFilenamePattern,
                invoiceEmailSubjectPattern = localUser.InvoiceEmailSubjectPattern,
                invoiceReplyToEmail = localUser.InvoiceReplyToEmail,
                invoiceUploadFolderId = googleDriveConnection?.InvoiceUploadFolderId,
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

        if (EndpointSupport.TryValidateInvoiceEmailSubjectPattern(
                request.InvoiceEmailSubjectPattern,
                out var subjectErrors))
        {
            return subjectErrors;
        }

        if (request.InvoiceReplyToEmail is not null && string.IsNullOrWhiteSpace(request.InvoiceReplyToEmail))
        {
            return new Dictionary<string, string[]>
            {
                ["invoiceReplyToEmail"] = ["Reply-to email cannot be empty or whitespace."]
            };
        }

        if (request.InvoiceUploadFolderId is not null && string.IsNullOrWhiteSpace(request.InvoiceUploadFolderId))
        {
            return new Dictionary<string, string[]>
            {
                ["invoiceUploadFolderId"] = ["Google Drive folder ID cannot be empty or whitespace."]
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
        [property: Range(0, 3650, ErrorMessage = "Default payment window must be between 0 and 3650 days.")]
        int? DefaultPaymentWindowDays,
        string? InvoiceFilenamePattern,
        string? InvoiceEmailSubjectPattern,
        [property: EmailAddress(ErrorMessage = "Reply-to email must be a valid email address.")]
        string? InvoiceReplyToEmail,
        [property: StringLength(200, ErrorMessage = "Google Drive folder ID must be 200 characters or fewer.")]
        string? InvoiceUploadFolderId);
}
