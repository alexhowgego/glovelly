using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

// Call only while holding AdministrativeMutationCoordinator's lock.
public sealed class AdministrativeAccountGuard(AppDbContext db)
{
    public async Task<bool> CanModifyAsync(Guid actorId, Guid targetId, CancellationToken cancellationToken)
    {
        var actor = await db.Users.AsNoTracking()
            .AnyAsync(user => user.Id == actorId && user.IsActive, cancellationToken);
        if (!actor)
        {
            return false;
        }

        var actorSets = await db.UserAdministrativePermissionSets.AsNoTracking()
            .Where(value => value.UserId == actorId)
            .Select(value => value.PermissionSet).ToListAsync(cancellationToken);
        var capabilities = AdministrativePermissionSets.Resolve(actorSets);
        if (!capabilities.Contains(AdministrativePermissions.ManageUsers))
        {
            return false;
        }

        return capabilities.Contains(AdministrativePermissions.ManagePermissions) ||
            !await db.UserAdministrativePermissionSets.AnyAsync(value => value.UserId == targetId, cancellationToken);
    }

    public async Task<bool> WouldRemoveLastUsableSuperAsync(User target, CancellationToken cancellationToken)
    {
        if (!target.IsActive || string.IsNullOrWhiteSpace(target.GoogleSubject) ||
            !await db.UserAdministrativePermissionSets.AnyAsync(
                value => value.UserId == target.Id && value.PermissionSet == AdministrativePermissionSet.SuperAdministrator,
                cancellationToken))
        {
            return false;
        }

        return !await db.UserAdministrativePermissionSets.AnyAsync(
            value => value.UserId != target.Id &&
                value.PermissionSet == AdministrativePermissionSet.SuperAdministrator &&
                value.User.IsActive && value.User.GoogleSubject != null,
            cancellationToken);
    }
}
