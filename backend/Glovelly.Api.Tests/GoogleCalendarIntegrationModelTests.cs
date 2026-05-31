using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleCalendarIntegrationModelTests : IClassFixture<GlovellyApiFactory>
{
    private readonly GlovellyApiFactory _factory;

    public GoogleCalendarIntegrationModelTests(GlovellyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void GoogleScopes_CalendarCapabilityUsesLeastPrivilegeAppCreatedScope()
    {
        Assert.Equal("https://www.googleapis.com/auth/calendar.app.created", GoogleScopes.CalendarAppCreated);
        Assert.True(GoogleScopes.Contains(GoogleScopes.CalendarAppCreated, GoogleScopes.CalendarAppCreated));
        Assert.False(GoogleScopes.Contains(GoogleScopes.CalendarAppCreated, GoogleScopes.DriveFile));
    }

    [Fact]
    public async Task CalendarSettings_ArePersistedIndependentlyFromTokenStorage()
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenProtector = scope.ServiceProvider.GetRequiredService<IGoogleTokenProtector>();
        var connectionId = Guid.NewGuid();
        dbContext.GoogleConnections.Add(new GoogleConnection
        {
            Id = connectionId,
            UserId = TestAuthContext.UserId,
            EncryptedAccessToken = tokenProtector.Protect("access-token"),
            EncryptedRefreshToken = tokenProtector.Protect("refresh-token"),
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            GrantedScopes = GoogleScopes.CalendarAppCreated,
            TokenType = "Bearer",
            ConnectedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        dbContext.GoogleCalendarIntegrationSettings.Add(new GoogleCalendarIntegrationSettings
        {
            Id = Guid.NewGuid(),
            UserId = TestAuthContext.UserId,
            GoogleConnectionId = connectionId,
            IsEnabled = true,
            GoogleCalendarId = "calendar-id",
            CalendarName = "Glovelly Gigs",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var settings = await dbContext.GoogleCalendarIntegrationSettings.SingleAsync();
        var connection = await dbContext.GoogleConnections.SingleAsync();
        Assert.Equal(connection.Id, settings.GoogleConnectionId);
        Assert.Equal("calendar-id", settings.GoogleCalendarId);
        Assert.NotEqual("access-token", connection.EncryptedAccessToken);
        Assert.Equal("access-token", tokenProtector.Unprotect(connection.EncryptedAccessToken));
        _ = client;
    }

    [Fact]
    public async Task GigCalendarSyncState_PersistsProviderMappingHashAndRetryState()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gig = CreateGig(GigStatus.Confirmed);
        dbContext.Gigs.Add(gig);
        dbContext.GigCalendarSyncStates.Add(new GigCalendarSyncState
        {
            Id = Guid.NewGuid(),
            GigId = gig.Id,
            UserId = TestAuthContext.UserId,
            Provider = CalendarProvider.GoogleCalendar,
            ProviderCalendarId = "calendar-id",
            ProviderEventId = "event-id",
            LastSyncHash = "hash",
            LastSyncedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastSyncAttemptedAtUtc = DateTimeOffset.UtcNow,
            LastSyncError = "temporary provider error",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var state = await dbContext.GigCalendarSyncStates.SingleAsync();
        Assert.Equal(CalendarProvider.GoogleCalendar, state.Provider);
        Assert.Equal("calendar-id", state.ProviderCalendarId);
        Assert.Equal("event-id", state.ProviderEventId);
        Assert.Equal("hash", state.LastSyncHash);
        Assert.Equal("temporary provider error", state.LastSyncError);
        Assert.Null(state.DeletedAtUtc);
    }

    [Fact]
    public void PayloadHash_IsDeterministicAndChangesWhenPayloadChanges()
    {
        var hasher = new CalendarEventPayloadHasher();
        var payload = new CalendarEventPayload(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Gig title",
            new DateOnly(2026, 5, 29),
            new DateOnly(2026, 5, 30),
            "Venue",
            "Description");

        var firstHash = hasher.Hash(payload);
        var secondHash = hasher.Hash(payload);
        var changedHash = hasher.Hash(payload with { Summary = "Changed title" });

        Assert.Equal(firstHash, secondHash);
        Assert.NotEqual(firstHash, changedHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Theory]
    [InlineData(GigStatus.Confirmed, true)]
    [InlineData(GigStatus.Draft, false)]
    [InlineData(GigStatus.Completed, true)]
    [InlineData(GigStatus.Cancelled, false)]
    public void Mapper_SyncsConfirmedAndCompletedGigsByDefault(GigStatus status, bool expectedShouldExist)
    {
        var mapper = new GigCalendarEventMapper();
        var gig = CreateGig(status);
        var settings = CreateSettings();

        Assert.Equal(expectedShouldExist, mapper.ShouldExistInCalendar(gig, settings));
    }

    [Fact]
    public void Mapper_MapsGigsToAllDayEvents()
    {
        var mapper = new GigCalendarEventMapper();
        var gig = CreateGig(GigStatus.Confirmed);
        var client = CreateClient();
        var settings = CreateSettings();

        var payload = mapper.Map(gig, client, settings);

        Assert.Equal(gig.Id, payload.SourceGigId);
        Assert.Equal(gig.Title, payload.Summary);
        Assert.Equal(gig.Date, payload.StartDate);
        Assert.Equal(gig.Date.AddDays(1), payload.EndDate);
        Assert.Equal(gig.Venue, payload.Location);
        Assert.Contains(client.Name, payload.Description);
        Assert.Contains(gig.Id.ToString(), payload.Description);
    }

    [Fact]
    public void Planner_PlansCreateWhenConfirmedGigHasNoProviderEvent()
    {
        var planner = CreatePlanner();
        var gig = CreateGig(GigStatus.Confirmed);
        var client = CreateClient();
        var settings = CreateSettings();

        var plan = planner.Plan(gig, client, settings, syncState: null);

        Assert.Equal(CalendarSyncOperation.Create, plan.Operation);
        Assert.NotNull(plan.Payload);
        Assert.False(string.IsNullOrWhiteSpace(plan.PayloadHash));
    }

    [Fact]
    public void Planner_PlansNoneWhenHashMatchesExistingProviderEvent()
    {
        var mapper = new GigCalendarEventMapper();
        var hasher = new CalendarEventPayloadHasher();
        var planner = new GigCalendarSyncPlanner(mapper, hasher);
        var gig = CreateGig(GigStatus.Confirmed);
        var client = CreateClient();
        var settings = CreateSettings();
        var hash = hasher.Hash(mapper.Map(gig, client, settings));
        var syncState = CreateSyncState(gig.Id, hash, "event-id");

        var plan = planner.Plan(gig, client, settings, syncState);

        Assert.Equal(CalendarSyncOperation.None, plan.Operation);
    }

    [Fact]
    public void Planner_PlansUpdateWhenHashDiffersFromExistingProviderEvent()
    {
        var planner = CreatePlanner();
        var gig = CreateGig(GigStatus.Confirmed);
        var client = CreateClient();
        var settings = CreateSettings();
        var syncState = CreateSyncState(gig.Id, "old-hash", "event-id");

        var plan = planner.Plan(gig, client, settings, syncState);

        Assert.Equal(CalendarSyncOperation.Update, plan.Operation);
    }

    [Fact]
    public void Planner_PlansDeleteWhenGigShouldNoLongerExistButProviderEventExists()
    {
        var planner = CreatePlanner();
        var gig = CreateGig(GigStatus.Cancelled);
        var client = CreateClient();
        var settings = CreateSettings();
        var syncState = CreateSyncState(gig.Id, "old-hash", "event-id");

        var plan = planner.Plan(gig, client, settings, syncState);

        Assert.Equal(CalendarSyncOperation.Delete, plan.Operation);
        Assert.Null(plan.Payload);
        Assert.Null(plan.PayloadHash);
    }

    [Fact]
    public async Task EnsureCalendarAsync_CreatesCalendarWhenNoCalendarIdIsPersisted()
    {
        var calendarClient = new FakeGoogleCalendarApiClient();
        using var factory = CreateFactory(calendarClient);
        _ = factory.CreateClient();
        await SeedCalendarConnectionAsync(factory);

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoogleCalendarIntegrationService>();

        var settings = await service.EnsureCalendarAsync(TestAuthContext.UserId, CancellationToken.None);

        Assert.Equal("google-calendar-id", settings.GoogleCalendarId);
        Assert.Equal("Glovelly Gigs", calendarClient.Summary);
        Assert.Equal("access-token", calendarClient.AccessToken);
    }

    [Fact]
    public async Task EnsureCalendarAsync_ReusesPersistedCalendarIdWithoutCreatingCalendar()
    {
        var calendarClient = new FakeGoogleCalendarApiClient();
        using var factory = CreateFactory(calendarClient);
        _ = factory.CreateClient();
        var connectionId = await SeedCalendarConnectionAsync(factory);

        using (var seedScope = factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleCalendarIntegrationSettings.Add(new GoogleCalendarIntegrationSettings
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                GoogleConnectionId = connectionId,
                IsEnabled = true,
                GoogleCalendarId = "persisted-calendar-id",
                CalendarName = "Glovelly Gigs",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoogleCalendarIntegrationService>();

        var settings = await service.EnsureCalendarAsync(TestAuthContext.UserId, CancellationToken.None);

        Assert.Equal("persisted-calendar-id", settings.GoogleCalendarId);
        Assert.False(calendarClient.CreateWasCalled);
    }

    [Fact]
    public async Task CalendarSyncWorkQueue_EnqueuesPendingGigWork()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<ICalendarSyncWorkQueue>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await queue.EnqueueGigAsync(
            TestAuthContext.UserId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            CalendarSyncWorkItemReason.GigUpdated);

        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Equal(CalendarSyncWorkItemStatus.Pending, workItem.Status);
        Assert.Equal(CalendarSyncWorkItemReason.GigUpdated, workItem.Reason);
        Assert.Equal(CalendarProvider.GoogleCalendar, workItem.Provider);
    }

    [Fact]
    public async Task CalendarSyncWorkQueue_DeduplicatesPendingGigWork()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<ICalendarSyncWorkQueue>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gigId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await queue.EnqueueGigAsync(TestAuthContext.UserId, gigId, CalendarSyncWorkItemReason.GigCreated);
        await queue.EnqueueGigAsync(TestAuthContext.UserId, gigId, CalendarSyncWorkItemReason.GigUpdated);

        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Equal(gigId, workItem.GigId);
        Assert.Equal(CalendarSyncWorkItemReason.GigUpdated, workItem.Reason);
        Assert.Equal(CalendarSyncWorkItemStatus.Pending, workItem.Status);
    }

    [Fact]
    public async Task CalendarSyncWorkQueue_DeduplicatesPendingFullSyncWork()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<ICalendarSyncWorkQueue>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await queue.EnqueueFullSyncAsync(TestAuthContext.UserId, CalendarSyncWorkItemReason.ConnectionChanged);
        await queue.EnqueueFullSyncAsync(TestAuthContext.UserId, CalendarSyncWorkItemReason.ManualSync);

        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Null(workItem.GigId);
        Assert.Equal(CalendarSyncWorkItemReason.ManualSync, workItem.Reason);
        Assert.Equal(CalendarSyncWorkItemStatus.Pending, workItem.Status);
    }

    [Fact]
    public async Task SyncProcessor_CreatesEventAndStoresSyncState()
    {
        var calendarClient = new FakeGoogleCalendarApiClient();
        using var factory = CreateFactory(calendarClient);
        _ = factory.CreateClient();
        var connectionId = await SeedCalendarConnectionAsync(factory);
        var gig = CreateGig(GigStatus.Confirmed);

        using (var seedScope = factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleCalendarIntegrationSettings.Add(new GoogleCalendarIntegrationSettings
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                GoogleConnectionId = connectionId,
                IsEnabled = true,
                GoogleCalendarId = "calendar-id",
                CalendarName = "Glovelly Gigs",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            dbContext.Gigs.Add(gig);
            await dbContext.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IGoogleCalendarSyncProcessor>();
        await processor.ProcessAsync(new CalendarSyncWorkItem
        {
            Id = Guid.NewGuid(),
            UserId = TestAuthContext.UserId,
            GigId = gig.Id,
            Reason = CalendarSyncWorkItemReason.GigCreated,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        var assertionDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncState = await assertionDbContext.GigCalendarSyncStates.SingleAsync();
        Assert.Equal("calendar-id", calendarClient.EventCalendarId);
        Assert.Equal(GoogleCalendarApiClient.BuildDeterministicEventId(gig.Id), calendarClient.EventId);
        Assert.Equal(calendarClient.EventId, syncState.ProviderEventId);
        Assert.False(string.IsNullOrWhiteSpace(syncState.LastSyncHash));
        Assert.Null(syncState.LastSyncError);
    }

    [Fact]
    public async Task SyncProcessor_WhenStoredCalendarIsMissing_RecreatesCalendarAndRetriesCreate()
    {
        var calendarClient = new FakeGoogleCalendarApiClient
        {
            ThrowNotFoundOnNextCreateEvent = true,
            CreatedCalendarId = "replacement-calendar-id",
        };
        using var factory = CreateFactory(calendarClient);
        _ = factory.CreateClient();
        var connectionId = await SeedCalendarConnectionAsync(factory);
        var gig = CreateGig(GigStatus.Confirmed);

        using (var seedScope = factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleCalendarIntegrationSettings.Add(new GoogleCalendarIntegrationSettings
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                GoogleConnectionId = connectionId,
                IsEnabled = true,
                GoogleCalendarId = "deleted-calendar-id",
                CalendarName = "Glovelly Gigs",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            dbContext.Gigs.Add(gig);
            await dbContext.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IGoogleCalendarSyncProcessor>();
        await processor.ProcessAsync(new CalendarSyncWorkItem
        {
            Id = Guid.NewGuid(),
            UserId = TestAuthContext.UserId,
            GigId = gig.Id,
            Reason = CalendarSyncWorkItemReason.GigCreated,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        var assertionDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await assertionDbContext.GoogleCalendarIntegrationSettings.SingleAsync();
        var syncState = await assertionDbContext.GigCalendarSyncStates.SingleAsync();
        Assert.Equal("replacement-calendar-id", settings.GoogleCalendarId);
        Assert.Equal("replacement-calendar-id", syncState.ProviderCalendarId);
        Assert.Equal(["deleted-calendar-id", "replacement-calendar-id"], calendarClient.CreateEventCalendarIds);
    }

    [Fact]
    public async Task SyncProcessor_DeletesEventWhenGigNoLongerShouldSync()
    {
        var calendarClient = new FakeGoogleCalendarApiClient();
        using var factory = CreateFactory(calendarClient);
        _ = factory.CreateClient();
        var connectionId = await SeedCalendarConnectionAsync(factory);
        var gig = CreateGig(GigStatus.Cancelled);

        using (var seedScope = factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.GoogleCalendarIntegrationSettings.Add(new GoogleCalendarIntegrationSettings
            {
                Id = Guid.NewGuid(),
                UserId = TestAuthContext.UserId,
                GoogleConnectionId = connectionId,
                IsEnabled = true,
                GoogleCalendarId = "calendar-id",
                CalendarName = "Glovelly Gigs",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            dbContext.Gigs.Add(gig);
            dbContext.GigCalendarSyncStates.Add(CreateSyncState(gig.Id, "old-hash", "event-id"));
            await dbContext.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IGoogleCalendarSyncProcessor>();
        await processor.ProcessAsync(new CalendarSyncWorkItem
        {
            Id = Guid.NewGuid(),
            UserId = TestAuthContext.UserId,
            GigId = gig.Id,
            Reason = CalendarSyncWorkItemReason.GigCancelled,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        var assertionDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncState = await assertionDbContext.GigCalendarSyncStates.SingleAsync();
        Assert.Equal("event-id", calendarClient.DeletedEventId);
        Assert.NotNull(syncState.DeletedAtUtc);
    }

    [Fact]
    public async Task QueueDrainer_WhenWorkSucceeds_MarksItemSucceededAndReturnsCounts()
    {
        var processor = new FakeGoogleCalendarSyncProcessor();
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        var workItemId = await SeedWorkItemAsync(factory, CalendarSyncWorkItemReason.ManualSync);

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(MaxItems: 5));

        Assert.Equal(new CalendarSyncDrainResult(1, 1, 0, 0, 0, 0), result);
        Assert.Equal(workItemId, processor.ProcessedWorkItemIds.Single());

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Equal(CalendarSyncWorkItemStatus.Succeeded, workItem.Status);
        Assert.Null(workItem.ProcessingOwnerId);
        Assert.Null(workItem.ProcessingStartedAtUtc);
        Assert.NotNull(workItem.LastAttemptedAtUtc);
        Assert.Null(workItem.LastError);
        Assert.Null(workItem.LastErrorType);
        Assert.Null(workItem.LastErrorDetail);
    }

    [Fact]
    public async Task QueueDrainer_WhenWorkFailsBeforeMaxAttempts_SchedulesRetryAndReturnsCounts()
    {
        var processor = new FakeGoogleCalendarSyncProcessor
        {
            ExceptionToThrow = new InvalidOperationException("temporary failure")
        };
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        await SeedWorkItemAsync(factory, CalendarSyncWorkItemReason.ManualSync);

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(MaxItems: 5, OwnerId: "test-owner"));

        Assert.Equal(new CalendarSyncDrainResult(1, 0, 1, 0, 0, 0), result);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Equal(CalendarSyncWorkItemStatus.Pending, workItem.Status);
        Assert.Equal(1, workItem.AttemptCount);
        Assert.Null(workItem.ProcessingOwnerId);
        Assert.Null(workItem.ProcessingStartedAtUtc);
        Assert.NotNull(workItem.LastAttemptedAtUtc);
        Assert.Equal("temporary failure", workItem.LastError);
        Assert.Equal(typeof(InvalidOperationException).FullName, workItem.LastErrorType);
        Assert.Contains("temporary failure", workItem.LastErrorDetail);
        Assert.True(workItem.NextAttemptAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task QueueDrainer_WhenWorkFailsAtMaxAttempts_MarksItemFailedAndReturnsCounts()
    {
        var processor = new FakeGoogleCalendarSyncProcessor
        {
            ExceptionToThrow = new InvalidOperationException("permanent failure")
        };
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        await SeedWorkItemAsync(factory, CalendarSyncWorkItemReason.ManualSync, attemptCount: 4);

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(MaxItems: 5));

        Assert.Equal(new CalendarSyncDrainResult(1, 0, 0, 1, 0, 0), result);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Equal(CalendarSyncWorkItemStatus.Failed, workItem.Status);
        Assert.Equal(5, workItem.AttemptCount);
        Assert.Null(workItem.ProcessingOwnerId);
        Assert.Null(workItem.ProcessingStartedAtUtc);
        Assert.Equal("permanent failure", workItem.LastError);
        Assert.Equal(typeof(InvalidOperationException).FullName, workItem.LastErrorType);
        Assert.Contains("permanent failure", workItem.LastErrorDetail);
    }

    [Fact]
    public async Task QueueDrainer_RespectsMaxItems()
    {
        var processor = new FakeGoogleCalendarSyncProcessor();
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        await SeedWorkItemAsync(factory, CalendarSyncWorkItemReason.ManualSync);
        await SeedWorkItemAsync(factory, CalendarSyncWorkItemReason.GigUpdated, Guid.NewGuid());

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(MaxItems: 1));

        Assert.Equal(new CalendarSyncDrainResult(1, 1, 0, 0, 0, 0), result);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await dbContext.CalendarSyncWorkItems.CountAsync(item => item.Status == CalendarSyncWorkItemStatus.Succeeded));
        Assert.Equal(1, await dbContext.CalendarSyncWorkItems.CountAsync(item => item.Status == CalendarSyncWorkItemStatus.Pending));
    }

    [Fact]
    public async Task QueueDrainer_WhenNoDueWork_ReturnsZeroCounts()
    {
        var processor = new FakeGoogleCalendarSyncProcessor();
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        await SeedWorkItemAsync(
            factory,
            CalendarSyncWorkItemReason.ManualSync,
            nextAttemptAtUtc: DateTimeOffset.UtcNow.AddMinutes(10));

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(MaxItems: 5));

        Assert.Equal(new CalendarSyncDrainResult(0, 0, 0, 0, 0, 0), result);
        Assert.Empty(processor.ProcessedWorkItemIds);
    }

    [Fact]
    public async Task QueueDrainer_RecoversStaleProcessingWorkAndProcessesIt()
    {
        var processor = new FakeGoogleCalendarSyncProcessor();
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        var workItemId = await SeedWorkItemAsync(
            factory,
            CalendarSyncWorkItemReason.ManualSync,
            status: CalendarSyncWorkItemStatus.Processing,
            processingOwnerId: "abandoned-owner",
            processingStartedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-20));

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(
            MaxItems: 5,
            OwnerId: "new-owner",
            ProcessingTimeout: TimeSpan.FromMinutes(10)));

        Assert.Equal(new CalendarSyncDrainResult(1, 1, 0, 0, 0, 1), result);
        Assert.Equal(workItemId, processor.ProcessedWorkItemIds.Single());

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Equal(CalendarSyncWorkItemStatus.Succeeded, workItem.Status);
        Assert.Null(workItem.ProcessingOwnerId);
        Assert.Null(workItem.ProcessingStartedAtUtc);
        Assert.Null(workItem.LastError);
    }

    [Fact]
    public async Task QueueDrainer_DoesNotRecoverNonStaleProcessingWork()
    {
        var processor = new FakeGoogleCalendarSyncProcessor();
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        await SeedWorkItemAsync(
            factory,
            CalendarSyncWorkItemReason.ManualSync,
            status: CalendarSyncWorkItemStatus.Processing,
            processingOwnerId: "active-owner",
            processingStartedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-2));

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(
            MaxItems: 5,
            ProcessingTimeout: TimeSpan.FromMinutes(10)));

        Assert.Equal(new CalendarSyncDrainResult(0, 0, 0, 0, 0, 0), result);
        Assert.Empty(processor.ProcessedWorkItemIds);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workItem = await dbContext.CalendarSyncWorkItems.SingleAsync();
        Assert.Equal(CalendarSyncWorkItemStatus.Processing, workItem.Status);
        Assert.Equal("active-owner", workItem.ProcessingOwnerId);
    }

    [Fact]
    public async Task QueueDrainer_ProcessorSeesClaimedOwnerDuringProcessing()
    {
        var processor = new FakeGoogleCalendarSyncProcessor
        {
            OnProcessAsync = async (serviceProvider, workItemId) =>
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var claimedItem = await dbContext.CalendarSyncWorkItems.SingleAsync(value => value.Id == workItemId);
                Assert.Equal(CalendarSyncWorkItemStatus.Processing, claimedItem.Status);
                Assert.Equal("claim-owner", claimedItem.ProcessingOwnerId);
                Assert.NotNull(claimedItem.ProcessingStartedAtUtc);
                Assert.NotNull(claimedItem.LastAttemptedAtUtc);
            }
        };
        using var factory = CreateFactory(processor);
        _ = factory.CreateClient();
        processor.ServiceProvider = factory.Services;
        await SeedWorkItemAsync(factory, CalendarSyncWorkItemReason.ManualSync);

        using var scope = factory.Services.CreateScope();
        var drainer = scope.ServiceProvider.GetRequiredService<ICalendarSyncQueueDrainer>();
        var result = await drainer.DrainAsync(new CalendarSyncDrainOptions(MaxItems: 5, OwnerId: "claim-owner"));

        Assert.Equal(new CalendarSyncDrainResult(1, 1, 0, 0, 0, 0), result);
    }

    private static IGigCalendarSyncPlanner CreatePlanner()
    {
        return new GigCalendarSyncPlanner(new GigCalendarEventMapper(), new CalendarEventPayloadHasher());
    }

    private static Gig CreateGig(GigStatus status)
    {
        return new Gig
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ClientId = TestData.FoxAndFinchId,
            Title = "Wedding reception",
            Date = new DateOnly(2026, 6, 20),
            Venue = "Town Hall",
            Fee = 250m,
            Notes = "Arrive early.",
            Status = status,
            CreatedByUserId = TestAuthContext.UserId,
            UpdatedByUserId = TestAuthContext.UserId,
        };
    }

    private static Client CreateClient()
    {
        return new Client
        {
            Id = TestData.FoxAndFinchId,
            Name = "Fox & Finch Events",
            Email = "bookings@foxandfinch.co.uk",
        };
    }

    private static GoogleCalendarIntegrationSettings CreateSettings()
    {
        return new GoogleCalendarIntegrationSettings
        {
            Id = Guid.NewGuid(),
            UserId = TestAuthContext.UserId,
            GoogleConnectionId = Guid.NewGuid(),
            IsEnabled = true,
            GoogleCalendarId = "calendar-id",
            CalendarName = "Glovelly Gigs",
            IncludeLocation = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static GigCalendarSyncState CreateSyncState(Guid gigId, string lastSyncHash, string providerEventId)
    {
        return new GigCalendarSyncState
        {
            Id = Guid.NewGuid(),
            GigId = gigId,
            UserId = TestAuthContext.UserId,
            Provider = CalendarProvider.GoogleCalendar,
            ProviderCalendarId = "calendar-id",
            ProviderEventId = providerEventId,
            LastSyncHash = lastSyncHash,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private WebApplicationFactory<Program> CreateFactory(FakeGoogleCalendarApiClient calendarClient)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "google-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-client-secret");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleCalendarApiClient>();
                services.AddSingleton<IGoogleCalendarApiClient>(calendarClient);
            });
        });
    }

    private WebApplicationFactory<Program> CreateFactory(FakeGoogleCalendarSyncProcessor processor)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleCalendarSyncProcessor>();
                services.AddSingleton<IGoogleCalendarSyncProcessor>(processor);
            });
        });
    }

    private static async Task<Guid> SeedWorkItemAsync(
        WebApplicationFactory<Program> factory,
        CalendarSyncWorkItemReason reason,
        Guid? gigId = null,
        int attemptCount = 0,
        DateTimeOffset? nextAttemptAtUtc = null,
        CalendarSyncWorkItemStatus status = CalendarSyncWorkItemStatus.Pending,
        string? processingOwnerId = null,
        DateTimeOffset? processingStartedAtUtc = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.CalendarSyncWorkItems.Add(new CalendarSyncWorkItem
        {
            Id = workItemId,
            UserId = TestAuthContext.UserId,
            GigId = gigId,
            Provider = CalendarProvider.GoogleCalendar,
            Reason = reason,
            Status = status,
            AttemptCount = attemptCount,
            NextAttemptAtUtc = nextAttemptAtUtc ?? now,
            ProcessingOwnerId = processingOwnerId,
            ProcessingStartedAtUtc = processingStartedAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await dbContext.SaveChangesAsync();

        return workItemId;
    }

    private static async Task<Guid> SeedCalendarConnectionAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenProtector = scope.ServiceProvider.GetRequiredService<IGoogleTokenProtector>();
        var connectionId = Guid.NewGuid();
        dbContext.GoogleConnections.Add(new GoogleConnection
        {
            Id = connectionId,
            UserId = TestAuthContext.UserId,
            EncryptedAccessToken = tokenProtector.Protect("access-token"),
            EncryptedRefreshToken = tokenProtector.Protect("refresh-token"),
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            GrantedScopes = GoogleScopes.CalendarAppCreated,
            TokenType = "Bearer",
            ConnectedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return connectionId;
    }

    private sealed class FakeGoogleCalendarApiClient : IGoogleCalendarApiClient
    {
        public bool CreateWasCalled { get; private set; }
        public bool ThrowNotFoundOnNextCreateEvent { get; set; }
        public string CreatedCalendarId { get; set; } = "google-calendar-id";
        public string? AccessToken { get; private set; }
        public string? Summary { get; private set; }
        public string? EventCalendarId { get; private set; }
        public string? EventId { get; private set; }
        public string? DeletedEventId { get; private set; }
        public List<string> CreateEventCalendarIds { get; } = [];

        public Task<GoogleCalendarCreateResult> CreateCalendarAsync(
            string accessToken,
            string summary,
            CancellationToken cancellationToken)
        {
            CreateWasCalled = true;
            AccessToken = accessToken;
            Summary = summary;

            return Task.FromResult(new GoogleCalendarCreateResult(CreatedCalendarId, summary));
        }

        public Task<GoogleCalendarEventResult> CreateEventAsync(
            string accessToken,
            string calendarId,
            string eventId,
            CalendarEventPayload payload,
            CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            EventCalendarId = calendarId;
            EventId = eventId;
            CreateEventCalendarIds.Add(calendarId);
            if (ThrowNotFoundOnNextCreateEvent)
            {
                ThrowNotFoundOnNextCreateEvent = false;
                throw new GoogleCalendarApiException(
                    "Google Calendar event creation failed with HTTP 404. Not Found",
                    System.Net.HttpStatusCode.NotFound,
                    "Not Found");
            }

            return Task.FromResult(new GoogleCalendarEventResult(eventId));
        }

        public Task<GoogleCalendarEventResult> UpdateEventAsync(
            string accessToken,
            string calendarId,
            string eventId,
            CalendarEventPayload payload,
            CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            EventCalendarId = calendarId;
            EventId = eventId;
            return Task.FromResult(new GoogleCalendarEventResult(eventId));
        }

        public Task DeleteEventAsync(
            string accessToken,
            string calendarId,
            string eventId,
            CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            EventCalendarId = calendarId;
            DeletedEventId = eventId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGoogleCalendarSyncProcessor : IGoogleCalendarSyncProcessor
    {
        public List<Guid> ProcessedWorkItemIds { get; } = [];
        public Exception? ExceptionToThrow { get; set; }
        public IServiceProvider? ServiceProvider { get; set; }
        public Func<IServiceProvider, Guid, Task>? OnProcessAsync { get; set; }

        public async Task ProcessAsync(CalendarSyncWorkItem workItem, CancellationToken cancellationToken)
        {
            ProcessedWorkItemIds.Add(workItem.Id);
            if (OnProcessAsync is not null && ServiceProvider is not null)
            {
                await OnProcessAsync(ServiceProvider, workItem.Id);
            }

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }
        }
    }
}
