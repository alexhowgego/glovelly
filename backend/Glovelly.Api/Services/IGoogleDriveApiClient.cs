namespace Glovelly.Api.Services;

public interface IGoogleDriveApiClient
{
    Task<GoogleDriveAccessTokenRefreshResult> RefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken);

    Task<GoogleDriveUploadResult> UploadPdfAsync(
        string accessToken,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);
}
