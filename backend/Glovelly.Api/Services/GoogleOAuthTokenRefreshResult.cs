namespace Glovelly.Api.Services;

public sealed record GoogleOAuthTokenRefreshResult(
    bool IsSuccess,
    int StatusCode,
    string ResponseBody,
    GoogleOAuthTokenResponse? TokenResponse = null);
