using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

internal static class AccessRequestAdminEndpoints
{
    public static IEndpointRouteBuilder MapAccessRequestAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var requests = app.MapGroup("/admin/access-requests")
            .WithTags("Admin")
            .RequireAuthorization(GlovellyPolicies.AdminUser);

        requests.MapGet("/", async (AccessRequestReviewService reviewService, CancellationToken cancellationToken) =>
            Results.Ok(await reviewService.ListPendingAsync(cancellationToken)));

        requests.MapGet("/{id:guid}", async (Guid id, AccessRequestReviewService reviewService, CancellationToken cancellationToken) =>
        {
            var request = await reviewService.GetAsync(id, cancellationToken);
            return request is null ? Results.NotFound() : Results.Ok(request);
        });

        requests.MapPost("/{id:guid}/approve", async (
            Guid id,
            ApproveAccessRequest request,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AccessRequestReviewService reviewService,
            AppDbContext dbContext,
            IWorkspaceEventPublisher workspaceEventPublisher,
            IEmailSender emailSender,
            IOptions<EmailSettings> emailSettingsAccessor,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<UserRole>(request.Role?.Trim(), true, out var role))
            {
                return EndpointSupport.ValidationProblem("role", "A valid user role is required.");
            }

            var reviewerId = currentUserAccessor.TryGetUserId(principal);
            if (!reviewerId.HasValue)
            {
                return Results.Forbid();
            }

            var decision = await reviewService.ApproveAsync(
                id, reviewerId.Value, role, request.IsActive, request.DecisionNote, cancellationToken);
            if (decision.AccessRequest is null)
            {
                return Results.NotFound();
            }

            if (decision.DecisionApplied)
            {
                await PublishDecisionAsync(
                    dbContext,
                    workspaceEventPublisher,
                    decision.AccessRequest.Id,
                    "provisioned",
                    loggerFactory,
                    cancellationToken);
            }

            var invitationEmailSent = (bool?)null;
            if (decision.DecisionApplied && decision.UserCreated && request.SendInvitationEmail && decision.AccessRequest.ProvisionedUserId.HasValue)
            {
                invitationEmailSent = await TrySendInvitationAsync(
                    decision.AccessRequest, emailSender, emailSettingsAccessor.Value, httpContext, loggerFactory, cancellationToken);
            }

            return Results.Ok(new
            {
                accessRequest = decision.AccessRequest,
                decisionApplied = decision.DecisionApplied,
                userCreated = decision.UserCreated,
                existingUser = decision.ExistingUser,
                invitationEmailSent
            });
        });

        requests.MapPost("/{id:guid}/decline", async (
            Guid id,
            DeclineAccessRequest request,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AccessRequestReviewService reviewService,
            AppDbContext dbContext,
            IWorkspaceEventPublisher workspaceEventPublisher,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var reviewerId = currentUserAccessor.TryGetUserId(principal);
            if (!reviewerId.HasValue)
            {
                return Results.Forbid();
            }

            var decision = await reviewService.DeclineAsync(id, reviewerId.Value, request.DecisionNote, cancellationToken);
            if (decision.AccessRequest is not null && decision.DecisionApplied)
            {
                await PublishDecisionAsync(
                    dbContext,
                    workspaceEventPublisher,
                    decision.AccessRequest.Id,
                    "declined",
                    loggerFactory,
                    cancellationToken);
            }
            return decision.AccessRequest is null
                ? Results.NotFound()
                : Results.Ok(new { accessRequest = decision.AccessRequest, decisionApplied = decision.DecisionApplied });
        });

        return app;
    }

    private static async Task PublishDecisionAsync(
        AppDbContext dbContext,
        IWorkspaceEventPublisher workspaceEventPublisher,
        Guid accessRequestId,
        string action,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var administratorIds = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.IsActive && user.Role == UserRole.Admin)
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
            var workspaceEvent = new WorkspaceEvent(
                "access-requests",
                action,
                accessRequestId,
                DateTimeOffset.UtcNow);
            await Task.WhenAll(administratorIds.Select(administratorId =>
                workspaceEventPublisher.PublishAsync(administratorId, workspaceEvent, cancellationToken)));
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("Glovelly.AccessRequests")
                .LogWarning(exception, "Failed to publish access-request decision workspace events.");
        }
    }

    private static async Task<bool> TrySendInvitationAsync(
        AccessRequest request,
        IEmailSender emailSender,
        EmailSettings emailSettings,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName
        };
        var loginUrl = AdminEndpoints.BuildLoginUrl(httpContext);

        try
        {
            await emailSender.SendAsync(new EmailMessage(
                To: [new EmailAddress(request.Email, request.DisplayName)],
                Subject: "You have been invited to Glovelly",
                PlainTextBody: AdminEndpoints.BuildInvitationPlainTextBody(user, loginUrl),
                From: EmailSenderSupport.ResolveConfiguredFromAddress(emailSettings, EmailUseCase.UserInvitations),
                HtmlBody: AdminEndpoints.BuildInvitationHtmlBody(user, loginUrl)), cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("Glovelly.UserInvitations").LogError(exception,
                "Failed to dispatch user invitation email for approved access request {AccessRequestId}.", request.Id);
            return false;
        }
    }

    private sealed record ApproveAccessRequest(string? Role, bool IsActive, bool SendInvitationEmail, string? DecisionNote);
    private sealed record DeclineAccessRequest(string? DecisionNote);
}
