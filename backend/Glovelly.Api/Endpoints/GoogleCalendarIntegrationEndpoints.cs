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

internal static class GoogleCalendarIntegrationEndpoints
{
    private const string GoogleAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string StateProtectionPurpose = "Glovelly.GoogleCalendarOAuthState";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapGoogleCalendarIntegrationEndpoints(
        this IEndpointRouteBuilder app,
        StartupSettings settings)
    {
        var googleCalendar = app.MapGroup("/integrations/google-calendar")
            .WithTags("Integrations")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        googleCalendar.MapGet("/connect", async (
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
                        GoogleScopes.CalendarAppCreated),
                    ["access_type"] = "offline",
                    ["prompt"] = "consent",
                    ["state"] = state,
                });

            return Results.Redirect(authorizationUrl);
        });

        googleCalendar.MapGet("/callback", async (
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
            IGoogleCalendarIntegrationService calendarIntegrationService,
            ICalendarSyncWorkQueue workQueue,
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
                    title: "Google Calendar connection was not approved.",
                    detail: string.IsNullOrWhiteSpace(error_description) ? error : error_description,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["code"] = ["Google Calendar authorization code is required."]
                });
            }

            if (string.IsNullOrWhiteSpace(settings.GoogleClientId) ||
                string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
            {
                return Results.Problem(
                    detail: "Google OAuth is not configured. Set Authentication:Google:ClientId and ClientSecret.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var tokenResponse = await tokenClient.ExchangeCodeAsync(
                code,
                BuildCallbackUri(httpContext),
                settings.GoogleClientId,
                settings.GoogleClientSecret,
                cancellationToken);
            if (!tokenResponse.IsSuccess ||
                tokenResponse.TokenResponse is null ||
                string.IsNullOrWhiteSpace(tokenResponse.TokenResponse.AccessToken))
            {
                return Results.Problem(
                    title: "Google Calendar token exchange failed.",
                    detail: settings.IsDevelopment ? tokenResponse.ResponseBody : "Google rejected the Calendar authorization code.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            if (!GoogleScopes.Contains(tokenResponse.TokenResponse.Scope, GoogleScopes.CalendarAppCreated))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["scope"] = ["Google Calendar authorization did not grant the required Calendar scope."]
                });
            }

            _ = await googleConnectionService.SaveConnectionAsync(
                currentUserId.Value,
                tokenResponse.TokenResponse,
                cancellationToken);
            _ = await calendarIntegrationService.EnsureCalendarAsync(currentUserId.Value, cancellationToken);
            await workQueue.EnqueueFullSyncAsync(
                currentUserId.Value,
                CalendarSyncWorkItemReason.ConnectionChanged,
                cancellationToken);

            return Results.Redirect(BuildIntegrationStatusRedirectUri(settings));
        });

        googleCalendar.MapGet("/status", async (
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext) =>
        {
            var userId = currentUserAccessor.TryGetUserId(principal);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var connection = await dbContext.GoogleConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(value =>
                    value.UserId == userId.Value &&
                    value.RevokedAtUtc == null &&
                    (value.RefreshTokenExpiresAtUtc == null || value.RefreshTokenExpiresAtUtc > now));
            var hasRequiredScope = connection is not null &&
                GoogleScopes.Contains(connection.GrantedScopes, GoogleScopes.CalendarAppCreated);
            var calendarSettings = await dbContext.GoogleCalendarIntegrationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.UserId == userId.Value);
            var pendingWorkCount = await dbContext.CalendarSyncWorkItems.CountAsync(value =>
                value.UserId == userId.Value &&
                value.Provider == CalendarProvider.GoogleCalendar &&
                value.Status == CalendarSyncWorkItemStatus.Pending);
            var failedWorkCount = await dbContext.CalendarSyncWorkItems.CountAsync(value =>
                value.UserId == userId.Value &&
                value.Provider == CalendarProvider.GoogleCalendar &&
                value.Status == CalendarSyncWorkItemStatus.Failed);
            var lastError = await dbContext.CalendarSyncWorkItems
                .AsNoTracking()
                .Where(value =>
                    value.UserId == userId.Value &&
                    value.Provider == CalendarProvider.GoogleCalendar &&
                    value.LastError != null)
                .OrderByDescending(value => value.UpdatedAtUtc)
                .Select(value => value.LastError)
                .FirstOrDefaultAsync();

            return Results.Ok(new
            {
                isConnected = calendarSettings is not null &&
                    calendarSettings.IsEnabled &&
                    calendarSettings.DisconnectedAtUtc == null &&
                    hasRequiredScope,
                isEnabled = calendarSettings?.IsEnabled ?? false,
                hasRequiredScope,
                calendarId = calendarSettings?.GoogleCalendarId,
                calendarName = calendarSettings?.CalendarName,
                lastSuccessfulSyncAtUtc = calendarSettings?.LastSuccessfulSyncAtUtc,
                pendingWorkCount,
                failedWorkCount,
                lastError,
            });
        });

        googleCalendar.MapPost("/disconnect", async (
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(principal);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var calendarSettings = await dbContext.GoogleCalendarIntegrationSettings
                .FirstOrDefaultAsync(value => value.UserId == userId.Value, cancellationToken);
            if (calendarSettings is not null)
            {
                var now = DateTimeOffset.UtcNow;
                calendarSettings.IsEnabled = false;
                calendarSettings.DisconnectedAtUtc = now;
                calendarSettings.UpdatedAtUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

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
        return $"{request.Scheme}://{request.Host}{request.PathBase}/integrations/google-calendar/callback";
    }

    private static string BuildIntegrationStatusRedirectUri(StartupSettings settings)
    {
        const string integrationStatusPath = "/?integration=google-calendar&status=callback-received";

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

        return protector.Protect(
            JsonSerializer.Serialize(new GoogleCalendarOAuthState(userId, DateTime.UtcNow), JsonOptions),
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
                ["state"] = ["Google Calendar OAuth state is required."]
            };
        }

        try
        {
            var protector = dataProtectionProvider
                .CreateProtector(StateProtectionPurpose)
                .ToTimeLimitedDataProtector();
            var payloadJson = protector.Unprotect(state, out _);
            var payload = JsonSerializer.Deserialize<GoogleCalendarOAuthState>(payloadJson, JsonOptions);
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
            ["state"] = ["Google Calendar OAuth state is invalid or expired."]
        };
    }

    private sealed record GoogleCalendarOAuthState(Guid UserId, DateTime CreatedUtc);
}
