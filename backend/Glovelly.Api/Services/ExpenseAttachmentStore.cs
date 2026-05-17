namespace Glovelly.Api.Services;

public sealed class ExpenseAttachmentStore(IBlobStore blobStore) : IExpenseAttachmentStore
{
    public Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return blobStore.SaveAsync(
            new BlobWriteRequest(storageKey, content, contentType),
            cancellationToken);
    }

    public async Task<ExpenseAttachmentContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var blob = await blobStore.OpenReadAsync(storageKey, cancellationToken);
        return new ExpenseAttachmentContent(blob.Content, blob.ContentType);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return blobStore.DeleteAsync(storageKey, cancellationToken);
    }
}
