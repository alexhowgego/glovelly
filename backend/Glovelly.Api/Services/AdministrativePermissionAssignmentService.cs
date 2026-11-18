using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed record AdministrativeChangeActor(Guid? UserId, string Identity, string Source);

public enum AdministrativeAssignmentResult
{
    Changed,
    Unchanged,
    UserNotFound,
    InvalidBootstrapTarget,
    Forbidden,
    LastSuperAdministrator,
}

public sealed class AdministrativePermissionAssignmentService(
    AppDbContext db,
    AdministrativeMutationCoordinator coordinator,
    TimeProvider timeProvider)
{
    public Task<AdministrativeAssignmentResult> AssignAsync(
        Guid userId,
        IReadOnlyCollection<AdministrativePermissionSet> requestedSets,
        AdministrativeChangeActor actor,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        _ = AdministrativePermissionSets.Resolve(requestedSets);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.Source);
        return coordinator.ExecuteAsync(
            () => AssignLockedAsync(userId, requestedSets, actor, reason, false, cancellationToken), cancellationToken);
    }

    public Task<AdministrativeAssignmentResult> BootstrapSuperAsync(
        Guid userId, AdministrativeChangeActor actor, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (actor.UserId.HasValue || actor.Source != "Operator")
        {
            throw new ArgumentException("Bootstrap requires an operator actor.", nameof(actor));
        }

        return coordinator.ExecuteAsync(
            () => AssignLockedAsync(userId, [], actor, reason, true, cancellationToken), cancellationToken);
    }

    private async Task<AdministrativeAssignmentResult> AssignLockedAsync(
        Guid userId, IReadOnlyCollection<AdministrativePermissionSet> requestedSets,
        AdministrativeChangeActor actor, string? reason, bool bootstrap, CancellationToken cancellationToken)
    {
        if (!bootstrap)
        {
            if (!actor.UserId.HasValue || !await db.Users.AsNoTracking().AnyAsync(
                    value => value.Id == actor.UserId && value.IsActive, cancellationToken))
            {
                return AdministrativeAssignmentResult.Forbidden;
            }
            var actorSets = await db.UserAdministrativePermissionSets.AsNoTracking()
                .Where(value => value.UserId == actor.UserId)
                .Select(value => value.PermissionSet).ToListAsync(cancellationToken);
            if (!AdministrativePermissionSets.Resolve(actorSets).Contains(AdministrativePermissions.ManagePermissions))
            {
                return AdministrativeAssignmentResult.Forbidden;
            }
        }
        var user = await db.Users.FirstOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
        {
            return AdministrativeAssignmentResult.UserNotFound;
        }
        if (bootstrap && (!user.IsActive || string.IsNullOrWhiteSpace(user.GoogleSubject)))
        {
            return AdministrativeAssignmentResult.InvalidBootstrapTarget;
        }

        var existing = await db.UserAdministrativePermissionSets
            .Where(value => value.UserId == userId).ToListAsync(cancellationToken);
        var before = existing.Select(value => value.PermissionSet).ToHashSet();
        var after = bootstrap ? before.ToHashSet() : requestedSets.ToHashSet();
        if (bootstrap)
        {
            after.Add(AdministrativePermissionSet.SuperAdministrator);
        }
        if (before.SetEquals(after))
        {
            return AdministrativeAssignmentResult.Unchanged;
        }

        if (user.IsActive && user.GoogleSubject is not null &&
            before.Contains(AdministrativePermissionSet.SuperAdministrator) &&
            !after.Contains(AdministrativePermissionSet.SuperAdministrator) &&
            !await db.UserAdministrativePermissionSets.AnyAsync(
                assignment => assignment.UserId != userId &&
                    assignment.PermissionSet == AdministrativePermissionSet.SuperAdministrator &&
                    assignment.User.IsActive && assignment.User.GoogleSubject != null,
                cancellationToken))
        {
            return AdministrativeAssignmentResult.LastSuperAdministrator;
        }

        db.UserAdministrativePermissionSets.RemoveRange(existing.Where(value => !after.Contains(value.PermissionSet)));
        db.UserAdministrativePermissionSets.AddRange(after.Except(before).Select(value =>
            new UserAdministrativePermissionSet { UserId = userId, PermissionSet = value }));
        db.AdministrativePermissionChanges.Add(new AdministrativePermissionChange
        {
            Id = Guid.NewGuid(),
            TargetUserId = user.Id,
            TargetIdentity = user.Email,
            ActorUserId = actor.UserId,
            ActorIdentity = actor.Identity,
            Source = actor.Source,
            ChangedAtUtc = timeProvider.GetUtcNow(),
            PreviousSets = JsonSerializer.Serialize(before.OrderBy(value => value).Select(value => value.ToString())),
            NewSets = JsonSerializer.Serialize(after.OrderBy(value => value).Select(value => value.ToString())),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        return AdministrativeAssignmentResult.Changed;
    }
}
