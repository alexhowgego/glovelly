using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var access = app.MapGroup("/access")
            .WithTags("Access")
            .RequireAuthorization();

        access.MapPost("/request", [Authorize] async (
            ClaimsPrincipal user,
            AppDbContext dbContext,
            IEmailSender emailSender,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Glovelly.AccessRequests");
            var requestedAtUtc = DateTimeOffset.UtcNow;
            var requester = AccessRequestEmailRequest.FromClaims(user, requestedAtUtc);

            if (string.IsNullOrWhiteSpace(requester.Email))
            {
                logger.LogWarning(
                    "Access request rejected because the authenticated principal did not include an email address. Subject: {Subject}",
                    requester.Subject ?? "(missing)");
                return Results.BadRequest(new { message = "A verified email address is required to request access." });
            }

            var administrators = await dbContext.Users
                .AsNoTracking()
                .Where(value => value.IsActive && value.Role == UserRole.Admin)
                .OrderBy(value => value.Email)
                .ToListAsync(cancellationToken);

            var recipients = administrators
                .Where(admin => !string.IsNullOrWhiteSpace(admin.Email))
                .Select(admin => admin.Email.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var skippedAdminCount = administrators.Count - recipients.Length;
            if (skippedAdminCount > 0)
            {
                logger.LogWarning(
                    "Skipped {SkippedAdminCount} administrator access-request recipients because no valid email address was available.",
                    skippedAdminCount);
            }

            try
            {
                foreach (var recipient in recipients)
                {
                    await emailSender.SendAsync(
                        new EmailMessage(
                            To: [new EmailAddress(recipient)],
                            Subject: "Glovelly access request",
                            PlainTextBody: BuildPlainTextBody(requester)),
                        cancellationToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to dispatch access request notification for requester subject {RequesterSubject}.",
                    requester.Subject ?? "(missing)");
                return Results.Problem(
                    title: "Unable to submit access request",
                    detail: "We couldn't submit your access request right now. Please try again shortly.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            logger.LogInformation(
                "Access request submitted for requester subject {RequesterSubject} to {RecipientCount} administrator recipients.",
                requester.Subject ?? "(missing)",
                recipients.Length);

            return Results.Ok(new { message = "Access request submitted." });
        });

        return app;
    }

    private static string BuildPlainTextBody(AccessRequestEmailRequest requester)
    {
        var lines = new List<string>
        {
            "A Glovelly user has requested access.",
            string.Empty,
            FormattableString.Invariant($"User email: {requester.Email}"),
        };

        if (!string.IsNullOrWhiteSpace(requester.DisplayName))
        {
            lines.Add(FormattableString.Invariant($"User display name: {requester.DisplayName}"));
        }

        lines.Add(FormattableString.Invariant($"Timestamp: {requester.RequestedAtUtc:yyyy-MM-dd HH:mm:ss 'UTC'}"));

        return string.Join(Environment.NewLine, lines);
    }

    private sealed record AccessRequestEmailRequest(
        string Email,
        string? DisplayName,
        string? Subject,
        DateTimeOffset RequestedAtUtc)
    {
        public static AccessRequestEmailRequest FromClaims(ClaimsPrincipal user, DateTimeOffset requestedAtUtc)
        {
            var email = AuthFlowSupport.NormalizeEmail(
                user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email)) ?? string.Empty;
            var displayName = Normalize(user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name));
            var subject = Normalize(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier));

            return new AccessRequestEmailRequest(email, displayName, subject, requestedAtUtc);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
