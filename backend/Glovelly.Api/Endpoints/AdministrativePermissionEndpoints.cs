using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

public static class AdministrativePermissionEndpoints
{
    public static IEndpointRouteBuilder MapAdministrativePermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/permissions")
            .RequireAuthorization(AdministrativePermissions.ManagePermissions)
            .WithTags("Admin");

        group.MapGet("/users", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var users = await db.Users.AsNoTracking().OrderBy(value => value.Email)
                .Select(value => new { value.Id, value.Email, value.DisplayName, value.IsActive, value.GoogleSubject })
                .ToListAsync(cancellationToken);
            var assignments = await db.UserAdministrativePermissionSets.AsNoTracking().ToListAsync(cancellationToken);
            return Results.Ok(users.Select(user => new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive,
                IsEnrolled = user.GoogleSubject != null,
                PermissionSets = assignments.Where(value => value.UserId == user.Id).Select(value => value.PermissionSet)
            }));
        });

        group.MapGet("/audit", async (Guid? targetUserId, AppDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.AdministrativePermissionChanges.AsNoTracking()
                .Where(value => !targetUserId.HasValue || value.TargetUserId == targetUserId.Value)
                .OrderByDescending(value => value.ChangedAtUtc)
                .Take(200).ToListAsync(cancellationToken)));

        group.MapPut("/users/{id:guid}", async (
            Guid id, PermissionAssignmentRequest request, ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor, AppDbContext db,
            AdministrativePermissionAssignmentService service, CancellationToken cancellationToken) =>
        {
            var actorId = currentUserAccessor.TryGetUserId(principal);
            if (!actorId.HasValue)
            {
                return Results.Forbid();
            }
            var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(value => value.Id == actorId, cancellationToken);
            if (actor is null || request.PermissionSets is null ||
                request.PermissionSets.Any(value => !Enum.IsDefined(value)))
            {
                return Results.BadRequest();
            }
            var result = await service.AssignAsync(
                id, request.PermissionSets, new AdministrativeChangeActor(actor.Id, actor.Email, "User"),
                request.Reason, cancellationToken);
            return result switch
            {
                AdministrativeAssignmentResult.Changed or AdministrativeAssignmentResult.Unchanged => Results.Ok(new { result }),
                AdministrativeAssignmentResult.UserNotFound => Results.NotFound(),
                AdministrativeAssignmentResult.Forbidden => Results.Forbid(),
                AdministrativeAssignmentResult.LastSuperAdministrator => Results.Conflict(new
                {
                    message = "At least one active enrolled Super Administrator must remain."
                }),
                _ => Results.BadRequest()
            };
        });

        return app;
    }

    private sealed record PermissionAssignmentRequest(AdministrativePermissionSet[]? PermissionSets, string? Reason);
}
