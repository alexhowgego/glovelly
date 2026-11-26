using System.Net.Http.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class EntitlementServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-15T09:00:00Z");

    [Theory]
    [InlineData("2026-03-31T22:59:59Z", "2026-03-01T00:00:00Z", "2026-03-31T23:00:00Z")]
    [InlineData("2026-03-31T23:00:00Z", "2026-03-31T23:00:00Z", "2026-04-30T23:00:00Z")]
    [InlineData("2026-10-25T09:00:00Z", "2026-09-30T23:00:00Z", "2026-11-01T00:00:00Z")]
    [InlineData("2026-12-31T23:59:59Z", "2026-12-01T00:00:00Z", "2027-01-01T00:00:00Z")]
    public void MonthUsesLondonMidnightIncludingDst(string now, string start, string end)
    {
        var month = UkCalendarMonth.ForInstant(DateTimeOffset.Parse(now));
        Assert.Equal(DateTimeOffset.Parse(start), month.StartsAtUtc);
        Assert.Equal(DateTimeOffset.Parse(end), month.ResetsAtUtc);
    }

    [Fact]
    public async Task UnassignedSuperAdministratorDefaultsToFree()
    {
        await using var db = await CreateDatabaseAsync();
        var id = Guid.NewGuid();
        db.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = id, PermissionSet = AdministrativePermissionSet.SuperAdministrator,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var snapshot = await new EntitlementService(db, new Clock(Now)).GetAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(Plan.Free, snapshot.EffectivePlan.Code);
        Assert.Null(snapshot.AssignedPlan);
        Assert.Equal("Default", snapshot.AssignmentStatus);
        Assert.False(snapshot.Features.PaidIncomeSummary);
        Assert.Equal(2, snapshot.InvoiceIssuance.Remaining);
    }

    [Theory]
    [InlineData(SubscriptionState.Active, -1, "Expired")]
    [InlineData(SubscriptionState.Active, 0, "Expired")]
    [InlineData(SubscriptionState.Cancelled, 1, "Cancelled")]
    public async Task ExpiryAndCancellationFallBackToFreeWithoutReset(
        SubscriptionState state, int secondsToEnd, string expectedStatus)
    {
        await using var db = await CreateDatabaseAsync();
        var id = Guid.NewGuid();
        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = id, PlanCode = Plan.Pro, State = state,
            StartsAtUtc = Now.AddDays(-10), EndsAtUtc = Now.AddSeconds(secondsToEnd),
        });
        AddUsage(db, id, Now.AddDays(-1), 10);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var snapshot = await new EntitlementService(db, new Clock(Now)).GetAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(Plan.Free, snapshot.EffectivePlan.Code);
        Assert.Equal(Plan.Pro, snapshot.AssignedPlan!.Code);
        Assert.Equal(expectedStatus, snapshot.AssignmentStatus);
        Assert.Equal(10, snapshot.InvoiceIssuance.Used);
        Assert.Equal(0, snapshot.InvoiceIssuance.Remaining);
    }

    [Fact]
    public async Task UsageRemainsContinuousAcrossRepeatedPlanChanges()
    {
        await using var db = await CreateDatabaseAsync();
        var id = Guid.NewGuid();
        var grant = new UserSubscription { UserId = id, PlanCode = Plan.Pro, StartsAtUtc = Now.AddDays(-10) };
        db.UserSubscriptions.Add(grant);
        AddUsage(db, id, Now.AddDays(-1), 10);
        AddUsage(db, Guid.NewGuid(), Now, 40);
        var period = UkCalendarMonth.ForInstant(Now);
        AddUsage(db, id, period.StartsAtUtc.AddTicks(-1), 20);
        AddUsage(db, id, period.ResetsAtUtc, 20);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new EntitlementService(db, new Clock(Now));
        foreach (var code in new[] { Plan.Pro, Plan.Free, Plan.Solo, Plan.Free })
        {
            grant.PlanCode = code;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var snapshot = await service.GetAsync(id, TestContext.Current.CancellationToken);
            Assert.Equal(10, snapshot.InvoiceIssuance.Used);
            if (code == Plan.Free)
            {
                Assert.Equal(0, snapshot.InvoiceIssuance.Remaining);
            }
            else
            {
                Assert.Null(snapshot.InvoiceIssuance.Limit);
                Assert.Null(snapshot.InvoiceIssuance.Remaining);
                Assert.True(snapshot.Features.PaidIncomeSummary);
            }
        }
        Assert.Empty(await db.UserAdministrativePermissionSets.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExpiryIsObservedWithoutNewSessionOrScheduledJob()
    {
        await using var db = await CreateDatabaseAsync();
        var id = Guid.NewGuid();
        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = id, PlanCode = Plan.Pro, StartsAtUtc = Now.AddDays(-1), EndsAtUtc = Now.AddSeconds(1),
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var clock = new Clock(Now);
        var service = new EntitlementService(db, clock);
        Assert.Equal(Plan.Pro, (await service.GetAsync(id, TestContext.Current.CancellationToken)).EffectivePlan.Code);
        clock.Now = Now.AddSeconds(1);
        Assert.Equal(Plan.Free, (await service.GetAsync(id, TestContext.Current.CancellationToken)).EffectivePlan.Code);
    }

    [Fact]
    public async Task SelfServiceEndpointIsScopedAndDoesNotExposeAssignmentReason()
    {
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.UserSubscriptions.SingleAsync(TestContext.Current.CancellationToken)).Reason = "private operator note";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var response = await client.GetAsync("/entitlements/me", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl!.NoStore);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("private operator note", json);
        var snapshot = await response.Content.ReadFromJsonAsync<EntitlementSnapshot>(TestContext.Current.CancellationToken);
        Assert.Equal(Plan.Pro, snapshot!.EffectivePlan.Code);
        Assert.Null(snapshot.InvoiceIssuance.Limit);
    }

    private static void AddUsage(AppDbContext db, Guid userId, DateTimeOffset occurred, int quantity) =>
        db.UsageLedgerEntries.Add(new UsageLedgerEntry
        {
            Id = Guid.NewGuid(), UserId = userId, SourceInvoiceId = Guid.NewGuid(),
            OccurredAtUtc = occurred, Quantity = quantity, Source = "Test",
        });

    private static async Task<AppDbContext> CreateDatabaseAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return db;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
