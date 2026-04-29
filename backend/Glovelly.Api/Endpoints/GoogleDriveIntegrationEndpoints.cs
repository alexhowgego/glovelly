using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

public static class GoogleDriveIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapGoogleDriveIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var googleDrive = app.MapGroup("/integrations/google-drive")
            .WithTags("Integrations")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        googleDrive.MapGet("/callback", async (
            string? code,
            string? state,
            string? error,
            string? error_description,
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory) =>
        {
            var currentUserId = currentUserAccessor.TryGetUserId(principal);
            if (!currentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var userExists = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == currentUserId.Value && user.IsActive);
            if (!userExists)
            {
                return Results.Unauthorized();
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Problem(
                    title: "Google Drive connection was not approved.",
                    detail: string.IsNullOrWhiteSpace(error_description) ? error : error_description,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var validationErrors = ValidateCallback(code, state);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var logger = loggerFactory.CreateLogger(nameof(GoogleDriveIntegrationEndpoints));
            logger.LogInformation(
                "Received Google Drive OAuth callback for user {UserId}.",
                currentUserId.Value);

            return Results.Redirect("/?integration=google-drive&status=callback-received");
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateCallback(string? code, string? state)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["code"] = ["Google Drive authorization code is required."];
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            errors["state"] = ["Google Drive OAuth state is required."];
        }

        return errors;
    }
}
