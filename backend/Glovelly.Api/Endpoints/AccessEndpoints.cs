using Glovelly.Api.Configuration;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
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
            .WithTags("Access");
        var environmentLabel = ResolveEnvironmentLabel(settings);

        access.MapPost("/request", async (
            AccessRequestSubmission? request,
            ClaimsPrincipal user,
            AppDbContext dbContext,
            IEmailSender emailSender,
            Microsoft.Extensions.Options.IOptions<EmailSettings> emailSettingsAccessor,
            AccessRequestWorkflowService workflowService,
            HttpContext httpContext,
            IDataProtectionProvider dataProtectionProvider,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Glovelly.AccessRequests");
            var requestedAtUtc = DateTimeOffset.UtcNow;
            var requester =
                AccessRequestEmailRequest.FromClaims(user, requestedAtUtc) ??
                AccessRequestEmailRequest.FromToken(
                    request?.AccessRequestToken,
                    dataProtectionProvider,
                    requestedAtUtc,
                    logger);

            if (requester is null)
            {
                return Results.Unauthorized();
            }

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
                            From: EmailSenderSupport.ResolveConfiguredFromAddress(
                                emailSettingsAccessor.Value,
                                EmailUseCase.AccessRequests),
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
        .RequireRateLimiting("PublicAccessRequest")
        .AllowAnonymous();

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
        var encodedEmail = EmailHtmlRenderer.Encode(requester.Email);
        var encodedDisplayName = EmailHtmlRenderer.Encode(requester.DisplayName ?? "Not provided");
        var encodedEnvironment = EmailHtmlRenderer.Encode(environmentLabel);
        var encodedTimestamp = EmailHtmlRenderer.Encode(
            requester.RequestedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
        var encodedSubject = EmailHtmlRenderer.Encode(requester.Subject ?? "Not provided");

        return EmailHtmlRenderer.RenderDocument(
            "Access Request",
            "A user has successfully authenticated and is asking to be enrolled in Glovelly.",
            $$"""
                  <div class="info-note">
                    <p class="section-label">Environment</p>
                    <p>{{encodedEnvironment}}</p>
                  </div>
                  <table class="details">
                    <tr>
                      <th>User email</th>
                      <td>{{encodedEmail}}</td>
                    </tr>
                    <tr>
                      <th>Display name</th>
                      <td>{{encodedDisplayName}}</td>
                    </tr>
                    <tr>
                      <th>Timestamp</th>
                      <td>{{encodedTimestamp}}</td>
                    </tr>
                    <tr>
                      <th>Identity subject</th>
                      <td>{{encodedSubject}}</td>
                    </tr>
                  </table>
                  <div class="message-copy">
                    <p>Review this user in the target environment and grant access if appropriate.</p>
                  </div>
            """);
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
        public static AccessRequestEmailRequest? FromClaims(ClaimsPrincipal user, DateTimeOffset requestedAtUtc)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var email = AuthFlowSupport.NormalizeEmail(
                user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email)) ?? string.Empty;
            var displayName = Normalize(user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name));
            var subject = Normalize(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier));

            return new AccessRequestEmailRequest(email, displayName, subject, requestedAtUtc);
        }

        public static AccessRequestEmailRequest? FromToken(
            string? accessRequestToken,
            IDataProtectionProvider dataProtectionProvider,
            DateTimeOffset requestedAtUtc,
            ILogger logger)
        {
            var tokenIdentity = AuthFlowSupport.ReadAccessRequestIdentity(
                accessRequestToken,
                dataProtectionProvider,
                logger);

            return tokenIdentity is null
                ? null
                : new AccessRequestEmailRequest(
                    tokenIdentity.Email,
                    tokenIdentity.DisplayName,
                    tokenIdentity.Subject,
                    requestedAtUtc);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    internal sealed record AccessRequestSubmission(string? AccessRequestToken);
}
