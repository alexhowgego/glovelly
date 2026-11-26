using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class LicenceAssignmentTests
{
    [Fact]
    public async Task UserAdministratorCannotAssignLicences()
    {
        await using var db = await CreateAsync(AdministrativePermissionSet.UserAdministrator);
        var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LicenceAssignmentResult.Forbidden, await Service(db).AssignAsync(
            user.Id, new(Plan.Pro, null, false, null, null), user.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await db.LicenceChanges.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LicenceOnlyManagerCanAssignOwnPlanWithoutAdministrativePromotion()
    {
        await using var db = await CreateAsync(AdministrativePermissionSet.LicenseAdministrator);
        var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
        var end = DateTimeOffset.UtcNow.AddDays(10);
        Assert.Equal(LicenceAssignmentResult.Changed, await Service(db).AssignAsync(
            user.Id, new(Plan.Pro, end, false, null, "Tester"), user.Id, TestContext.Current.CancellationToken));
        var grant = await db.UserSubscriptions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(end, grant.EndsAtUtc);
        Assert.Equal("Tester", grant.Reason);
        var permissions = Assert.Single(await db.UserAdministrativePermissionSets.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(AdministrativePermissionSet.LicenseAdministrator, permissions.PermissionSet);
        var audit = Assert.Single(await db.LicenceChanges.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(user.Email, audit.ActorIdentity);
        Assert.Equal(user.Email, audit.TargetIdentity);
        Assert.Null(audit.PreviousAssignment);
    }

    [Fact]
    public async Task LicenceOnlyManagerCanChangePrivilegedTargetsProductPlanWithoutChangingAuthority()
    {
        var token = TestContext.Current.CancellationToken;
        await using var db = await CreateAsync(AdministrativePermissionSet.LicenseAdministrator);
        var actor = await db.Users.SingleAsync(token);
        var target = new User { Id = Guid.NewGuid(), Email = "super@example.test", GoogleSubject = "super", IsActive = true };
        db.Users.Add(target);
        db.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = target.Id, PermissionSet = AdministrativePermissionSet.SuperAdministrator,
        });
        await db.SaveChangesAsync(token);
        Assert.Equal(LicenceAssignmentResult.Changed, await Service(db).AssignAsync(
            target.Id, new(Plan.Solo, null, false, null, "Goodwill"), actor.Id, token));
        Assert.Equal(AdministrativePermissionSet.SuperAdministrator,
            (await db.UserAdministrativePermissionSets.SingleAsync(value => value.UserId == target.Id, token)).PermissionSet);
        Assert.Equal(Plan.Solo, (await db.UserSubscriptions.SingleAsync(token)).PlanCode);
    }

    [Fact]
    public async Task NoopAndStaleVersionDoNotCreateAuditOrResetUsage()
    {
        var token = TestContext.Current.CancellationToken;
        await using var db = await CreateAsync(AdministrativePermissionSet.LicenseAdministrator);
        var user = await db.Users.SingleAsync(token);
        var service = Service(db);
        var request = new LicenceAssignmentRequest(Plan.Pro, null, false, null, "Founder");
        Assert.Equal(LicenceAssignmentResult.Changed, await service.AssignAsync(user.Id, request, user.Id, token));
        var grant = await db.UserSubscriptions.SingleAsync(token);
        var version = grant.Version;
        var starts = grant.StartsAtUtc;
        Assert.Equal(LicenceAssignmentResult.Unchanged, await service.AssignAsync(user.Id, request with { Version = version }, user.Id, token));
        Assert.Equal(starts, grant.StartsAtUtc);
        Assert.Equal(version, grant.Version);
        Assert.Equal(LicenceAssignmentResult.Conflict, await service.AssignAsync(user.Id, request with { PlanCode = Plan.Free }, user.Id, token));
        Assert.Single(await db.LicenceChanges.ToListAsync(token));
        db.UsageLedgerEntries.Add(new UsageLedgerEntry
        {
            Id = Guid.NewGuid(), UserId = user.Id, SourceInvoiceId = Guid.NewGuid(), Quantity = 10,
            OccurredAtUtc = DateTimeOffset.UtcNow, Source = "Test",
        });
        await db.SaveChangesAsync(token);
        Assert.Equal(LicenceAssignmentResult.Changed, await service.AssignAsync(
            user.Id, new(Plan.Free, null, false, version, null), user.Id, token));
        var snapshot = await new EntitlementService(db, TimeProvider.System).GetAsync(user.Id, token);
        Assert.Equal(10, snapshot.InvoiceIssuance.Used);
        Assert.Equal(0, snapshot.InvoiceIssuance.Remaining);
        Assert.Null(grant.EndsAtUtc);
    }

    [Fact]
    public async Task InvalidExpiryAndOverlongReasonAreRejected()
    {
        var token = TestContext.Current.CancellationToken;
        await using var db = await CreateAsync(AdministrativePermissionSet.LicenseAdministrator);
        var user = await db.Users.SingleAsync(token);
        foreach (var request in new[]
        {
            new LicenceAssignmentRequest(Plan.Pro, DateTimeOffset.UtcNow.AddDays(-1), false, null, null),
            new LicenceAssignmentRequest(Plan.Free, DateTimeOffset.UtcNow.AddDays(1), false, null, null),
            new LicenceAssignmentRequest("missing", null, false, null, null),
            new LicenceAssignmentRequest(Plan.Pro, null, false, null, new string('x', 1001)),
        })
        {
            Assert.Equal(LicenceAssignmentResult.Invalid, await Service(db).AssignAsync(user.Id, request, user.Id, token));
        }
        Assert.Empty(await db.UserSubscriptions.ToListAsync(token));
        Assert.Empty(await db.LicenceChanges.ToListAsync(token));
    }

    [Fact]
    public async Task DirectoryIsMinimalAndAssignmentRequiresExplicitVersion()
    {
        var token = TestContext.Current.CancellationToken;
        using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/admin/licences/users?search=test-admin&pageSize=1", token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        var row = Assert.Single(json.GetProperty("users").EnumerateArray());
        Assert.False(row.TryGetProperty("googleSubject", out _));
        Assert.False(row.TryGetProperty("permissionSets", out _));
        Assert.Equal(1, json.GetProperty("pageSize").GetInt32());
        using var invalid = await client.PutAsJsonAsync($"/admin/licences/users/{TestAuthContext.UserId}",
            new { planCode = Plan.Pro }, token);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().LicenceChanges.ToListAsync(token));
    }

    private static LicenceAssignmentService Service(AppDbContext db) =>
        new(db, new AdministrativeMutationCoordinator(db), new EntitlementMutationCoordinator(db), TimeProvider.System);

    private static async Task<AppDbContext> CreateAsync(AdministrativePermissionSet set)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var user = new User { Id = Guid.NewGuid(), Email = "manager@example.test", IsActive = true };
        db.Users.Add(user);
        db.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet { UserId = user.Id, PermissionSet = set });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return db;
    }
}
