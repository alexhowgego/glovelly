using System.Security.Claims;
using Glovelly.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Auth;

public sealed record AdministrativePermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class AdministrativePermissionHandler(AppDbContext db, ICurrentUserAccessor currentUserAccessor)
    : AuthorizationHandler<AdministrativePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdministrativePermissionRequirement requirement)
    {
        var userId = currentUserAccessor.TryGetUserId(context.User);
        if (context.User.Identity?.IsAuthenticated != true || !userId.HasValue ||
            !await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive))
        {
            return;
        }

        var sets = await db.UserAdministrativePermissionSets.AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .Select(assignment => assignment.PermissionSet)
            .ToListAsync();
        if (AdministrativePermissionSets.Resolve(sets).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
