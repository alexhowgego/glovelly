using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/admin/users")
            .WithTags("Admin")
            .RequireAuthorization(GlovellyPolicies.AdminUser);

        users.MapGet("/", async (AppDbContext db) =>
        {
            var result = await db.Users
                .AsNoTracking()
                .OrderByDescending(user => user.IsActive)
                .ThenBy(user => user.Role)
                .ThenBy(user => user.Email)
                .ToListAsync();

            return Results.Ok(result);
        });

        users.MapPost("/", async (AdminUserRequest request, AppDbContext db) =>
        {
            var normalized = Normalize(request);
            var validationErrors = await ValidateRequestAsync(normalized, db);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalized.Email,
                DisplayName = normalized.DisplayName,
                GoogleSubject = normalized.GoogleSubject,
                MileageRate = normalized.MileageRate,
                PassengerMileageRate = normalized.PassengerMileageRate,
                Role = normalized.Role,
                IsActive = normalized.IsActive,
                CreatedUtc = DateTime.UtcNow,
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/admin/users/{user.Id}", user);
        });

        users.MapPut("/{id:guid}", async (
            Guid id,
            AdminUserRequest request,
            AppDbContext db,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(value => value.Id == id);
            if (user is null)
            {
                return Results.NotFound();
            }

            var normalized = Normalize(request);
            var validationErrors = await ValidateRequestAsync(normalized, db, id);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            if (user.GoogleSubject is not null && normalized.GoogleSubject != user.GoogleSubject)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["googleSubject"] = ["Google subject cannot be changed after enrolment."]
                });
            }

            var currentUserId = currentUserAccessor.TryGetUserId(principal);
            if (currentUserId == user.Id)
            {
                if (!normalized.IsActive)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["isActive"] = ["You cannot deactivate your own administrator account."]
                    });
                }

                if (normalized.Role != UserRole.Admin)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["role"] = ["You cannot remove administrator access from your own account."]
                    });
                }
            }

            user.Email = normalized.Email;
            user.DisplayName = normalized.DisplayName;
            user.GoogleSubject = normalized.GoogleSubject;
            user.MileageRate = normalized.MileageRate;
            user.PassengerMileageRate = normalized.PassengerMileageRate;
            user.Role = normalized.Role;
            user.IsActive = normalized.IsActive;

            await db.SaveChangesAsync();

            return Results.Ok(user);
        });

        return app;
    }

    private static async Task<Dictionary<string, string[]>> ValidateRequestAsync(
        NormalizedAdminUserRequest request,
        AppDbContext db,
        Guid? existingUserId = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (!request.Email.Contains('@'))
        {
            errors["email"] = ["Email must look like a valid email address."];
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (request.MileageRate.HasValue && request.MileageRate.Value < 0)
        {
            errors["mileageRate"] = ["Mileage rate cannot be negative."];
        }

        if (request.PassengerMileageRate.HasValue && request.PassengerMileageRate.Value < 0)
        {
            errors["passengerMileageRate"] = ["Passenger mileage rate cannot be negative."];
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (await db.Users.AnyAsync(user =>
                user.Email == request.Email &&
                (!existingUserId.HasValue || user.Id != existingUserId.Value)))
        {
            errors["email"] = ["Another user already uses that email address."];
        }

        if (!string.IsNullOrWhiteSpace(request.GoogleSubject) &&
            await db.Users.AnyAsync(user =>
                user.GoogleSubject == request.GoogleSubject &&
                (!existingUserId.HasValue || user.Id != existingUserId.Value)))
        {
            errors["googleSubject"] = ["Another user already uses that Google subject."];
        }

        return errors;
    }

    private static NormalizedAdminUserRequest Normalize(AdminUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role?.Trim(), ignoreCase: true, out var role))
        {
            role = UserRole.User;
        }

        return new NormalizedAdminUserRequest(
            request.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(request.GoogleSubject) ? null : request.GoogleSubject.Trim(),
            request.MileageRate,
            request.PassengerMileageRate,
            role,
            request.IsActive);
    }

    private sealed record AdminUserRequest(
        string Email,
        string? DisplayName,
        string? GoogleSubject,
        decimal? MileageRate,
        decimal? PassengerMileageRate,
        string Role,
        bool IsActive);

    private sealed record NormalizedAdminUserRequest(
        string Email,
        string? DisplayName,
        string? GoogleSubject,
        decimal? MileageRate,
        decimal? PassengerMileageRate,
        UserRole Role,
        bool IsActive);
}
