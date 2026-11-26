using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class PostgresAdministrationFactAttribute : FactAttribute
{
    public PostgresAdministrationFactAttribute(
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0) : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GLOVELLY_AUTH_TEST_CONNECTION")))
        {
            Skip = "Set GLOVELLY_AUTH_TEST_CONNECTION to an isolated migrated PostgreSQL test database.";
        }
    }
}

public sealed class AdministrativePostgresConcurrencyTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("GLOVELLY_AUTH_TEST_CONNECTION")).Options);

    [PostgresAdministrationFact]
    public async Task FailedAuditWriteRollsBackPermissionAssignment()
    {
        var token = TestContext.Current.CancellationToken;
        var manager = NewUser("manager");
        var target = NewUser("target");
        await using var setup = CreateContext();
        setup.Users.AddRange(manager, target);
        setup.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = manager.Id, PermissionSet = AdministrativePermissionSet.SuperAdministrator
        });
        await setup.SaveChangesAsync(token);
        try
        {
            await using var db = CreateContext();
            var service = new AdministrativePermissionAssignmentService(db, new AdministrativeMutationCoordinator(db), TimeProvider.System);
            await Assert.ThrowsAsync<DbUpdateException>(() => service.AssignAsync(
                target.Id, [AdministrativePermissionSet.UserAdministrator],
                new AdministrativeChangeActor(manager.Id, manager.Email, "User"), new string('x', 1001), token));
            Assert.False(await setup.UserAdministrativePermissionSets.AsNoTracking()
                .AnyAsync(value => value.UserId == target.Id, token));
            Assert.False(await setup.AdministrativePermissionChanges.AsNoTracking()
                .AnyAsync(value => value.TargetUserId == target.Id, token));
        }
        finally
        {
            await CleanupAsync([manager.Id, target.Id], token);
        }
    }

    [PostgresAdministrationFact]
    public async Task SimultaneousSuperDemotionsLeaveAUsableSuper()
    {
        var token = TestContext.Current.CancellationToken;
        var first = NewUser("first");
        var second = NewUser("second");
        await using var setup = CreateContext();
        Assert.False(await setup.UserAdministrativePermissionSets.AnyAsync(
            value => value.PermissionSet == AdministrativePermissionSet.SuperAdministrator && value.User.IsActive && value.User.GoogleSubject != null, token));
        setup.Users.AddRange(first, second);
        setup.UserAdministrativePermissionSets.AddRange(
            new UserAdministrativePermissionSet { UserId = first.Id, PermissionSet = AdministrativePermissionSet.SuperAdministrator },
            new UserAdministrativePermissionSet { UserId = second.Id, PermissionSet = AdministrativePermissionSet.SuperAdministrator });
        await setup.SaveChangesAsync(token);
        try
        {
            await using var db1 = CreateContext();
            await using var db2 = CreateContext();
            var one = new AdministrativePermissionAssignmentService(db1, new AdministrativeMutationCoordinator(db1), TimeProvider.System);
            var two = new AdministrativePermissionAssignmentService(db2, new AdministrativeMutationCoordinator(db2), TimeProvider.System);
            var outcomes = await Task.WhenAll(
                one.AssignAsync(first.Id, [], new AdministrativeChangeActor(first.Id, first.Email, "User"), null, token),
                two.AssignAsync(second.Id, [], new AdministrativeChangeActor(second.Id, second.Email, "User"), null, token));
            Assert.Single(outcomes, result => result == AdministrativeAssignmentResult.Changed);
            Assert.Single(outcomes, result => result == AdministrativeAssignmentResult.LastSuperAdministrator);
            Assert.Equal(1, await setup.UserAdministrativePermissionSets.AsNoTracking()
                .CountAsync(value => (value.UserId == first.Id || value.UserId == second.Id) &&
                    value.PermissionSet == AdministrativePermissionSet.SuperAdministrator, token));
        }
        finally
        {
            await CleanupAsync([first.Id, second.Id], token);
        }
    }

    [PostgresAdministrationFact]
    public async Task PermissionGrantSerializesWithTargetPrivilegeCheck()
    {
        var token = TestContext.Current.CancellationToken;
        var manager = NewUser("manager");
        var target = NewUser("target");
        await using var setup = CreateContext();
        setup.Users.AddRange(manager, target);
        setup.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = manager.Id, PermissionSet = AdministrativePermissionSet.UserAdministrator
        });
        await setup.SaveChangesAsync(token);
        try
        {
            await using var grantDb = CreateContext();
            await using var editDb = CreateContext();
            var lockHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var grant = new AdministrativeMutationCoordinator(grantDb).ExecuteAsync(async () =>
            {
                grantDb.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
                {
                    UserId = target.Id, PermissionSet = AdministrativePermissionSet.LicenseAdministrator
                });
                await grantDb.SaveChangesAsync(token);
                lockHeld.SetResult();
                await release.Task.WaitAsync(token);
                return true;
            }, token);
            await lockHeld.Task.WaitAsync(token);
            var edit = new AdministrativeMutationCoordinator(editDb).ExecuteAsync(
                () => new AdministrativeAccountGuard(editDb).CanModifyAsync(manager.Id, target.Id, token), token);
            var blocked = false;
            try
            {
                for (var attempt = 0; attempt < 100 && !blocked; attempt++)
                {
                    blocked = await setup.Database.SqlQueryRaw<int>(
                        """SELECT count(*)::int AS "Value" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted""")
                        .SingleAsync(token) > 0;
                    if (!blocked) await Task.Delay(20, token);
                }
            }
            finally
            {
                release.SetResult();
            }
            await grant;
            Assert.True(blocked, "The competing account mutation did not wait for the advisory lock.");
            Assert.False(await edit);
        }
        finally
        {
            await CleanupAsync([manager.Id, target.Id], token);
        }
    }

    private static User NewUser(string prefix)
    {
        var id = Guid.NewGuid();
        return new User
        {
            Id = id, Email = $"{prefix}-{id:N}@example.test", GoogleSubject = id.ToString(),
            IsActive = true, CreatedUtc = DateTime.UtcNow
        };
    }

    private static async Task CleanupAsync(Guid[] userIds, CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        await db.AdministrativePermissionChanges.Where(value => userIds.Contains(value.TargetUserId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.UserAdministrativePermissionSets.Where(value => userIds.Contains(value.UserId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.Users.Where(value => userIds.Contains(value.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
