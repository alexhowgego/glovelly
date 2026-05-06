namespace Glovelly.Api.Services;

public interface IExpenseAttachmentStore
{
    Task SaveAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<ExpenseAttachmentContent> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed record ExpenseAttachmentContent(Stream Content, string? ContentType);
