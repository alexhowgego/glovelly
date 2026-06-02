namespace Glovelly.Api.Services;

public interface IGoogleOAuthTokenClient
{
    Task<GoogleOAuthTokenExchangeResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken);

    Task<GoogleOAuthTokenRefreshResult> RefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken);
}
