using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed class GcsBlobStore(
    StorageClient storageClient,
    IOptions<BlobStorageSettings> options) : IBlobStore
{
    private readonly string _bucketName = options.Value.BucketName
        ?? throw new InvalidOperationException("Blob storage bucket is not configured.");

    public async Task SaveAsync(
        BlobWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await storageClient.UploadObjectAsync(
            _bucketName,
            request.Key,
            request.ContentType,
            request.Content,
            cancellationToken: cancellationToken);
    }

    public async Task<BlobReadResult> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var memory = new MemoryStream();
        var obj = await storageClient.DownloadObjectAsync(
            _bucketName,
            key,
            memory,
            cancellationToken: cancellationToken);

        memory.Position = 0;
        return new BlobReadResult(memory, obj.ContentType, (long?)obj.Size);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await storageClient.DeleteObjectAsync(_bucketName, key, cancellationToken: cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.Error?.Code == StatusCodes.Status404NotFound)
        {
        }
    }
}
