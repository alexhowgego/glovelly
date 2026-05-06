using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed class GcsExpenseAttachmentStore(
    StorageClient storageClient,
    IOptions<ExpenseAttachmentSettings> options) : IExpenseAttachmentStore
{
    private readonly string _bucketName = options.Value.BucketName
        ?? throw new InvalidOperationException("Expense attachment bucket is not configured.");

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await storageClient.UploadObjectAsync(
            _bucketName,
            storageKey,
            contentType,
            content,
            cancellationToken: cancellationToken);
    }

    public async Task<ExpenseAttachmentContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var memory = new MemoryStream();
        var obj = await storageClient.DownloadObjectAsync(
            _bucketName,
            storageKey,
            memory,
            cancellationToken: cancellationToken);

        memory.Position = 0;
        return new ExpenseAttachmentContent(memory, obj.ContentType);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await storageClient.DeleteObjectAsync(_bucketName, storageKey, cancellationToken: cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.Error?.Code == StatusCodes.Status404NotFound)
        {
        }
    }
}
