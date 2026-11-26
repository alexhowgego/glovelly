using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class LicencePostgresTests
{
    [PostgresAdministrationFact]
    public async Task ConcurrentMissingAssignmentVersionsCannotOverwriteEachOther()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var actor = await SeedAsync(setup);
            await using var first = Context(setup);
            await using var second = Context(setup);
            var outcomes = await Task.WhenAll(
                Service(first).AssignAsync(actor.Id, new(Plan.Pro, null, false, null, "First"), actor.Id, token),
                Service(second).AssignAsync(actor.Id, new(Plan.Solo, null, false, null, "Second"), actor.Id, token));
            Assert.Single(outcomes, value => value == LicenceAssignmentResult.Changed);
            Assert.Single(outcomes, value => value == LicenceAssignmentResult.Conflict);
            Assert.Single(await setup.LicenceChanges.ToListAsync(token));
        });
    }

    [PostgresAdministrationFact]
    public async Task AuditFailureRollsBackGrantAndAccountDeletionRetainsSnapshots()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var actor = await SeedAsync(setup);
            await using (var failing = Context(setup, new FailAudit()))
            {
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    Service(failing).AssignAsync(actor.Id, new(Plan.Pro, null, false, null, "Tester"), actor.Id, token));
            }
            Assert.Empty(await setup.UserSubscriptions.AsNoTracking().ToListAsync(token));
            Assert.Empty(await setup.LicenceChanges.AsNoTracking().ToListAsync(token));
            await using (var normal = Context(setup))
            {
                Assert.Equal(LicenceAssignmentResult.Changed,
                    await Service(normal).AssignAsync(actor.Id, new(Plan.Pro, null, false, null, "Tester"), actor.Id, token));
            }
            await setup.Users.Where(value => value.Id == actor.Id).ExecuteDeleteAsync(token);
            var audit = Assert.Single(await setup.LicenceChanges.AsNoTracking().ToListAsync(token));
            Assert.Equal(actor.Email, audit.ActorIdentity);
            Assert.Equal(actor.Email, audit.TargetIdentity);
            Assert.Empty(await setup.UserSubscriptions.AsNoTracking().ToListAsync(token));
        });
    }

    [PostgresAdministrationFact]
    public async Task CapabilityRevocationIsRecheckedAfterAdministrativeWait()
    {
        await EntitlementPostgresMigrationTests.WithDatabaseAsync(async setup =>
        {
            var token = TestContext.Current.CancellationToken;
            var actor = await SeedAsync(setup);
            await using var revoking = Context(setup);
            var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var revoke = new AdministrativeMutationCoordinator(revoking).ExecuteAsync(async () =>
            {
                revoking.UserAdministrativePermissionSets.RemoveRange(
                    await revoking.UserAdministrativePermissionSets.ToListAsync(token));
                await revoking.SaveChangesAsync(token);
                saved.TrySetResult();
                await release.Task.WaitAsync(token);
                return true;
            }, token);
            await saved.Task.WaitAsync(token);
            await using var licensing = Context(setup);
            var assignment = Service(licensing).AssignAsync(
                actor.Id, new(Plan.Pro, null, false, null, null), actor.Id, token);
            release.TrySetResult();
            await revoke;
            Assert.Equal(LicenceAssignmentResult.Forbidden, await assignment);
            Assert.Empty(await setup.UserSubscriptions.AsNoTracking().ToListAsync(token));
        });
    }

    private static AppDbContext Context(AppDbContext db, params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString())
            .AddInterceptors(interceptors).Options);
    private static LicenceAssignmentService Service(AppDbContext db) =>
        new(db, new AdministrativeMutationCoordinator(db), new EntitlementMutationCoordinator(db), TimeProvider.System);

    private static async Task<User> SeedAsync(AppDbContext db)
    {
        var token = TestContext.Current.CancellationToken;
        await db.Database.MigrateAsync(token);
        var user = new User { Id = Guid.NewGuid(), Email = "licence-manager@example.test", CreatedUtc = DateTime.UtcNow };
        db.Users.Add(user);
        db.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = user.Id, PermissionSet = AdministrativePermissionSet.LicenseAdministrator,
        });
        await db.SaveChangesAsync(token);
        return user;
    }

    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<LicenceChange>().Any())
            {
                throw new DbUpdateException("Injected audit save failure.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
