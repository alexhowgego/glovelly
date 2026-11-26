using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

public static class LicenceEndpoints
{
    public static IEndpointRouteBuilder MapLicenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/licences")
            .RequireAuthorization(AdministrativePermissions.ManageLicences).WithTags("Admin");
        group.MapGet("/summary", async (EntitlementService entitlements, CancellationToken cancellationToken) =>
            Results.Ok(await entitlements.GetLicenceSummaryAsync(cancellationToken)));
        group.MapGet("/users", async (
            string? search, int? page, int? pageSize, AppDbContext db,
            EntitlementService entitlements, CancellationToken cancellationToken) =>
        {
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var number = Math.Clamp(page ?? 1, 1, 100000);
            var query = db.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(value => value.Email.ToLower().Contains(term) ||
                    (value.DisplayName != null && value.DisplayName.ToLower().Contains(term)));
            }
            var total = await query.CountAsync(cancellationToken);
            var users = await query.OrderBy(value => value.Email).ThenBy(value => value.Id)
                .Skip((number - 1) * size).Take(size)
                .Select(value => new
                {
                    value.Id, value.Email, value.DisplayName, value.IsActive,
                    IsEnrolled = value.GoogleSubject != null,
                }).ToListAsync(cancellationToken);
            var ids = users.Select(value => value.Id).ToList();
            var versions = await db.UserSubscriptions.AsNoTracking().Where(value => ids.Contains(value.UserId))
                .ToDictionaryAsync(value => value.UserId, value => value.Version, cancellationToken);
            var snapshots = await entitlements.GetManyAsync(ids, cancellationToken);
            var rows = new List<object>();
            foreach (var user in users)
            {
                rows.Add(new
                {
                    user.Id, user.Email, user.DisplayName, user.IsActive, user.IsEnrolled,
                    Version = versions.TryGetValue(user.Id, out var version) ? (Guid?)version : null,
                    Entitlements = snapshots[user.Id],
                });
            }
            return Results.Ok(new { users = rows, total, page = number, pageSize = size });
        });
        group.MapGet("/plans", async (AppDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.Plans.AsNoTracking().OrderBy(value => value.Code).ToListAsync(cancellationToken)));
        group.MapGet("/audit", async (
            Guid? targetUserId, AppDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.LicenceChanges.AsNoTracking()
                .Where(value => !targetUserId.HasValue || value.TargetUserId == targetUserId)
                .OrderByDescending(value => value.ChangedAtUtc).ThenByDescending(value => value.Id)
                .Take(200).ToListAsync(cancellationToken)));
        group.MapPut("/users/{id:guid}", async (
            Guid id, LicenceAssignmentRequest request, ClaimsPrincipal user, ICurrentUserAccessor accessor,
            LicenceAssignmentService service, CancellationToken cancellationToken) =>
        {
            var actor = accessor.TryGetUserId(user);
            if (!actor.HasValue) return Results.Forbid();
            var result = await service.AssignAsync(id, request, actor.Value, cancellationToken);
            return result switch
            {
                LicenceAssignmentResult.Changed or LicenceAssignmentResult.Unchanged => Results.Ok(new { result = result.ToString() }),
                LicenceAssignmentResult.Forbidden => Results.Forbid(),
                LicenceAssignmentResult.NotFound => Results.NotFound(),
                LicenceAssignmentResult.Conflict => Results.Conflict(new { message = "This assignment changed. Reload before saving again." }),
                _ => EndpointSupport.ValidationProblem("assignment", "Choose an existing plan and a future paid-grant expiry, with a reason of at most 1000 characters."),
            };
        });
        return app;
    }
}
