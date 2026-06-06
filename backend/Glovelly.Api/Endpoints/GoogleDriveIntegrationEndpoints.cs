using System.Security.Claims;
using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GoogleDriveIntegrationEndpoints
{
    private const string GoogleAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string StateProtectionPurpose = "Glovelly.GoogleDriveOAuthState";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapGoogleDriveIntegrationEndpoints(
        this IEndpointRouteBuilder app,
        StartupSettings settings)
    {
        var googleDrive = app.MapGroup("/integrations/google-drive")
            .WithTags("Integrations")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        googleDrive.MapGet("/connect", async (
            HttpContext httpContext,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            IDataProtectionProvider dataProtectionProvider) =>
        {
            if (string.IsNullOrWhiteSpace(settings.GoogleClientId) ||
                string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
            {
                return Results.Problem(
                    detail: "Google OAuth is not configured. Set Authentication:Google:ClientId and ClientSecret.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var currentUserId = await GetActiveCurrentUserIdAsync(
                principal,
                currentUserAccessor,
                dbContext);
            if (!currentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var state = CreateStateToken(currentUserId.Value, dataProtectionProvider);
            var authorizationUrl = QueryHelpers.AddQueryString(
                GoogleAuthorizationEndpoint,
                new Dictionary<string, string?>
                {
                    ["client_id"] = settings.GoogleClientId,
                    ["redirect_uri"] = BuildCallbackUri(httpContext),
                    ["response_type"] = "code",
                    ["scope"] = GoogleScopes.Join(
                        GoogleScopes.OpenId,
                        GoogleScopes.Email,
                        GoogleScopes.Profile,
                        GoogleScopes.DriveFile),
                    ["access_type"] = "offline",
                    ["prompt"] = "consent",
                    ["state"] = state,
                });

            return Results.Redirect(authorizationUrl);
        });

        googleDrive.MapGet("/callback", async (
            HttpContext httpContext,
            string? code,
            string? state,
            string? error,
            string? error_description,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            IDataProtectionProvider dataProtectionProvider,
            IGoogleOAuthTokenClient tokenClient,
            IGoogleConnectionService googleConnectionService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = await GetActiveCurrentUserIdAsync(
                principal,
                currentUserAccessor,
                dbContext);
            if (!currentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var stateValidationErrors = ValidateState(state, currentUserId.Value, dataProtectionProvider);
            if (stateValidationErrors.Count > 0)
            {
                return Results.ValidationProblem(stateValidationErrors);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Problem(
                    title: "Google Drive connection was not approved.",
                    detail: string.IsNullOrWhiteSpace(error_description) ? error : error_description,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var validationErrors = ValidateCallback(code);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var logger = loggerFactory.CreateLogger(nameof(GoogleDriveIntegrationEndpoints));
            logger.LogInformation(
                "Received Google Drive OAuth callback for user {UserId}.",
                currentUserId.Value);

            if (string.IsNullOrWhiteSpace(settings.GoogleClientId) ||
                string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
            {
                return Results.Problem(
                    detail: "Google OAuth is not configured. Set Authentication:Google:ClientId and ClientSecret.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var tokenResponse = await tokenClient.ExchangeCodeAsync(
                code!,
                BuildCallbackUri(httpContext),
                settings.GoogleClientId,
                settings.GoogleClientSecret,
                cancellationToken);

            if (settings.IsDevelopment)
            {
                logger.LogInformation(
                    "Google Drive OAuth token response ({StatusCode}): {TokenResponse}",
                    tokenResponse.StatusCode,
                    tokenResponse.ResponseBody);
            }

            if (!tokenResponse.IsSuccess)
            {
                return Results.Problem(
                    title: "Google Drive token exchange failed.",
                    detail: settings.IsDevelopment
                        ? tokenResponse.ResponseBody
                        : "Google rejected the Drive authorization code.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            if (tokenResponse.TokenResponse is null ||
                string.IsNullOrWhiteSpace(tokenResponse.TokenResponse.AccessToken))
            {
                return Results.Problem(
                    title: "Google Drive token exchange failed.",
                    detail: "Google token response did not include an access token.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            if (!GoogleScopes.Contains(tokenResponse.TokenResponse.Scope, GoogleScopes.DriveFile))
            {
                return EndpointSupport.ValidationProblem("scope", "Google Drive authorization did not grant the required Drive scope.");
            }

            var connection = await googleConnectionService.SaveConnectionAsync(
                currentUserId.Value,
                tokenResponse.TokenResponse,
                cancellationToken);
            await EnsureGoogleDriveIntegrationSettingsAsync(
                dbContext,
                currentUserId.Value,
                connection.Id,
                cancellationToken);

            return Results.Redirect(BuildIntegrationStatusRedirectUri(settings));
        });

        googleDrive.MapPost("/disconnect", async (
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = await GetActiveCurrentUserIdAsync(
                principal,
                currentUserAccessor,
                dbContext);
            if (!currentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var connection = await dbContext.GoogleConnections
                .FirstOrDefaultAsync(value => value.UserId == currentUserId.Value, cancellationToken);
            if (connection is not null)
            {
                connection.RevokedAtUtc = now;
                connection.EncryptedAccessToken = string.Empty;
                connection.EncryptedRefreshToken = string.Empty;
                connection.UpdatedAtUtc = now;
            }

            var driveSettings = await dbContext.GoogleDriveIntegrationSettings
                .FirstOrDefaultAsync(value => value.UserId == currentUserId.Value, cancellationToken);
            if (driveSettings is not null)
            {
                driveSettings.InvoiceUploadFolderId = null;
                driveSettings.UpdatedAtUtc = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        return app;
    }

    private static async Task<Guid?> GetActiveCurrentUserIdAsync(
        ClaimsPrincipal principal,
        ICurrentUserAccessor currentUserAccessor,
        AppDbContext dbContext)
    {
        var currentUserId = currentUserAccessor.TryGetUserId(principal);
        if (!currentUserId.HasValue)
        {
            return null;
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == currentUserId.Value && user.IsActive);

        return userExists ? currentUserId.Value : null;
    }

    private static string BuildCallbackUri(HttpContext httpContext)
    {
        var request = httpContext.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}/integrations/google-drive/callback";
    }

    private static string BuildIntegrationStatusRedirectUri(StartupSettings settings)
    {
        const string integrationStatusPath = "/?integration=google-drive&status=callback-received";

        if (!settings.IsDevelopment || settings.AllowedCorsOrigins.Length == 0)
        {
            return integrationStatusPath;
        }

        var frontendOrigin = settings.AllowedCorsOrigins
            .FirstOrDefault(origin => origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? settings.AllowedCorsOrigins[0];

        return $"{frontendOrigin.TrimEnd('/')}{integrationStatusPath}";
    }

    private static async Task EnsureGoogleDriveIntegrationSettingsAsync(
        AppDbContext dbContext,
        Guid userId,
        Guid googleConnectionId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var driveSettings = await dbContext.GoogleDriveIntegrationSettings
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        if (driveSettings is null)
        {
            driveSettings = new GoogleDriveIntegrationSettings
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GoogleConnectionId = googleConnectionId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            dbContext.GoogleDriveIntegrationSettings.Add(driveSettings);
        }
        else
        {
            driveSettings.GoogleConnectionId = googleConnectionId;
            driveSettings.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CreateStateToken(
        Guid userId,
        IDataProtectionProvider dataProtectionProvider)
    {
        var protector = dataProtectionProvider
            .CreateProtector(StateProtectionPurpose)
            .ToTimeLimitedDataProtector();
        var state = new GoogleDriveOAuthState(userId, DateTime.UtcNow);

        return protector.Protect(
            JsonSerializer.Serialize(state, JsonOptions),
            lifetime: TimeSpan.FromMinutes(15));
    }

    private static Dictionary<string, string[]> ValidateState(
        string? state,
        Guid expectedUserId,
        IDataProtectionProvider dataProtectionProvider)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return new Dictionary<string, string[]>
            {
                ["state"] = ["Google Drive OAuth state is required."]
            };
        }

        try
        {
            var protector = dataProtectionProvider
                .CreateProtector(StateProtectionPurpose)
                .ToTimeLimitedDataProtector();
            var payloadJson = protector.Unprotect(state, out _);
            var payload = JsonSerializer.Deserialize<GoogleDriveOAuthState>(payloadJson, JsonOptions);

            if (payload?.UserId == expectedUserId)
            {
                return [];
            }
        }
        catch
        {
            // Invalid state is expected when a callback is forged, expired, or belongs to another app instance.
        }

        return new Dictionary<string, string[]>
        {
            ["state"] = ["Google Drive OAuth state is invalid or expired."]
        };
    }

    private static Dictionary<string, string[]> ValidateCallback(string? code)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["code"] = ["Google Drive authorization code is required."];
        }

        return errors;
    }

    private sealed record GoogleDriveOAuthState(Guid UserId, DateTime CreatedUtc);
}
