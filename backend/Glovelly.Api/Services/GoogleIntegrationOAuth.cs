using System.Security.Claims;
using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Endpoints;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

// Only these server-owned integrations can be selected by a protected OAuth state.
internal static class GoogleIntegrationOAuth
{
    internal sealed record Integration(string Id, string Name, string ScopeLabel, string Scope);
    internal static readonly Integration Sheets = new("google-sheets", "Google Sheets", "Sheets", GoogleScopes.SpreadsheetsReadonly);
    internal static readonly Integration Docs = new("google-docs", "Google Docs", "Docs", GoogleScopes.DocumentsReadonly);
    internal static readonly Integration Drive = new("google-drive", "Google Drive", "Drive", GoogleScopes.DriveFile);
    internal static readonly Integration Calendar = new("google-calendar", "Google Calendar", "Calendar", GoogleScopes.CalendarAppCreated);
    private const string StatePurpose = "Glovelly.GoogleIntegrationOAuthState";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record OAuthState(Guid UserId, string IntegrationId);

    internal static async Task<IResult> ConnectAsync(StartupSettings settings, Integration integration, ClaimsPrincipal principal,
        ICurrentUserAccessor users, AppDbContext db, IDataProtectionProvider protection, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(settings.GoogleClientId) || string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
            return ConfigurationError();
        var userId = await ActiveUserIdAsync(principal, users, db, token);
        if (!userId.HasValue) return Results.Unauthorized();
        var scope = await GoogleIntegrationEndpointSupport.BuildAuthorizationScopeAsync(db, userId.Value, integration.Scope, token);
        var state = protection.CreateProtector(StatePurpose).ToTimeLimitedDataProtector().Protect(
            JsonSerializer.Serialize(new OAuthState(userId.Value, integration.Id), JsonOptions), TimeSpan.FromMinutes(15));
        return Results.Redirect(QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
        {
            ["client_id"] = settings.GoogleClientId, ["redirect_uri"] = CallbackUri(settings), ["response_type"] = "code",
            ["scope"] = scope, ["access_type"] = "offline", ["prompt"] = "consent", ["state"] = state,
        }));
    }

    internal static async Task<IResult> CallbackAsync(StartupSettings settings, string? code, string? state, string? error,
        string? errorDescription, ClaimsPrincipal principal, ICurrentUserAccessor users, AppDbContext db,
        IDataProtectionProvider protection, IGoogleOAuthTokenClient tokenClient, IGoogleConnectionService connections,
        IGoogleCalendarIntegrationService calendar, ICalendarSyncWorkQueue workQueue, ILoggerFactory loggerFactory, CancellationToken token)
    {
        var userId = await ActiveUserIdAsync(principal, users, db, token);
        if (!userId.HasValue) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(state)) return EndpointSupport.ValidationProblem("state", "Google OAuth state is required.");
        var integration = ValidateState(state, userId.Value, protection);
        if (integration is null) return EndpointSupport.ValidationProblem("state", "Google OAuth state is invalid or expired.");
        if (!string.IsNullOrWhiteSpace(error))
            return Results.Problem(title: $"{integration.Name} connection was not approved.",
                detail: string.IsNullOrWhiteSpace(errorDescription) ? error : errorDescription, statusCode: StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(code)) return EndpointSupport.ValidationProblem("code", $"{integration.Name} authorization code is required.");
        if (string.IsNullOrWhiteSpace(settings.GoogleClientId) || string.IsNullOrWhiteSpace(settings.GoogleClientSecret)) return ConfigurationError();

        loggerFactory.CreateLogger(nameof(GoogleIntegrationOAuth)).LogInformation("Received {Integration} OAuth callback for user {UserId}.", integration.Name, userId.Value);
        var response = await tokenClient.ExchangeCodeAsync(code, CallbackUri(settings), settings.GoogleClientId, settings.GoogleClientSecret, token);
        if (!response.IsSuccess)
            return Results.Problem(title: $"{integration.Name} token exchange failed.",
                detail: settings.IsDevelopment ? response.ResponseBody : $"Google rejected the {integration.Name} authorization code.",
                statusCode: StatusCodes.Status502BadGateway);
        if (response.TokenResponse is null || string.IsNullOrWhiteSpace(response.TokenResponse.AccessToken))
            return Results.Problem(title: $"{integration.Name} token exchange failed.", detail: "Google token response did not include an access token.", statusCode: StatusCodes.Status502BadGateway);
        if (!GoogleScopes.Contains(response.TokenResponse.Scope, integration.Scope))
            return EndpointSupport.ValidationProblem("scope", $"Google authorization did not grant the required {integration.ScopeLabel} scope.");
        var connection = await connections.SaveConnectionAsync(userId.Value, response.TokenResponse, token);
        await GoogleIntegrationPostConnect.CompleteAsync(integration, userId.Value, connection.Id, db, calendar, workQueue, token);
        return Results.Redirect(StatusRedirectUri(settings, integration));
    }

    internal static async Task<IResult> DisconnectAsync(Integration integration, ClaimsPrincipal principal, ICurrentUserAccessor users,
        AppDbContext db, CancellationToken token)
    {
        var userId = await ActiveUserIdAsync(principal, users, db, token);
        if (!userId.HasValue) return Results.Unauthorized();
        var connection = await db.GoogleConnections.FirstOrDefaultAsync(value => value.UserId == userId.Value, token);
        if (connection is not null)
        {
            var now = DateTimeOffset.UtcNow;
            connection.GrantedScopes = GoogleScopes.Remove(connection.GrantedScopes, integration.Scope);
            if (string.IsNullOrWhiteSpace(connection.GrantedScopes))
            {
                connection.RevokedAtUtc = now;
                connection.EncryptedAccessToken = string.Empty;
                connection.EncryptedRefreshToken = string.Empty;
            }
            connection.UpdatedAtUtc = now;
            await db.SaveChangesAsync(token);
        }
        return Results.NoContent();
    }

    private static Integration? ValidateState(string state, Guid userId, IDataProtectionProvider protection)
    {
        try
        {
            var json = protection.CreateProtector(StatePurpose).ToTimeLimitedDataProtector().Unprotect(state, out _);
            var payload = JsonSerializer.Deserialize<OAuthState>(json, JsonOptions);
            if (payload?.UserId != userId) return null;
            return payload.IntegrationId switch { "google-sheets" => Sheets, "google-docs" => Docs, "google-drive" => Drive, "google-calendar" => Calendar, _ => null };
        }
        catch { return null; } // Invalid, expired, or otherwise unprotectable state.
    }

    private static async Task<Guid?> ActiveUserIdAsync(ClaimsPrincipal principal, ICurrentUserAccessor users, AppDbContext db, CancellationToken token)
    {
        var userId = users.TryGetUserId(principal);
        return userId.HasValue && await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId.Value && user.IsActive, token) ? userId : null;
    }

    private static string CallbackUri(StartupSettings settings) => settings.BuildPublicUrl("/integrations/google/callback");
    private static IResult ConfigurationError() => Results.Problem(detail: "Google OAuth is not configured. Set Authentication:Google:ClientId and ClientSecret.", statusCode: StatusCodes.Status500InternalServerError);
    private static string StatusRedirectUri(StartupSettings settings, Integration integration)
    {
        var path = $"/?integration={integration.Id}&status=callback-received";
        if (!settings.IsDevelopment || settings.AllowedCorsOrigins.Length == 0) return path;
        var origin = settings.AllowedCorsOrigins.FirstOrDefault(value => value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) ?? settings.AllowedCorsOrigins[0];
        return $"{origin.TrimEnd('/')}{path}";
    }
}
