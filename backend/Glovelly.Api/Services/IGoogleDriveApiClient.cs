namespace Glovelly.Api.Services;

public interface IGoogleDriveApiClient
{
    Task<GoogleDriveUploadResult> UploadPdfAsync(
        string accessToken,
        string fileName,
        byte[] content,
        string? parentFolderId,
        CancellationToken cancellationToken);
}
