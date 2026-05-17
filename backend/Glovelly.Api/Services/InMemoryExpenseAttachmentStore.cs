namespace Glovelly.Api.Services;

public sealed class InMemoryExpenseAttachmentStore() : IExpenseAttachmentStore
{
    private readonly ExpenseAttachmentStore _inner = new(new InMemoryBlobStore());

    public Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return _inner.SaveAsync(storageKey, content, contentType, cancellationToken);
    }

    public Task<ExpenseAttachmentContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        return _inner.OpenReadAsync(storageKey, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return _inner.DeleteAsync(storageKey, cancellationToken);
    }
}
