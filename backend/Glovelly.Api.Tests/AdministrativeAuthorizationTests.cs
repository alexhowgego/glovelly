using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AdministrativeAuthorizationTests
{
    [Fact]
    public async Task DefaultTestSeedingDoesNotCreateSuperAdministrator()
    {
        await using var factory = new GlovellyApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.UserAdministrativePermissionSets.AnyAsync(
            value => value.PermissionSet == AdministrativePermissionSet.SuperAdministrator,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PermissionsAreResolvedFromActiveUserOnEveryRequest()
    {
        var token = TestContext.Current.CancellationToken;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Id = Guid.NewGuid(), Email = "manager@example.test", IsActive = true };
        db.Users.Add(user);
        var assignment = new UserAdministrativePermissionSet
        {
            UserId = user.Id, PermissionSet = AdministrativePermissionSet.UserAdministrator
        };
        db.UserAdministrativePermissionSets.Add(assignment);
        await db.SaveChangesAsync(token);
        var identity = new ClaimsIdentity([new Claim(GlovellyClaimTypes.UserId, user.Id.ToString())], "test");
        var principal = new ClaimsPrincipal(identity);
        var handler = new AdministrativePermissionHandler(db, new CurrentUserAccessor());

        Assert.True(await IsAllowedAsync(handler, principal, AdministrativePermissions.ManageUsers));
        Assert.False(await IsAllowedAsync(handler, principal, AdministrativePermissions.ManageLicences));
        db.UserAdministrativePermissionSets.Remove(assignment);
        await db.SaveChangesAsync(token);
        Assert.False(await IsAllowedAsync(handler, principal, AdministrativePermissions.ManageUsers));
        db.UserAdministrativePermissionSets.Add(assignment);
        user.IsActive = false;
        await db.SaveChangesAsync(token);
        Assert.False(await IsAllowedAsync(handler, principal, AdministrativePermissions.ManageUsers));
    }

    private static async Task<bool> IsAllowedAsync(
        AdministrativePermissionHandler handler, ClaimsPrincipal principal, string permission)
    {
        var requirement = new AdministrativePermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }
}
