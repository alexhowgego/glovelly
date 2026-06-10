using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class GoogleCalendarIntegrationService(
    AppDbContext dbContext,
    IGoogleConnectionService googleConnectionService,
    IGoogleCalendarApiClient calendarApiClient) : IGoogleCalendarIntegrationService
{
    private const string DefaultCalendarName = "Glovelly Gigs";

    public async Task<GoogleCalendarIntegrationSettings> EnsureCalendarAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var connection = await googleConnectionService.GetActiveConnectionAsync(
            userId,
            [GoogleScopes.CalendarAppCreated],
            cancellationToken)
            ?? throw new InvalidOperationException("Google Calendar is not connected.");

        var now = DateTimeOffset.UtcNow;
        var settings = await dbContext.GoogleCalendarIntegrationSettings
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        if (settings is null)
        {
            settings = new GoogleCalendarIntegrationSettings
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GoogleConnectionId = connection.Id,
                CalendarName = DefaultCalendarName,
                IsEnabled = true,
                IncludeLocation = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            dbContext.GoogleCalendarIntegrationSettings.Add(settings);
        }
        else
        {
            settings.GoogleConnectionId = connection.Id;
            settings.DisconnectedAtUtc = null;
            settings.IsEnabled = true;
            settings.UpdatedAtUtc = now;
        }

        if (!string.IsNullOrWhiteSpace(settings.GoogleCalendarId))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return settings;
        }

        var accessToken = await googleConnectionService.GetAccessTokenAsync(
            connection,
            [GoogleScopes.CalendarAppCreated],
            cancellationToken);
        var createResult = await calendarApiClient.CreateCalendarAsync(
            accessToken.AccessToken,
            string.IsNullOrWhiteSpace(settings.CalendarName) ? DefaultCalendarName : settings.CalendarName,
            cancellationToken);

        settings.GoogleCalendarId = createResult.Id;
        settings.CalendarName = string.IsNullOrWhiteSpace(createResult.Summary)
            ? settings.CalendarName
            : createResult.Summary;
        settings.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return settings;
    }

    public async Task InvalidateSyncHashesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var syncStates = await dbContext.GigCalendarSyncStates
            .Where(state =>
                state.UserId == userId &&
                state.Provider == CalendarProvider.GoogleCalendar &&
                state.LastSyncHash != null)
            .ToListAsync(cancellationToken);

        foreach (var state in syncStates)
        {
            state.LastSyncHash = null;
            state.UpdatedAtUtc = now;
        }

        if (syncStates.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
