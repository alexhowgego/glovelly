using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AdministrativePermissionAssignmentTests
{
    [Fact]
    public async Task BootstrapRejectsMissingAndInactiveTargets()
    {
        await using var db = CreateContext();
        var user = new User { Id = Guid.NewGuid(), Email = "inactive@example.test", IsActive = false, GoogleSubject = "subject" };
        db.Users.Add(user);
        var token = TestContext.Current.CancellationToken;
        await db.SaveChangesAsync(token);
        var actor = new AdministrativeChangeActor(null, "operator", "Operator");
        var service = CreateService(db);

        Assert.Equal(AdministrativeAssignmentResult.UserNotFound,
            await service.BootstrapSuperAsync(Guid.NewGuid(), actor, "recovery", token));
        Assert.Equal(AdministrativeAssignmentResult.InvalidBootstrapTarget,
            await service.BootstrapSuperAsync(user.Id, actor, "recovery", token));
        Assert.Empty(await db.AdministrativePermissionChanges.ToListAsync(token));
    }

    [Fact]
    public async Task SuperAdministratorCanHandOverAndAuditSurvivesTargetDeletion()
    {
        await using var db = CreateContext();
        var first = new User { Id = Guid.NewGuid(), Email = "first@example.test", IsActive = true, GoogleSubject = "first" };
        var second = new User { Id = Guid.NewGuid(), Email = "second@example.test", IsActive = true, GoogleSubject = "second" };
        db.Users.AddRange(first, second);
        db.UserAdministrativePermissionSets.AddRange(
            new UserAdministrativePermissionSet { UserId = first.Id, PermissionSet = AdministrativePermissionSet.SuperAdministrator },
            new UserAdministrativePermissionSet { UserId = second.Id, PermissionSet = AdministrativePermissionSet.SuperAdministrator });
        var token = TestContext.Current.CancellationToken;
        await db.SaveChangesAsync(token);

        Assert.Equal(AdministrativeAssignmentResult.Changed, await CreateService(db).AssignAsync(
            first.Id, [], new AdministrativeChangeActor(first.Id, first.Email, "User"), "handover", token));
        db.Users.Remove(first);
        await db.SaveChangesAsync(token);
        var audit = Assert.Single(await db.AdministrativePermissionChanges.ToListAsync(token));
        Assert.Equal(first.Id, audit.TargetUserId);
        Assert.Equal(first.Email, audit.ActorIdentity);
        Assert.Equal("handover", audit.Reason);
    }

    [Fact]
    public async Task BootstrapRequiresActiveEnrolledUserAndPreservesExistingSets()
    {
        await using var db = CreateContext();
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.test", IsActive = true };
        db.Users.Add(user);
        db.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = user.Id, PermissionSet = AdministrativePermissionSet.UserAdministrator
        });
        var token = TestContext.Current.CancellationToken;
        await db.SaveChangesAsync(token);
        var service = CreateService(db);
        var actor = new AdministrativeChangeActor(null, "operator", "Operator");

        Assert.Equal(AdministrativeAssignmentResult.InvalidBootstrapTarget,
            await service.BootstrapSuperAsync(user.Id, actor, "recovery", token));
        user.GoogleSubject = "verified-subject";
        await db.SaveChangesAsync(token);
        Assert.Equal(AdministrativeAssignmentResult.Changed,
            await service.BootstrapSuperAsync(user.Id, actor, "recovery", token));
        Assert.Equal(AdministrativeAssignmentResult.Unchanged,
            await service.BootstrapSuperAsync(user.Id, actor, "recovery", token));
        Assert.Equal(2, await db.UserAdministrativePermissionSets.CountAsync(token));
        Assert.Single(await db.AdministrativePermissionChanges.ToListAsync(token));
    }

    [Fact]
    public async Task UserManagerCannotGrantThemselvesPermissionsOrModifyPrivilegedUsers()
    {
        await using var db = CreateContext();
        var manager = new User { Id = Guid.NewGuid(), Email = "manager@example.test", IsActive = true };
        var target = new User { Id = Guid.NewGuid(), Email = "target@example.test", IsActive = true };
        db.Users.AddRange(manager, target);
        db.UserAdministrativePermissionSets.AddRange(
            new UserAdministrativePermissionSet { UserId = manager.Id, PermissionSet = AdministrativePermissionSet.UserAdministrator },
            new UserAdministrativePermissionSet { UserId = target.Id, PermissionSet = AdministrativePermissionSet.LicenseAdministrator });
        var token = TestContext.Current.CancellationToken;
        await db.SaveChangesAsync(token);

        Assert.Equal(AdministrativeAssignmentResult.Forbidden,
            await CreateService(db).AssignAsync(manager.Id, [AdministrativePermissionSet.SuperAdministrator],
                new AdministrativeChangeActor(manager.Id, manager.Email, "User"), null, token));
        Assert.Equal(AdministrativeAssignmentResult.Forbidden,
            await CreateService(db).AssignAsync(target.Id, [AdministrativePermissionSet.SuperAdministrator],
                new AdministrativeChangeActor(manager.Id, manager.Email, "User"), null, token));
        Assert.False(await new AdministrativeAccountGuard(db).CanModifyAsync(manager.Id, target.Id, token));
        Assert.Empty(await db.AdministrativePermissionChanges.ToListAsync(token));
    }

    [Fact]
    public async Task AssignmentWritesAuditAndNoOpDoesNot()
    {
        await using var db = CreateContext();
        var user = new User { Id = Guid.NewGuid(), Email = "target@example.test", IsActive = true };
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@example.test", IsActive = true };
        db.Users.Add(user);
        db.Users.Add(admin);
        db.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = admin.Id,
            PermissionSet = AdministrativePermissionSet.SuperAdministrator
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await db.SaveChangesAsync(cancellationToken);
        var service = CreateService(db);
        var actor = new AdministrativeChangeActor(admin.Id, admin.Email, "User");

        var result = await service.AssignAsync(user.Id, [AdministrativePermissionSet.UserAdministrator], actor, "initial", cancellationToken);
        var repeated = await service.AssignAsync(user.Id, [AdministrativePermissionSet.UserAdministrator], actor, "initial", cancellationToken);

        Assert.Equal(AdministrativeAssignmentResult.Changed, result);
        Assert.Equal(AdministrativeAssignmentResult.Unchanged, repeated);
        Assert.Equal(2, await db.UserAdministrativePermissionSets.CountAsync(cancellationToken));
        var audit = Assert.Single(await db.AdministrativePermissionChanges.ToListAsync(cancellationToken));
        Assert.Equal(user.Id, audit.TargetUserId);
        Assert.Equal(admin.Email, audit.ActorIdentity);
        Assert.Contains("UserAdministrator", audit.NewSets);
        Assert.Equal("initial", audit.Reason);
    }

    [Fact]
    public async Task CannotRemoveLastActiveEnrolledSuper()
    {
        await using var db = CreateContext();
        var user = new User { Id = Guid.NewGuid(), Email = "super@example.test", IsActive = true, GoogleSubject = "subject" };
        db.Users.Add(user);
        db.UserAdministrativePermissionSets.Add(new UserAdministrativePermissionSet
        {
            UserId = user.Id,
            PermissionSet = AdministrativePermissionSet.SuperAdministrator
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await db.SaveChangesAsync(cancellationToken);

        var result = await CreateService(db).AssignAsync(
            user.Id, [], new AdministrativeChangeActor(user.Id, user.Email, "User"), null, cancellationToken);

        Assert.Equal(AdministrativeAssignmentResult.LastSuperAdministrator, result);
        Assert.Single(await db.UserAdministrativePermissionSets.ToListAsync(cancellationToken));
        Assert.Empty(await db.AdministrativePermissionChanges.ToListAsync(cancellationToken));
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AdministrativePermissionAssignmentService CreateService(AppDbContext db) =>
        new(db, new AdministrativeMutationCoordinator(db), TimeProvider.System);
}
