using System.Net.Http.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class LicenceSummaryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T09:00:00Z");

    [Fact]
    public async Task EmptyDirectoryHasGenuineZeroCounts()
    {
        await using var db = MemoryContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new LicenceSummary(0, 0, 0),
            await new EntitlementService(db, new Clock()).GetLicenceSummaryAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CountsUseEffectiveAccessAndThirtyDayExpiryAcrossAllAccounts()
    {
        await using var db = MemoryContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await AssertSummaryAsync(db);
    }

    [PostgresAdministrationFact]
    public async Task PostgreSqlAggregatesEffectiveAccessInTheDatabase()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async db =>
        {
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await AssertSummaryAsync(db);
        });
    }

    [Fact]
    public async Task SummaryIsSeparateFromPagedSearchAndRequiresManageLicences()
    {
        var token = TestContext.Current.CancellationToken;
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            await SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }
        var summary = await client.GetFromJsonAsync<LicenceSummary>(
            "/admin/licences/summary?search=no-match&page=100&pageSize=1", token);
        // The normal factory account has a permanent Pro grant in addition to
        // the nine-account boundary fixture.
        Assert.Equal(new LicenceSummary(5, 5, 2), summary);
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Single(value => value.RoutePattern.RawText == "/admin/licences/summary");
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(value => value.Policy).ToList();
        Assert.Contains(AdministrativePermissions.ManageLicences, policies);
        Assert.DoesNotContain(AdministrativePermissions.ManageUsers, policies);
    }

    private static async Task AssertSummaryAsync(AppDbContext db)
    {
        var token = TestContext.Current.CancellationToken;
        await SeedAsync(db);
        var service = new EntitlementService(db, new Clock());
        var summary = await service.GetLicenceSummaryAsync(token);
        Assert.Equal(new LicenceSummary(5, 4, 2), summary);
        var ids = await db.Users.Select(value => value.Id).ToListAsync(token);
        var snapshots = await service.GetManyAsync(ids, token);
        Assert.Equal(summary.FreeUsers, snapshots.Values.Count(value => value.EffectivePlan.Code == Plan.Free));
        Assert.Equal(summary.PaidUsers, snapshots.Values.Count(value => value.EffectivePlan.Code != Plan.Free));
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var cases = new (string Name, string? Plan, SubscriptionState State, DateTimeOffset Start, DateTimeOffset? End, bool Active)[]
        {
            ("default", null, SubscriptionState.Active, Now.AddDays(-1), null, true),
            ("free", Plan.Free, SubscriptionState.Active, Now.AddDays(-1), null, true),
            ("expired", Plan.Pro, SubscriptionState.Active, Now.AddDays(-1), Now, true),
            ("revoked", Plan.Solo, SubscriptionState.Cancelled, Now.AddDays(-1), Now.AddDays(10), true),
            ("future", Plan.Pro, SubscriptionState.Active, Now.AddDays(1), null, true),
            ("inactive-permanent", Plan.Pro, SubscriptionState.Active, Now.AddDays(-1), null, false),
            ("boundary", Plan.Solo, SubscriptionState.Active, Now.AddDays(-1), Now.AddDays(30), true),
            ("outside", Plan.Pro, SubscriptionState.Active, Now.AddDays(-1), Now.AddDays(31), true),
            ("soon", Plan.Pro, SubscriptionState.Active, Now.AddDays(-1), Now.AddDays(1), true),
        };
        foreach (var item in cases)
        {
            var user = new User { Id = Guid.NewGuid(), Email = $"{item.Name}@summary.test", IsActive = item.Active, CreatedUtc = Now.UtcDateTime };
            db.Users.Add(user);
            if (item.Plan is not null)
            {
                db.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = user.Id, PlanCode = item.Plan, State = item.State, StartsAtUtc = item.Start, EndsAtUtc = item.End,
                });
            }
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static AppDbContext MemoryContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
