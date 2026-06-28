using System.Security.Claims;
using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GoogleSheetsIntegrationEndpoints
{
    private const string GoogleAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string StateProtectionPurpose = "Glovelly.GoogleSheetsOAuthState";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapGoogleSheetsIntegrationEndpoints(
        this IEndpointRouteBuilder app,
        StartupSettings settings)
    {
        var googleSheets = app.MapGroup("/integrations/google-sheets")
            .WithTags("Integrations")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        googleSheets.MapGet("/connect", async (
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

            var currentUserId = await GetActiveCurrentUserIdAsync(principal, currentUserAccessor, dbContext);
            if (!currentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var authorizationScope = await GoogleIntegrationEndpointSupport.BuildAuthorizationScopeAsync(
                dbContext,
                currentUserId.Value,
                GoogleScopes.SpreadsheetsReadonly);
            var state = CreateStateToken(currentUserId.Value, dataProtectionProvider);
            var authorizationUrl = QueryHelpers.AddQueryString(
                GoogleAuthorizationEndpoint,
                new Dictionary<string, string?>
                {
                    ["client_id"] = settings.GoogleClientId,
                    ["redirect_uri"] = BuildCallbackUri(httpContext),
                    ["response_type"] = "code",
                    ["scope"] = authorizationScope,
                    ["access_type"] = "offline",
                    ["prompt"] = "consent",
                    ["state"] = state,
                });

            return Results.Redirect(authorizationUrl);
        });

        googleSheets.MapGet("/callback", async (
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
            var currentUserId = await GetActiveCurrentUserIdAsync(principal, currentUserAccessor, dbContext);
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
                    title: "Google Sheets connection was not approved.",
                    detail: string.IsNullOrWhiteSpace(error_description) ? error : error_description,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var validationErrors = ValidateCallback(code);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var logger = loggerFactory.CreateLogger(nameof(GoogleSheetsIntegrationEndpoints));
            logger.LogInformation("Received Google Sheets OAuth callback for user {UserId}.", currentUserId.Value);

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
                    "Google Sheets OAuth token response ({StatusCode}): {TokenResponse}",
                    tokenResponse.StatusCode,
                    tokenResponse.ResponseBody);
            }

            if (!tokenResponse.IsSuccess)
            {
                return Results.Problem(
                    title: "Google Sheets token exchange failed.",
                    detail: settings.IsDevelopment
                        ? tokenResponse.ResponseBody
                        : "Google rejected the Sheets authorization code.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            if (tokenResponse.TokenResponse is null ||
                string.IsNullOrWhiteSpace(tokenResponse.TokenResponse.AccessToken))
            {
                return Results.Problem(
                    title: "Google Sheets token exchange failed.",
                    detail: "Google token response did not include an access token.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            if (!GoogleScopes.Contains(tokenResponse.TokenResponse.Scope, GoogleScopes.SpreadsheetsReadonly))
            {
                return EndpointSupport.ValidationProblem("scope", "Google authorization did not grant the required Sheets scope.");
            }

            await googleConnectionService.SaveConnectionAsync(
                currentUserId.Value,
                tokenResponse.TokenResponse,
                cancellationToken);

            return Results.Redirect(BuildIntegrationStatusRedirectUri(settings));
        });

        googleSheets.MapPost("/disconnect", async (
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = await GetActiveCurrentUserIdAsync(principal, currentUserAccessor, dbContext);
            if (!currentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var connection = await dbContext.GoogleConnections
                .FirstOrDefaultAsync(value => value.UserId == currentUserId.Value, cancellationToken);
            if (connection is not null)
            {
                connection.GrantedScopes = GoogleScopes.Remove(connection.GrantedScopes, GoogleScopes.SpreadsheetsReadonly);
                if (string.IsNullOrWhiteSpace(connection.GrantedScopes))
                {
                    connection.RevokedAtUtc = now;
                    connection.EncryptedAccessToken = string.Empty;
                    connection.EncryptedRefreshToken = string.Empty;
                }
                connection.UpdatedAtUtc = now;
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
        return $"{request.Scheme}://{request.Host}{request.PathBase}/integrations/google-sheets/callback";
    }

    private static string BuildIntegrationStatusRedirectUri(StartupSettings settings)
    {
        const string integrationStatusPath = "/?integration=google-sheets&status=callback-received";

        if (!settings.IsDevelopment || settings.AllowedCorsOrigins.Length == 0)
        {
            return integrationStatusPath;
        }

        var frontendOrigin = settings.AllowedCorsOrigins
            .FirstOrDefault(origin => origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? settings.AllowedCorsOrigins[0];

        return $"{frontendOrigin.TrimEnd('/')}{integrationStatusPath}";
    }

    private static string CreateStateToken(Guid userId, IDataProtectionProvider dataProtectionProvider)
    {
        var protector = dataProtectionProvider
            .CreateProtector(StateProtectionPurpose)
            .ToTimeLimitedDataProtector();
        var state = new GoogleSheetsOAuthState(userId, DateTime.UtcNow);

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
                ["state"] = ["Google Sheets OAuth state is required."]
            };
        }

        try
        {
            var protector = dataProtectionProvider
                .CreateProtector(StateProtectionPurpose)
                .ToTimeLimitedDataProtector();
            var payloadJson = protector.Unprotect(state, out _);
            var payload = JsonSerializer.Deserialize<GoogleSheetsOAuthState>(payloadJson, JsonOptions);

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
            ["state"] = ["Google Sheets OAuth state is invalid or expired."]
        };
    }

    private static Dictionary<string, string[]> ValidateCallback(string? code)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["code"] = ["Google Sheets authorization code is required."];
        }

        return errors;
    }

    private sealed record GoogleSheetsOAuthState(Guid UserId, DateTime CreatedUtc);
}
