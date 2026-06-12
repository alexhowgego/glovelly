using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

        users.MapPost("/{id:guid}/invitation-email", async (
            Guid id,
            AppDbContext db,
            IEmailSender emailSender,
            IOptions<EmailSettings> emailSettingsAccessor,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
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

            var loginUrl = BuildLoginUrl(httpContext);
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
                return EndpointSupport.ValidationProblem("googleSubject", "Google subject cannot be changed after enrolment.");
            }

            var currentUserId = currentUserAccessor.TryGetUserId(principal);
            if (currentUserId == user.Id)
            {
                if (!normalized.IsActive)
                {
                    return EndpointSupport.ValidationProblem("isActive", "You cannot deactivate your own administrator account.");
                }

                if (normalized.Role != UserRole.Admin)
                {
                    return EndpointSupport.ValidationProblem("role", "You cannot remove administrator access from your own account.");
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

        users.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor) =>
        {
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

            db.Users.Remove(user);
            await db.SaveChangesAsync();

            return Results.NoContent();
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

    private static string BuildLoginUrl(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var baseUri = new UriBuilder(request.Scheme, request.Host.Host)
        {
            Path = request.PathBase.Add("/auth/login").Value,
        };

        if (request.Host.Port.HasValue)
        {
            baseUri.Port = request.Host.Port.Value;
        }

        return baseUri.Uri.ToString();
    }

    private static string BuildInvitationPlainTextBody(User user, string loginUrl)
    {
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;

        return string.Join(Environment.NewLine, [
            $"Hi {displayName},",
            string.Empty,
            "You have been invited to use Glovelly.",
            string.Empty,
            "Sign in with Google using this email address:",
            user.Email,
            string.Empty,
            "Open Glovelly:",
            loginUrl,
            string.Empty,
            "If you were not expecting this invitation, you can ignore this email.",
        ]);
    }

    private static string BuildInvitationHtmlBody(User user, string loginUrl)
    {
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
        var encodedDisplayName = EmailHtmlRenderer.Encode(displayName);
        var encodedEmail = EmailHtmlRenderer.Encode(user.Email);
        var encodedLoginUrl = EmailHtmlRenderer.Encode(loginUrl);

        return EmailHtmlRenderer.RenderDocument(
            "You're invited",
            "Your Glovelly account is ready for you to sign in.",
            $$"""
                  <div class="message-copy">
                    <p>Hi {{encodedDisplayName}},</p>
                    <p>You have been invited to use Glovelly. Sign in with Google using <strong>{{encodedEmail}}</strong>.</p>
                    <p><a class="button" href="{{encodedLoginUrl}}">Open Glovelly</a></p>
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
