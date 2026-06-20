using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class BusinessLifecycleAdvancementTests : IClassFixture<GlovellyApiFactory>
{
    private readonly GlovellyApiFactory _factory;

    public BusinessLifecycleAdvancementTests(GlovellyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BusinessLifecycleTask_WhenNextWorkIsInFuture_Skips()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var stateStore = scope.ServiceProvider.GetRequiredService<IScheduledTaskStateStore>();
        var task = scope.ServiceProvider.GetRequiredService<BusinessLifecycleAdvancementScheduledTask>();
        var now = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

        await stateStore.WriteAsync(new ScheduledTaskStateEnvelope<BusinessLifecycleAdvancementTaskState>
        {
            TaskName = ScheduledTaskNames.BusinessLifecycleAdvancement,
            LastSuccessfulRunUtc = now,
            State = new BusinessLifecycleAdvancementTaskState
            {
                NextGigCompletionDate = new DateOnly(2026, 5, 2),
                NextInvoiceOverdueDate = new DateOnly(2026, 5, 3),
                LastSuccessfulAdvancementUtc = now
            }
        }, TestContext.Current.CancellationToken);

        var decision = await task.ShouldRunAsync(
            new ScheduledTaskContext(now),
            TestContext.Current.CancellationToken);

        Assert.False(decision.ShouldRun);
    }

    [Fact]
    public async Task BusinessLifecycleTask_WhenNextWorkIsDue_Runs()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var stateStore = scope.ServiceProvider.GetRequiredService<IScheduledTaskStateStore>();
        var task = scope.ServiceProvider.GetRequiredService<BusinessLifecycleAdvancementScheduledTask>();
        var now = new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero);

        await stateStore.WriteAsync(new ScheduledTaskStateEnvelope<BusinessLifecycleAdvancementTaskState>
        {
            TaskName = ScheduledTaskNames.BusinessLifecycleAdvancement,
            LastSuccessfulRunUtc = now.AddHours(-1),
            State = new BusinessLifecycleAdvancementTaskState
            {
                NextGigCompletionDate = new DateOnly(2026, 5, 2),
                LastSuccessfulAdvancementUtc = now.AddHours(-1)
            }
        }, TestContext.Current.CancellationToken);

        var decision = await task.ShouldRunAsync(
            new ScheduledTaskContext(now),
            TestContext.Current.CancellationToken);

        Assert.True(decision.ShouldRun);
    }

    [Fact]
    public async Task BusinessLifecycleTask_AdvancesDueGigsAndInvoicesAndRecomputesWakeState()
    {
        _ = _factory.CreateClient();
        var now = new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero);
        var pastGigId = Guid.Parse("11111111-1111-1111-1111-111111111171");
        var futureGigId = Guid.Parse("11111111-1111-1111-1111-111111111172");
        var draftPastGigId = Guid.Parse("11111111-1111-1111-1111-111111111173");
        var pastInvoiceId = Guid.Parse("22222222-2222-2222-2222-222222222271");
        var futureInvoiceId = Guid.Parse("22222222-2222-2222-2222-222222222272");
        var paidPastInvoiceId = Guid.Parse("22222222-2222-2222-2222-222222222273");

        using (var seedScope = _factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.AddRange(
                BuildGig(pastGigId, new DateOnly(2026, 5, 1), GigStatus.Confirmed),
                BuildGig(futureGigId, new DateOnly(2026, 5, 4), GigStatus.Confirmed),
                BuildGig(draftPastGigId, new DateOnly(2026, 5, 1), GigStatus.Draft));
            db.Invoices.AddRange(
                BuildInvoice(pastInvoiceId, "GLV-LIFE-001", new DateOnly(2026, 5, 1), InvoiceStatus.Issued),
                BuildInvoice(futureInvoiceId, "GLV-LIFE-002", new DateOnly(2026, 5, 5), InvoiceStatus.Issued),
                BuildInvoice(paidPastInvoiceId, "GLV-LIFE-003", new DateOnly(2026, 5, 1), InvoiceStatus.Paid));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var runScope = _factory.Services.CreateScope())
        {
            var task = runScope.ServiceProvider.GetRequiredService<BusinessLifecycleAdvancementScheduledTask>();
            var result = await task.ExecuteAsync(
                new BusinessLifecycleAdvancementOptions(),
                new ScheduledTaskContext(now),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.CompletedGigs);
            Assert.Equal(2, result.OverdueInvoices);
            Assert.Equal(new DateOnly(2026, 5, 5), result.NextGigCompletionDate);
            Assert.Equal(new DateOnly(2026, 5, 6), result.NextInvoiceOverdueDate);
        }

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(GigStatus.Completed, await GetGigStatusAsync(assertDb, pastGigId));
        Assert.Equal(GigStatus.Confirmed, await GetGigStatusAsync(assertDb, futureGigId));
        Assert.Equal(GigStatus.Draft, await GetGigStatusAsync(assertDb, draftPastGigId));
        Assert.Equal(InvoiceStatus.Overdue, await GetInvoiceStatusAsync(assertDb, pastInvoiceId));
        Assert.Equal(InvoiceStatus.Issued, await GetInvoiceStatusAsync(assertDb, futureInvoiceId));
        Assert.Equal(InvoiceStatus.Paid, await GetInvoiceStatusAsync(assertDb, paidPastInvoiceId));
        Assert.True(await assertDb.CalendarSyncWorkItems.AnyAsync(
            item => item.GigId == pastGigId && item.Reason == CalendarSyncWorkItemReason.GigUpdated,
            TestContext.Current.CancellationToken));

        var stateStore = assertScope.ServiceProvider.GetRequiredService<IScheduledTaskStateStore>();
        var envelope = await stateStore.ReadAsync<BusinessLifecycleAdvancementTaskState>(
            ScheduledTaskNames.BusinessLifecycleAdvancement,
            TestContext.Current.CancellationToken);
        Assert.NotNull(envelope);
        Assert.Equal(new DateOnly(2026, 5, 5), envelope.State.NextGigCompletionDate);
        Assert.Equal(new DateOnly(2026, 5, 6), envelope.State.NextInvoiceOverdueDate);
        Assert.Equal(now, envelope.State.LastSuccessfulAdvancementUtc);
    }

    [Fact]
    public async Task BusinessLifecycleSignal_TracksEarlierConfirmedGigCandidateOnly()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var signal = scope.ServiceProvider.GetRequiredService<IBusinessLifecycleSignal>();
        var stateStore = scope.ServiceProvider.GetRequiredService<IScheduledTaskStateStore>();
        await stateStore.WriteAsync(new ScheduledTaskStateEnvelope<BusinessLifecycleAdvancementTaskState>
        {
            TaskName = ScheduledTaskNames.BusinessLifecycleAdvancement
        }, TestContext.Current.CancellationToken);

        await signal.TrackGigAsync(
            BuildGig(Guid.NewGuid(), new DateOnly(2026, 5, 10), GigStatus.Confirmed),
            TestContext.Current.CancellationToken);
        await signal.TrackGigAsync(
            BuildGig(Guid.NewGuid(), new DateOnly(2026, 5, 20), GigStatus.Confirmed),
            TestContext.Current.CancellationToken);
        await signal.TrackGigAsync(
            BuildGig(Guid.NewGuid(), new DateOnly(2026, 5, 1), GigStatus.Draft),
            TestContext.Current.CancellationToken);

        var envelope = await stateStore.ReadAsync<BusinessLifecycleAdvancementTaskState>(
            ScheduledTaskNames.BusinessLifecycleAdvancement,
            TestContext.Current.CancellationToken);

        Assert.NotNull(envelope);
        Assert.Equal(new DateOnly(2026, 5, 11), envelope.State.NextGigCompletionDate);
        Assert.Null(envelope.State.NextInvoiceOverdueDate);
    }

    private static Gig BuildGig(Guid id, DateOnly date, GigStatus status)
    {
        return new Gig
        {
            Id = id,
            ClientId = TestData.FoxAndFinchId,
            CreatedByUserId = TestAuthContext.UserId,
            UpdatedByUserId = TestAuthContext.UserId,
            Title = $"Lifecycle gig {id:N}",
            Date = date,
            Venue = "Lifecycle Hall",
            Fee = 100,
            Status = status
        };
    }

    private static Invoice BuildInvoice(Guid id, string invoiceNumber, DateOnly dueDate, InvoiceStatus status)
    {
        return new Invoice
        {
            Id = id,
            InvoiceNumber = invoiceNumber,
            ClientId = TestData.FoxAndFinchId,
            CreatedByUserId = TestAuthContext.UserId,
            UpdatedByUserId = TestAuthContext.UserId,
            InvoiceDate = dueDate.AddDays(-14),
            DueDate = dueDate,
            Status = status,
            Description = "Lifecycle invoice"
        };
    }

    private static async Task<GigStatus> GetGigStatusAsync(AppDbContext dbContext, Guid id)
    {
        return await dbContext.Gigs
            .Where(gig => gig.Id == id)
            .Select(gig => gig.Status)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<InvoiceStatus> GetInvoiceStatusAsync(AppDbContext dbContext, Guid id)
    {
        return await dbContext.Invoices
            .Where(invoice => invoice.Id == id)
            .Select(invoice => invoice.Status)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
