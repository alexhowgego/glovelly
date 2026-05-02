namespace Glovelly.Api.Services;

public sealed record GoogleDriveAccessTokenRefreshResult(
    bool IsSuccess,
    int StatusCode,
    string ResponseBody,
    GoogleDriveOAuthTokenResponse? TokenResponse = null);
