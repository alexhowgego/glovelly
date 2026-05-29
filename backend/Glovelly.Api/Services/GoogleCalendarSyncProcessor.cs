using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class GoogleCalendarSyncProcessor(
    AppDbContext dbContext,
    IGoogleCalendarIntegrationService calendarIntegrationService,
    IGoogleConnectionService googleConnectionService,
    IGoogleCalendarApiClient calendarApiClient,
    IGigCalendarSyncPlanner syncPlanner,
    ICalendarSyncWorkQueue workQueue) : IGoogleCalendarSyncProcessor
{
    public async Task ProcessAsync(CalendarSyncWorkItem workItem, CancellationToken cancellationToken)
    {
        if (workItem.GigId is null)
        {
            await ProcessFullSyncAsync(workItem.UserId, workItem.Reason, cancellationToken);
            return;
        }

        await ProcessGigSyncAsync(workItem.UserId, workItem.GigId.Value, cancellationToken);
    }

    private async Task ProcessFullSyncAsync(
        Guid userId,
        CalendarSyncWorkItemReason reason,
        CancellationToken cancellationToken)
    {
        var settings = await GetEnabledSettingsAsync(userId, cancellationToken);
        if (settings is null)
        {
            return;
        }

        _ = await calendarIntegrationService.EnsureCalendarAsync(userId, cancellationToken);

        var gigIds = await dbContext.Gigs
            .AsNoTracking()
            .Where(gig => gig.CreatedByUserId == userId)
            .Select(gig => gig.Id)
            .ToListAsync(cancellationToken);
        var stateGigIds = await dbContext.GigCalendarSyncStates
            .AsNoTracking()
            .Where(state => state.UserId == userId && state.Provider == CalendarProvider.GoogleCalendar && state.GigId != null)
            .Select(state => state.GigId!.Value)
            .ToListAsync(cancellationToken);

        foreach (var gigId in gigIds.Concat(stateGigIds).Distinct())
        {
            await workQueue.EnqueueGigAsync(userId, gigId, reason, cancellationToken);
        }
    }

    private async Task ProcessGigSyncAsync(
        Guid userId,
        Guid gigId,
        CancellationToken cancellationToken)
    {
        var settings = await GetEnabledSettingsAsync(userId, cancellationToken);
        if (settings is null)
        {
            return;
        }

        settings = await calendarIntegrationService.EnsureCalendarAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.GoogleCalendarId))
        {
            throw new InvalidOperationException("Google Calendar ID is missing.");
        }

        var syncState = await dbContext.GigCalendarSyncStates
            .SingleOrDefaultAsync(
                state => state.GigId == gigId && state.Provider == CalendarProvider.GoogleCalendar,
                cancellationToken);
        var gig = await dbContext.Gigs
            .AsNoTracking()
            .Include(value => value.Client)
            .FirstOrDefaultAsync(value => value.Id == gigId && value.CreatedByUserId == userId, cancellationToken);

        if (gig is null)
        {
            if (syncState is not null)
            {
                await DeleteProviderEventAsync(userId, settings, syncState, cancellationToken);
            }

            return;
        }

        if (gig.Client is null)
        {
            throw new InvalidOperationException("Gig client is missing.");
        }

        var plan = syncPlanner.Plan(gig, gig.Client, settings, syncState);
        switch (plan.Operation)
        {
            case CalendarSyncOperation.None:
                await RecordNoOpAsync(userId, gigId, settings, syncState, cancellationToken);
                break;
            case CalendarSyncOperation.Create:
                await CreateProviderEventAsync(userId, gigId, settings, plan, cancellationToken);
                break;
            case CalendarSyncOperation.Update:
                await UpdateProviderEventAsync(userId, gigId, settings, plan, cancellationToken);
                break;
            case CalendarSyncOperation.Delete:
                if (syncState is not null)
                {
                    await DeleteProviderEventAsync(userId, settings, syncState, cancellationToken);
                }
                break;
        }
    }

    private async Task<GoogleCalendarIntegrationSettings?> GetEnabledSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.GoogleCalendarIntegrationSettings
            .FirstOrDefaultAsync(settings =>
                settings.UserId == userId &&
                settings.IsEnabled &&
                settings.DisconnectedAtUtc == null,
                cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await googleConnectionService.GetActiveConnectionAsync(
            userId,
            [GoogleScopes.CalendarAppCreated],
            cancellationToken)
            ?? throw new InvalidOperationException("Google Calendar is not connected.");
        var accessToken = await googleConnectionService.GetAccessTokenAsync(
            connection,
            [GoogleScopes.CalendarAppCreated],
            cancellationToken);

        return accessToken.AccessToken;
    }

    private async Task RecordNoOpAsync(
        Guid userId,
        Guid gigId,
        GoogleCalendarIntegrationSettings settings,
        GigCalendarSyncState? syncState,
        CancellationToken cancellationToken)
    {
        if (syncState is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        syncState.LastSyncAttemptedAtUtc = now;
        syncState.LastSyncError = null;
        syncState.UpdatedAtUtc = now;
        settings.LastSuccessfulSyncAtUtc = now;
        settings.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateProviderEventAsync(
        Guid userId,
        Guid gigId,
        GoogleCalendarIntegrationSettings settings,
        CalendarSyncPlan plan,
        CancellationToken cancellationToken)
    {
        var payload = plan.Payload ?? throw new InvalidOperationException("Calendar create payload is missing.");
        var payloadHash = plan.PayloadHash ?? throw new InvalidOperationException("Calendar create hash is missing.");
        var eventId = GoogleCalendarApiClient.BuildDeterministicEventId(gigId);
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken);
        var createdEvent = await calendarApiClient.CreateEventAsync(
            accessToken,
            settings.GoogleCalendarId!,
            eventId,
            payload,
            cancellationToken);

        var syncState = plan.SyncState ?? new GigCalendarSyncState
        {
            Id = Guid.NewGuid(),
            GigId = gigId,
            UserId = userId,
            Provider = CalendarProvider.GoogleCalendar,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        if (plan.SyncState is null)
        {
            dbContext.GigCalendarSyncStates.Add(syncState);
        }

        RecordSuccess(settings, syncState, settings.GoogleCalendarId!, createdEvent.Id, payloadHash, deleted: false);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateProviderEventAsync(
        Guid userId,
        Guid gigId,
        GoogleCalendarIntegrationSettings settings,
        CalendarSyncPlan plan,
        CancellationToken cancellationToken)
    {
        var syncState = plan.SyncState ?? throw new InvalidOperationException("Calendar update state is missing.");
        var payload = plan.Payload ?? throw new InvalidOperationException("Calendar update payload is missing.");
        var payloadHash = plan.PayloadHash ?? throw new InvalidOperationException("Calendar update hash is missing.");
        var eventId = string.IsNullOrWhiteSpace(syncState.ProviderEventId)
            ? GoogleCalendarApiClient.BuildDeterministicEventId(gigId)
            : syncState.ProviderEventId;
        var accessToken = await GetAccessTokenAsync(userId, cancellationToken);
        var updatedEvent = await calendarApiClient.UpdateEventAsync(
            accessToken,
            settings.GoogleCalendarId!,
            eventId,
            payload,
            cancellationToken);

        RecordSuccess(settings, syncState, settings.GoogleCalendarId!, updatedEvent.Id, payloadHash, deleted: false);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteProviderEventAsync(
        Guid userId,
        GoogleCalendarIntegrationSettings settings,
        GigCalendarSyncState syncState,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(syncState.ProviderEventId) &&
            !string.IsNullOrWhiteSpace(syncState.ProviderCalendarId))
        {
            var accessToken = await GetAccessTokenAsync(userId, cancellationToken);
            await calendarApiClient.DeleteEventAsync(
                accessToken,
                syncState.ProviderCalendarId,
                syncState.ProviderEventId,
                cancellationToken);
        }

        syncState.DeletedAtUtc = now;
        syncState.LastSyncedAtUtc = now;
        syncState.LastSyncAttemptedAtUtc = now;
        syncState.LastSyncError = null;
        syncState.UpdatedAtUtc = now;
        settings.LastSuccessfulSyncAtUtc = now;
        settings.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void RecordSuccess(
        GoogleCalendarIntegrationSettings settings,
        GigCalendarSyncState syncState,
        string calendarId,
        string eventId,
        string payloadHash,
        bool deleted)
    {
        var now = DateTimeOffset.UtcNow;
        syncState.ProviderCalendarId = calendarId;
        syncState.ProviderEventId = eventId;
        syncState.LastSyncHash = payloadHash;
        syncState.LastSyncedAtUtc = now;
        syncState.LastSyncAttemptedAtUtc = now;
        syncState.LastSyncError = null;
        syncState.DeletedAtUtc = deleted ? now : null;
        syncState.UpdatedAtUtc = now;
        settings.LastSuccessfulSyncAtUtc = now;
        settings.UpdatedAtUtc = now;
    }
}
