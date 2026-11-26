using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record LicenceAssignmentRequest(
    string PlanCode, DateTimeOffset? EndsAtUtc, bool Cancelled,
    [property: System.Text.Json.Serialization.JsonRequired] Guid? Version, string? Reason);

public enum LicenceAssignmentResult { Changed, Unchanged, Forbidden, NotFound, Invalid, Conflict }

public sealed class LicenceAssignmentService(
    AppDbContext db, AdministrativeMutationCoordinator administration,
    EntitlementMutationCoordinator entitlements, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public Task<LicenceAssignmentResult> AssignAsync(
        Guid targetUserId, LicenceAssignmentRequest request, Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        administration.ExecuteAsync(async () =>
        {
            // Read inside the same administrative boundary as capability
            // revocation/account status changes; never trust caller claims here.
            var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(
                value => value.Id == actorUserId && value.IsActive, cancellationToken);
            if (actor is null)
            {
                return LicenceAssignmentResult.Forbidden;
            }
            var sets = await db.UserAdministrativePermissionSets.AsNoTracking()
                .Where(value => value.UserId == actorUserId).Select(value => value.PermissionSet)
                .ToListAsync(cancellationToken);
            if (!AdministrativePermissionSets.Resolve(sets).Contains(AdministrativePermissions.ManageLicences))
            {
                return LicenceAssignmentResult.Forbidden;
            }
            return await entitlements.ExecuteAsync(targetUserId, async () =>
            {
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
                    value => value.Id == targetUserId, cancellationToken);
                if (user is null)
                {
                    return LicenceAssignmentResult.NotFound;
                }
                var now = timeProvider.GetUtcNow();
                if (request.Reason?.Length > 1000 ||
                    !await db.Plans.AnyAsync(value => value.Code == request.PlanCode, cancellationToken) ||
                    (request.EndsAtUtc.HasValue && request.EndsAtUtc <= now) ||
                    (request.PlanCode == Plan.Free && request.EndsAtUtc.HasValue))
                {
                    return LicenceAssignmentResult.Invalid;
                }
                // Fresh tracked assignment is loaded only after both locks.
                var assignment = await db.UserSubscriptions.SingleOrDefaultAsync(
                    value => value.UserId == targetUserId, cancellationToken);
                if (assignment is not null)
                {
                    await db.Entry(assignment).ReloadAsync(cancellationToken);
                }
                if (assignment?.Version != request.Version)
                {
                    return LicenceAssignmentResult.Conflict;
                }
                var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
                var state = request.Cancelled ? SubscriptionState.Cancelled : SubscriptionState.Active;
                var end = request.Cancelled || request.PlanCode == Plan.Free ? null : request.EndsAtUtc?.ToUniversalTime();
                if (assignment is not null && assignment.PlanCode == request.PlanCode &&
                    assignment.State == state && assignment.EndsAtUtc == end && assignment.Reason == reason)
                {
                    return LicenceAssignmentResult.Unchanged;
                }
                if (assignment is null && request.PlanCode == Plan.Free && !request.Cancelled && reason is null)
                {
                    return LicenceAssignmentResult.Unchanged;
                }
                var previous = assignment is null ? null : JsonSerializer.Serialize(assignment, JsonOptions);
                if (assignment is null)
                {
                    assignment = new UserSubscription { UserId = targetUserId };
                    db.UserSubscriptions.Add(assignment);
                }
                assignment.PlanCode = request.PlanCode;
                assignment.State = state;
                assignment.StartsAtUtc = now;
                assignment.EndsAtUtc = end;
                assignment.Source = "Manual";
                assignment.Reason = reason;
                assignment.Version = Guid.NewGuid();
                db.LicenceChanges.Add(new LicenceChange
                {
                    Id = Guid.NewGuid(), TargetUserId = user.Id, TargetIdentity = user.Email,
                    ActorUserId = actor.Id, ActorIdentity = actor.Email, Source = "User", ChangedAtUtc = now,
                    PreviousAssignment = previous, NewAssignment = JsonSerializer.Serialize(assignment, JsonOptions),
                    Reason = reason,
                });
                await db.SaveChangesAsync(cancellationToken);
                return LicenceAssignmentResult.Changed;
            }, cancellationToken);
        }, cancellationToken);
}
