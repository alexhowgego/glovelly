using System.Security.Claims;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GoogleDriveIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapGoogleDriveIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var googleDrive = app.MapGroup("/integrations/google-drive")
            .WithTags("Integrations")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        googleDrive.MapPost("/disconnect", async (
            ClaimsPrincipal principal,
            ICurrentUserAccessor currentUserAccessor,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var currentUserId = await GetActiveCurrentUserIdAsync(
                principal,
                currentUserAccessor,
                dbContext);
            if (!currentUserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var connection = await dbContext.GoogleConnections
                .FirstOrDefaultAsync(value => value.UserId == currentUserId.Value, cancellationToken);
            if (connection is not null)
            {
                connection.GrantedScopes = GoogleScopes.Remove(connection.GrantedScopes, GoogleScopes.DriveFile);
                if (string.IsNullOrWhiteSpace(connection.GrantedScopes))
                {
                    connection.RevokedAtUtc = now;
                    connection.EncryptedAccessToken = string.Empty;
                    connection.EncryptedRefreshToken = string.Empty;
                }
                connection.UpdatedAtUtc = now;
            }

            var driveSettings = await dbContext.GoogleDriveIntegrationSettings
                .FirstOrDefaultAsync(value => value.UserId == currentUserId.Value, cancellationToken);
            if (driveSettings is not null)
            {
                driveSettings.InvoiceUploadFolderId = null;
                driveSettings.UpdatedAtUtc = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        return app;
    }

    private static async Task<Guid?> GetActiveCurrentUserIdAsync(
        ClaimsPrincipal principal,
        ICurrentUserAccessor currentUserAccessor,
        AppDbContext dbContext)
    {
        var currentUserId = currentUserAccessor.TryGetUserId(principal);
        if (!currentUserId.HasValue)
        {
            return null;
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == currentUserId.Value && user.IsActive);

        return userExists ? currentUserId.Value : null;
    }

}
