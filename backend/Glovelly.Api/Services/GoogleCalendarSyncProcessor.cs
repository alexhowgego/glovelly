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
    ICalendarSyncWorkQueue workQueue,
    ILogger<GoogleCalendarSyncProcessor> logger) : IGoogleCalendarSyncProcessor
{
    public async Task ProcessAsync(CalendarSyncWorkItem workItem, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing Google Calendar sync work item {WorkItemId}. User: {UserId}; Gig: {GigId}; Reason: {Reason}.",
            workItem.Id,
            workItem.UserId,
            workItem.GigId,
            workItem.Reason);

        if (workItem.GigId is null)
        {
            await ProcessFullSyncAsync(workItem, cancellationToken);
            return;
        }

        await ProcessGigSyncAsync(workItem, cancellationToken);
    }

    private async Task ProcessFullSyncAsync(
        CalendarSyncWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var userId = workItem.UserId;
        var settings = await GetEnabledSettingsAsync(userId, cancellationToken);
        if (settings is null)
        {
            logger.LogInformation(
                "Google Calendar full sync work item {WorkItemId} skipped because enabled calendar settings were not found for user {UserId}.",
                workItem.Id,
                userId);
            return;
        }

        settings = await calendarIntegrationService.EnsureCalendarAsync(userId, cancellationToken);
        logger.LogInformation(
            "Google Calendar full sync work item {WorkItemId} using calendar {CalendarId} for user {UserId}.",
            workItem.Id,
            settings.GoogleCalendarId,
            userId);

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
        var enqueuedGigIds = gigIds.Concat(stateGigIds).Distinct().ToList();
        logger.LogInformation(
            "Google Calendar full sync work item {WorkItemId} queued {GigCount} gig sync items. User: {UserId}; Reason: {Reason}.",
            workItem.Id,
            enqueuedGigIds.Count,
            userId,
            workItem.Reason);

        foreach (var gigId in enqueuedGigIds)
        {
            await workQueue.EnqueueGigAsync(userId, gigId, workItem.Reason, cancellationToken);
        }
    }

    private async Task ProcessGigSyncAsync(
        CalendarSyncWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var userId = workItem.UserId;
        var gigId = workItem.GigId!.Value;
        var settings = await GetEnabledSettingsAsync(userId, cancellationToken);
        if (settings is null)
        {
            logger.LogInformation(
                "Google Calendar gig sync work item {WorkItemId} skipped because enabled calendar settings were not found. User: {UserId}; Gig: {GigId}.",
                workItem.Id,
                userId,
                gigId);
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
            logger.LogInformation(
                "Google Calendar gig sync work item {WorkItemId} found no local gig. User: {UserId}; Gig: {GigId}; HasSyncState: {HasSyncState}; ProviderCalendar: {ProviderCalendarId}; ProviderEvent: {ProviderEventId}.",
                workItem.Id,
                userId,
                gigId,
                syncState is not null,
                syncState?.ProviderCalendarId,
                syncState?.ProviderEventId);
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
        logger.LogInformation(
            "Google Calendar gig sync work item {WorkItemId} planned {Operation}. User: {UserId}; Gig: {GigId}; Status: {Status}; Calendar: {CalendarId}; HasSyncState: {HasSyncState}; ProviderCalendar: {ProviderCalendarId}; ProviderEvent: {ProviderEventId}; HasLastSyncHash: {HasLastSyncHash}; PayloadHashChanged: {PayloadHashChanged}.",
            workItem.Id,
            plan.Operation,
            userId,
            gigId,
            gig.Status,
            settings.GoogleCalendarId,
            syncState is not null,
            syncState?.ProviderCalendarId,
            syncState?.ProviderEventId,
            !string.IsNullOrWhiteSpace(syncState?.LastSyncHash),
            plan.PayloadHash is not null && !string.Equals(syncState?.LastSyncHash, plan.PayloadHash, StringComparison.Ordinal));
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
        logger.LogInformation(
            "Creating Google Calendar event {EventId} in calendar {CalendarId}. User: {UserId}; Gig: {GigId}.",
            eventId,
            settings.GoogleCalendarId,
            userId,
            gigId);
        var createdEvent = await CreateEventWithCalendarRecoveryAsync(
            userId,
            settings,
            accessToken,
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
        logger.LogInformation(
            "Updating Google Calendar event {EventId} in calendar {CalendarId}. User: {UserId}; Gig: {GigId}.",
            eventId,
            settings.GoogleCalendarId,
            userId,
            gigId);
        var updatedEvent = await UpdateEventWithCalendarRecoveryAsync(
            userId,
            settings,
            accessToken,
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
            logger.LogInformation(
                "Deleting Google Calendar event {EventId} from calendar {CalendarId}. User: {UserId}; Gig: {GigId}.",
                syncState.ProviderEventId,
                syncState.ProviderCalendarId,
                userId,
                syncState.GigId);
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

    private async Task<GoogleCalendarEventResult> CreateEventWithCalendarRecoveryAsync(
        Guid userId,
        GoogleCalendarIntegrationSettings settings,
        string accessToken,
        string eventId,
        CalendarEventPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await calendarApiClient.CreateEventAsync(
                accessToken,
                settings.GoogleCalendarId!,
                eventId,
                payload,
                cancellationToken);
        }
        catch (GoogleCalendarApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning(
                exception,
                "Google Calendar event create returned 404 for event {EventId} in calendar {CalendarId}; recreating calendar. User: {UserId}.",
                eventId,
                settings.GoogleCalendarId,
                userId);
            await RecreateMissingCalendarAsync(userId, settings, cancellationToken);
            accessToken = await GetAccessTokenAsync(userId, cancellationToken);
            logger.LogInformation(
                "Retrying Google Calendar event create for event {EventId} in replacement calendar {CalendarId}. User: {UserId}.",
                eventId,
                settings.GoogleCalendarId,
                userId);
            return await calendarApiClient.CreateEventAsync(
                accessToken,
                settings.GoogleCalendarId!,
                eventId,
                payload,
                cancellationToken);
        }
    }

    private async Task<GoogleCalendarEventResult> UpdateEventWithCalendarRecoveryAsync(
        Guid userId,
        GoogleCalendarIntegrationSettings settings,
        string accessToken,
        string eventId,
        CalendarEventPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await calendarApiClient.UpdateEventAsync(
                accessToken,
                settings.GoogleCalendarId!,
                eventId,
                payload,
                cancellationToken);
        }
        catch (GoogleCalendarApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning(
                exception,
                "Google Calendar event update returned 404 for event {EventId} in calendar {CalendarId}; trying create before calendar recreation. User: {UserId}.",
                eventId,
                settings.GoogleCalendarId,
                userId);
            try
            {
                return await calendarApiClient.CreateEventAsync(
                    accessToken,
                    settings.GoogleCalendarId!,
                    eventId,
                    payload,
                    cancellationToken);
            }
            catch (GoogleCalendarApiException createException) when (createException.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning(
                    createException,
                    "Google Calendar event create-after-update returned 404 for event {EventId} in calendar {CalendarId}; recreating calendar. User: {UserId}.",
                    eventId,
                    settings.GoogleCalendarId,
                    userId);
                await RecreateMissingCalendarAsync(userId, settings, cancellationToken);
                accessToken = await GetAccessTokenAsync(userId, cancellationToken);
                logger.LogInformation(
                    "Retrying Google Calendar event create for event {EventId} in replacement calendar {CalendarId}. User: {UserId}.",
                    eventId,
                    settings.GoogleCalendarId,
                    userId);
                return await calendarApiClient.CreateEventAsync(
                    accessToken,
                    settings.GoogleCalendarId!,
                    eventId,
                    payload,
                    cancellationToken);
            }
        }
    }

    private async Task RecreateMissingCalendarAsync(
        Guid userId,
        GoogleCalendarIntegrationSettings settings,
        CancellationToken cancellationToken)
    {
        var oldCalendarId = settings.GoogleCalendarId;
        logger.LogWarning(
            "Google Calendar {CalendarId} was not found for user {UserId}; recreating the integration calendar.",
            oldCalendarId,
            userId);

        settings.GoogleCalendarId = null;
        settings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        _ = await calendarIntegrationService.EnsureCalendarAsync(userId, cancellationToken);

        if (string.IsNullOrWhiteSpace(oldCalendarId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var statesToInvalidate = await dbContext.GigCalendarSyncStates
            .Where(state =>
                state.UserId == userId &&
                state.Provider == CalendarProvider.GoogleCalendar)
            .ToListAsync(cancellationToken);

        foreach (var state in statesToInvalidate)
        {
            state.ProviderCalendarId = null;
            state.ProviderEventId = null;
            state.LastSyncHash = null;
            state.DeletedAtUtc = null;
            state.UpdatedAtUtc = now;
        }

        if (statesToInvalidate.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await workQueue.EnqueueFullSyncAsync(userId, CalendarSyncWorkItemReason.CalendarRecreated, cancellationToken);
    }
}
