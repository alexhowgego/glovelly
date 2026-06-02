using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IGoogleConnectionService
{
    Task<GoogleConnection> SaveConnectionAsync(
        Guid userId,
        GoogleOAuthTokenResponse tokenResponse,
        CancellationToken cancellationToken);

    Task<GoogleConnection?> GetActiveConnectionAsync(
        Guid userId,
        IEnumerable<string> requiredScopes,
        CancellationToken cancellationToken);

    Task<GoogleConnectionAccessToken> GetAccessTokenAsync(
        GoogleConnection connection,
        IEnumerable<string> requiredScopes,
        CancellationToken cancellationToken);

    bool HasScopes(GoogleConnection connection, IEnumerable<string> requiredScopes);
}
