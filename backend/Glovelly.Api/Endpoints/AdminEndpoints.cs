using Glovelly.Api.Auth;
using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app, StartupSettings settings)
    {
        var users = app.MapGroup("/admin/users")
            .WithTags("Admin")
            .RequireAuthorization(AdministrativePermissions.ManageUsers);

        users.MapGet("/", async (AppDbContext db) =>
        {
            var result = await db.Users
                .AsNoTracking()
                .OrderByDescending(user => user.IsActive)
                .ThenBy(user => user.Role)
                .ThenBy(user => user.Email)
                .ToListAsync();
            var assignments = await db.UserAdministrativePermissionSets.AsNoTracking().ToListAsync();
            foreach (var user in result)
            {
                user.PermissionSets = assignments.Where(value => value.UserId == user.Id)
                    .Select(value => value.PermissionSet).ToArray();
            }

            return Results.Ok(result);
        });

        users.MapPost("/", async (AdminUserRequest request, AppDbContext db) =>
        {
            if (HasPrivilegeFields(request))
            {
                return EndpointSupport.ValidationProblem("role", "Administrative permissions must be assigned separately.");
            }
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
                Role = UserRole.User,
                IsActive = normalized.IsActive,
                CreatedUtc = DateTime.UtcNow,
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/admin/users/{user.Id}", user);
        });

        users.MapPost("/{id:guid}/invitation-email", async (
            Guid id,
            AppDbContext db,
            IEmailSender emailSender,
            IOptions<EmailSettings> emailSettingsAccessor,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            AdministrativeMutationCoordinator coordinator,
            AdministrativeAccountGuard guard,
            ICurrentUserAccessor currentUserAccessor,
            CancellationToken cancellationToken) =>
        {
            return await coordinator.ExecuteAsync<IResult>(async () =>
            {
                var actorId = currentUserAccessor.TryGetUserId(httpContext.User);
                if (!actorId.HasValue || !await guard.CanModifyAsync(actorId.Value, id, cancellationToken))
                {
                    return Results.Forbid();
                }
                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == id, cancellationToken);
                if (user is null)
                {
                    return Results.NotFound();
                }

                if (!user.IsActive)
                {
                    return EndpointSupport.ValidationProblem("isActive", "Only active users can be invited to sign in.");
                }

                var loginUrl = BuildLoginUrl(settings);
                var logger = loggerFactory.CreateLogger("Glovelly.UserInvitations");

                try
                {
                    await emailSender.SendAsync(
                        new EmailMessage(
                            To: [new EmailAddress(user.Email, user.DisplayName)],
                            Subject: "You have been invited to Glovelly",
                            PlainTextBody: BuildInvitationPlainTextBody(user, loginUrl),
                            From: EmailSenderSupport.ResolveConfiguredFromAddress(
                                emailSettingsAccessor.Value,
                                EmailUseCase.UserInvitations),
                            HtmlBody: BuildInvitationHtmlBody(user, loginUrl)),
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed to dispatch user invitation email for user {UserId}.",
                        user.Id);
                    return Results.Problem(
                        title: "Unable to send invitation email",
                        detail: "The user was saved, but the invitation email could not be sent right now.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                logger.LogInformation("User invitation email sent for user {UserId}.", user.Id);

                return Results.NoContent();
            }, cancellationToken);
        });

        users.MapPut("/{id:guid}", async (
            Guid id,
            AdminUserRequest request,
            AppDbContext db,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AdministrativeMutationCoordinator coordinator,
            AdministrativeAccountGuard guard,
            CancellationToken cancellationToken) =>
        {
            return await coordinator.ExecuteAsync<IResult>(async () =>
            {
                if (HasPrivilegeFields(request))
                {
                    return EndpointSupport.ValidationProblem("role", "Administrative permissions must be assigned separately.");
                }
                var actorId = currentUserAccessor.TryGetUserId(principal);
                if (!actorId.HasValue || !await guard.CanModifyAsync(actorId.Value, id, cancellationToken))
                {
                    return Results.Forbid();
                }
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
                    return EndpointSupport.ValidationProblem("googleSubject", "Google subject cannot be changed after enrolment.");
                }

                var currentUserId = currentUserAccessor.TryGetUserId(principal);
                if (currentUserId == user.Id)
                {
                    if (!normalized.IsActive)
                    {
                        return EndpointSupport.ValidationProblem("isActive", "You cannot deactivate your own administrator account.");
                    }
                }
                if (!normalized.IsActive && await guard.WouldRemoveLastUsableSuperAsync(user, cancellationToken))
                {
                    return Results.Conflict(new { message = "At least one active enrolled Super Administrator must remain." });
                }

                user.Email = normalized.Email;
                user.DisplayName = normalized.DisplayName;
                user.GoogleSubject = normalized.GoogleSubject;
                user.MileageRate = normalized.MileageRate;
                user.PassengerMileageRate = normalized.PassengerMileageRate;
                user.IsActive = normalized.IsActive;

                await db.SaveChangesAsync();

                user.PermissionSets = await db.UserAdministrativePermissionSets.AsNoTracking()
                    .Where(value => value.UserId == user.Id)
                    .Select(value => value.PermissionSet).ToListAsync(cancellationToken);
                return Results.Ok(user);
            }, cancellationToken);
        });

        users.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AdministrativeMutationCoordinator coordinator,
            AdministrativeAccountGuard guard,
            CancellationToken cancellationToken) =>
        {
            return await coordinator.ExecuteAsync<IResult>(async () =>
            {
                var actorId = currentUserAccessor.TryGetUserId(principal);
                if (!actorId.HasValue || !await guard.CanModifyAsync(actorId.Value, id, cancellationToken))
                {
                    return Results.Forbid();
                }
                var user = await db.Users.FirstOrDefaultAsync(value => value.Id == id);
                if (user is null)
                {
                    return Results.NotFound();
                }

                var currentUserId = currentUserAccessor.TryGetUserId(principal);
                if (currentUserId == user.Id)
                {
                    return EndpointSupport.ValidationProblem("id", "You cannot delete your own administrator account.");
                }

                if (user.IsActive)
                {
                    return EndpointSupport.ValidationProblem("isActive", "Only inactive users can be deleted.");
                }

                var assignments = await db.UserAdministrativePermissionSets
                    .Where(value => value.UserId == user.Id).ToListAsync(cancellationToken);
                if (assignments.Count > 0)
                {
                    var actor = await db.Users.AsNoTracking().FirstAsync(value => value.Id == actorId, cancellationToken);
                    db.AdministrativePermissionChanges.Add(new AdministrativePermissionChange
                    {
                        Id = Guid.NewGuid(),
                        TargetUserId = user.Id,
                        TargetIdentity = user.Email,
                        ActorUserId = actor.Id,
                        ActorIdentity = actor.Email,
                        Source = "User",
                        ChangedAtUtc = DateTimeOffset.UtcNow,
                        PreviousSets = JsonSerializer.Serialize(assignments.Select(value => value.PermissionSet.ToString()).Order()),
                        NewSets = "[]",
                        Reason = "Account deleted",
                    });
                }
                db.Users.Remove(user);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }, cancellationToken);
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

    private static bool HasPrivilegeFields(AdminUserRequest request) =>
        request.Role is not null || (request.Extra is not null && request.Extra.Keys.Any(key =>
            key.Equals("permissionSets", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("permissions", StringComparison.OrdinalIgnoreCase)));

    internal static string BuildLoginUrl(StartupSettings settings)
    {
        return settings.BuildPublicUrl("/auth/login");
    }

    internal static string BuildInvitationPlainTextBody(User user, string loginUrl)
    {
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;

        return string.Join(Environment.NewLine, [
            $"Hi {displayName},",
            string.Empty,
            "You have been invited to use Glovelly.",
            string.Empty,
            "Accept this invitation by signing in with Google using this email address:",
            user.Email,
            string.Empty,
            "Accept invitation and sign in:",
            loginUrl,
            string.Empty,
            "If you were not expecting this invitation, you can ignore this email.",
        ]);
    }

    internal static string BuildInvitationHtmlBody(User user, string loginUrl)
    {
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
        var encodedDisplayName = EmailHtmlRenderer.Encode(displayName);
        var encodedEmail = EmailHtmlRenderer.Encode(user.Email);
        var encodedLoginUrl = EmailHtmlRenderer.Encode(loginUrl);

        return EmailHtmlRenderer.RenderDocument(
            "You're invited",
            "Accept your invitation by signing in to Glovelly with Google.",
            $$"""
                  <div class="message-copy">
                    <p>Hi {{encodedDisplayName}},</p>
                    <p>You have been invited to use Glovelly. Accept this invitation by signing in with Google using <strong>{{encodedEmail}}</strong>.</p>
                    <p><a class="button" href="{{encodedLoginUrl}}">Accept invitation and sign in</a></p>
                  </div>
                  <div class="info-note">
                    <p>If you were not expecting this invitation, you can ignore this email.</p>
                  </div>
            """);
    }

    private sealed record AdminUserRequest(
        string Email,
        string? DisplayName,
        string? GoogleSubject,
        decimal? MileageRate,
        decimal? PassengerMileageRate,
        string? Role,
        bool IsActive)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }
    }

    private sealed record NormalizedAdminUserRequest(
        string Email,
        string? DisplayName,
        string? GoogleSubject,
        decimal? MileageRate,
        decimal? PassengerMileageRate,
        UserRole Role,
        bool IsActive);
}
