using System.Collections.Concurrent;

namespace Glovelly.Api.Services;

public sealed class InMemoryExpenseAttachmentStore : IExpenseAttachmentStore
{
    private readonly ConcurrentDictionary<string, StoredAttachment> _attachments = new();

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        _attachments[storageKey] = new StoredAttachment(memory.ToArray(), contentType);
    }

    public Task<ExpenseAttachmentContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        if (!_attachments.TryGetValue(storageKey, out var attachment))
        {
            throw new FileNotFoundException("Expense attachment blob was not found.", storageKey);
        }

        return Task.FromResult(new ExpenseAttachmentContent(
            new MemoryStream(attachment.Content, writable: false),
            attachment.ContentType));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _attachments.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }

    private sealed record StoredAttachment(byte[] Content, string ContentType);
}
