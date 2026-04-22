using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Glovelly.Api.Endpoints;

internal static class AccessEndpoints
{
    private const string GenericSuccessMessage = "Access request submitted.";

    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder app, StartupSettings settings)
    {
        var access = app.MapGroup("/access")
            .WithTags("Access")
            .RequireAuthorization();
        var environmentLabel = ResolveEnvironmentLabel(settings);

        access.MapPost("/request", [Authorize] async (
            ClaimsPrincipal user,
            AppDbContext dbContext,
            IEmailSender emailSender,
            AccessRequestWorkflowService workflowService,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Glovelly.AccessRequests");
            var requester = AccessRequestEmailRequest.FromClaims(user);

            if (string.IsNullOrWhiteSpace(requester.Email))
            {
                logger.LogWarning(
                    "Access request rejected because the authenticated principal did not include an email address. Subject: {Subject}",
                    requester.Subject ?? "(missing)");
                return Results.BadRequest(new { message = "A verified email address is required to request access." });
            }

            var workflowResult = await workflowService.RecordAsync(
                requester.Email,
                requester.DisplayName,
                requester.Subject,
                AccessRequestRequestContext.ResolveRemoteIpAddress(httpContext),
                cancellationToken);
            var notificationRequest = requester with
            {
                RequestedAtUtc = workflowResult.AccessRequest.RequestedAtUtc
            };

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

            if (!workflowResult.ShouldSendNotification)
            {
                logger.LogInformation(
                    "Access request notification suppressed.");
                return Results.Ok(new { message = GenericSuccessMessage });
            }

            if (recipients.Length == 0)
            {
                logger.LogWarning(
                    "Access request recorded, but no administrator recipients were available for notification.");
                return Results.Ok(new { message = GenericSuccessMessage });
            }

            try
            {
                logger.LogInformation(
                    "Attempting access request notification send to {RecipientCount} administrator recipients.",
                    recipients.Length);

                foreach (var recipient in recipients)
                {
                    await emailSender.SendAsync(
                        new EmailMessage(
                            To: [new EmailAddress(recipient)],
                            Subject: "Glovelly access request",
                            PlainTextBody: BuildPlainTextBody(notificationRequest, environmentLabel),
                            HtmlBody: BuildHtmlBody(notificationRequest, environmentLabel)),
                        cancellationToken);
                }

                await workflowService.MarkNotificationSentAsync(
                    workflowResult.AccessRequest.Id,
                    cancellationToken);
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

            return Results.Ok(new { message = GenericSuccessMessage });
        })
        .RequireRateLimiting("PublicAccessRequest");

        return app;
    }

    private static string BuildPlainTextBody(AccessRequestEmailRequest requester, string environmentLabel)
    {
        var lines = new List<string>
        {
            "Glovelly access request",
            "=======================",
            string.Empty,
            "A user has successfully authenticated and is asking to be enrolled in Glovelly.",
            string.Empty,
            FormattableString.Invariant($"Environment: {environmentLabel}"),
            FormattableString.Invariant($"User email: {requester.Email}"),
        };

        if (!string.IsNullOrWhiteSpace(requester.DisplayName))
        {
            lines.Add(FormattableString.Invariant($"User display name: {requester.DisplayName}"));
        }

        lines.Add(FormattableString.Invariant($"Timestamp: {requester.RequestedAtUtc:yyyy-MM-dd HH:mm:ss 'UTC'}"));

        if (!string.IsNullOrWhiteSpace(requester.Subject))
        {
            lines.Add(FormattableString.Invariant($"Identity subject: {requester.Subject}"));
        }

        lines.Add(string.Empty);
        lines.Add("Review this user in the target environment and grant access if appropriate.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildHtmlBody(AccessRequestEmailRequest requester, string environmentLabel)
    {
        var encodedEmail = WebUtility.HtmlEncode(requester.Email);
        var encodedDisplayName = WebUtility.HtmlEncode(requester.DisplayName ?? "Not provided");
        var encodedEnvironment = WebUtility.HtmlEncode(environmentLabel);
        var encodedTimestamp = WebUtility.HtmlEncode(
            requester.RequestedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
        var encodedSubject = WebUtility.HtmlEncode(requester.Subject ?? "Not provided");

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <body style="margin:0;padding:24px;background:#f5efe7;font-family:'Avenir Next','Segoe UI',sans-serif;color:#21313c;">
                <div style="max-width:640px;margin:0 auto;background:#fffdf9;border:1px solid #e5d8ca;border-radius:24px;overflow:hidden;box-shadow:0 18px 45px rgba(39,31,24,0.08);">
                    <div style="padding:24px 28px;background:linear-gradient(135deg,#17324d,#255a7a);color:#ffffff;">
                        <div style="font-size:12px;letter-spacing:0.18em;text-transform:uppercase;opacity:0.82;">Glovelly</div>
                        <h1 style="margin:12px 0 0;font-size:28px;line-height:1.05;font-family:Georgia,serif;">Access Request</h1>
                        <p style="margin:12px 0 0;font-size:15px;line-height:1.6;color:rgba(255,255,255,0.88);">
                            A user has successfully authenticated and is asking to be enrolled in Glovelly.
                        </p>
                    </div>
                    <div style="padding:28px;">
                        <div style="display:inline-block;padding:8px 12px;border-radius:999px;background:#efe4d7;color:#8c4920;font-size:12px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;">
                            {{encodedEnvironment}}
                        </div>
                        <table style="width:100%;margin-top:20px;border-collapse:collapse;">
                            <tr>
                                <td style="padding:12px 0;border-bottom:1px solid #efe3d6;font-weight:700;width:180px;">User email</td>
                                <td style="padding:12px 0;border-bottom:1px solid #efe3d6;">{{encodedEmail}}</td>
                            </tr>
                            <tr>
                                <td style="padding:12px 0;border-bottom:1px solid #efe3d6;font-weight:700;">Display name</td>
                                <td style="padding:12px 0;border-bottom:1px solid #efe3d6;">{{encodedDisplayName}}</td>
                            </tr>
                            <tr>
                                <td style="padding:12px 0;border-bottom:1px solid #efe3d6;font-weight:700;">Timestamp</td>
                                <td style="padding:12px 0;border-bottom:1px solid #efe3d6;">{{encodedTimestamp}}</td>
                            </tr>
                            <tr>
                                <td style="padding:12px 0;font-weight:700;">Identity subject</td>
                                <td style="padding:12px 0;">{{encodedSubject}}</td>
                            </tr>
                        </table>
                        <p style="margin:24px 0 0;font-size:14px;line-height:1.7;color:#52606b;">
                            Review this user in the target environment and grant access if appropriate.
                        </p>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    private static string ResolveEnvironmentLabel(StartupSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DeploymentName))
        {
            return settings.DeploymentName.Trim();
        }

        if (settings.IsTesting)
        {
            return "Testing";
        }

        if (settings.IsDevelopment)
        {
            return "Development";
        }

        return "Production";
    }

    private sealed record AccessRequestEmailRequest(
        string Email,
        string? DisplayName,
        string? Subject,
        DateTimeOffset RequestedAtUtc)
    {
        public static AccessRequestEmailRequest FromClaims(ClaimsPrincipal user)
        {
            var email = AuthFlowSupport.NormalizeEmail(
                user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email)) ?? string.Empty;
            var displayName = Normalize(user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name));
            var subject = Normalize(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier));

            return new AccessRequestEmailRequest(email, displayName, subject, DateTimeOffset.MinValue);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
