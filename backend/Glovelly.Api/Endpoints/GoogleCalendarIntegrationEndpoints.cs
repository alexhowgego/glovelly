using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GoogleCalendarIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapGoogleCalendarIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var googleCalendar = app.MapGroup("/integrations/google-calendar")
            .WithTags("Integrations")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

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
                value.Status == CalendarSyncWorkItemStatus.Failed &&
                value.SupersededAtUtc == null);
            var latestError = await dbContext.CalendarSyncWorkItems
                .AsNoTracking()
                .Where(value =>
                    value.UserId == userId.Value &&
                    value.Provider == CalendarProvider.GoogleCalendar &&
                    value.Status == CalendarSyncWorkItemStatus.Failed &&
                    value.SupersededAtUtc == null &&
                    value.LastError != null)
                .OrderByDescending(value => value.UpdatedAtUtc)
                .Select(value => new { value.LastError, value.LastErrorType })
                .FirstOrDefaultAsync();
            var lastError = latestError is not null &&
                latestError.LastErrorType == typeof(GoogleCalendarApiException).FullName &&
                latestError.LastError != GoogleCalendarApiException.InsufficientScopeMessage
                ? GoogleCalendarApiException.GenericCalendarErrorMessage
                : latestError?.LastError;

            return Results.Ok(new
            {
                isConnected = calendarSettings is not null &&
                    calendarSettings.IsEnabled &&
                    calendarSettings.DisconnectedAtUtc == null &&
                    !calendarSettings.RequiresReconnection &&
                    hasRequiredScope,
                isEnabled = calendarSettings?.IsEnabled ?? false,
                hasRequiredScope,
                requiresReconnection = calendarSettings?.RequiresReconnection ?? false,
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
            var now = DateTimeOffset.UtcNow;
            if (calendarSettings is not null)
            {
                calendarSettings.IsEnabled = false;
                calendarSettings.DisconnectedAtUtc = now;
                calendarSettings.RequiresReconnection = false;
                calendarSettings.UpdatedAtUtc = now;
            }

            var connection = await dbContext.GoogleConnections
                .FirstOrDefaultAsync(value => value.UserId == userId.Value, cancellationToken);
            if (connection is not null)
            {
                connection.GrantedScopes = GoogleScopes.Remove(connection.GrantedScopes, GoogleScopes.CalendarAppCreated);
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

}
