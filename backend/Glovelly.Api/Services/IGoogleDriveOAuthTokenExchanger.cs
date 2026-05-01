namespace Glovelly.Api.Services;

public interface IGoogleDriveOAuthTokenExchanger
{
    Task<GoogleDriveOAuthTokenExchangeResult> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken);
}
