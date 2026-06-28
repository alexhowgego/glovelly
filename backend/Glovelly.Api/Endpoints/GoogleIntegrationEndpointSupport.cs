using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GoogleIntegrationEndpointSupport
{
    public static async Task<string> BuildAuthorizationScopeAsync(
        AppDbContext dbContext,
        Guid userId,
        string requiredScope,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var existingScopes = await dbContext.GoogleConnections
            .AsNoTracking()
            .Where(connection =>
                connection.UserId == userId &&
                connection.RevokedAtUtc == null &&
                (connection.RefreshTokenExpiresAtUtc == null || connection.RefreshTokenExpiresAtUtc > now))
            .Select(connection => connection.GrantedScopes)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var authorizationScopes = new List<string>
        {
            GoogleScopes.OpenId,
            GoogleScopes.Email,
            GoogleScopes.Profile,
        };
        authorizationScopes.AddRange(GoogleScopes.MergeManagedIntegrationScopes(existingScopes, requiredScope));

        return GoogleScopes.Join(authorizationScopes.ToArray());
    }
}
