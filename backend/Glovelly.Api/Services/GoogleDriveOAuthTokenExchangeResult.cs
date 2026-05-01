namespace Glovelly.Api.Services;

public sealed record GoogleDriveOAuthTokenExchangeResult(
    bool IsSuccess,
    int StatusCode,
    string ResponseBody);
