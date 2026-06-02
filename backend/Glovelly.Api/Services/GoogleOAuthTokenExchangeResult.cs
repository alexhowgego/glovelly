namespace Glovelly.Api.Services;

public sealed record GoogleOAuthTokenExchangeResult(
    bool IsSuccess,
    int StatusCode,
    string ResponseBody,
    GoogleOAuthTokenResponse? TokenResponse = null);
