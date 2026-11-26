using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoicePdfService
{
    Task<InvoicePdfCandidate> PrepareAsync(
        Invoice invoice, Guid? userId, byte[] content, bool hasDraftWatermark,
        CancellationToken cancellationToken = default);

    void ApplyCandidate(Invoice invoice, InvoicePdfCandidate candidate);

    Task SaveGeneratedPdfAsync(
        Invoice invoice,
        Guid? userId,
        byte[] content,
        bool hasDraftWatermark,
        CancellationToken cancellationToken = default);

    Task<InvoicePdfContent?> OpenReadAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default);

    Task<InvoicePdfReadResult> OpenCurrentReadAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Invoice invoice, CancellationToken cancellationToken = default);
}

public sealed record InvoicePdfCandidate(
    string StorageKey, string FileName, long SizeBytes, DateTimeOffset GeneratedAtUtc,
    int DocumentRevision, bool HasDraftWatermark);

public sealed record InvoicePdfContent(
    Stream Content,
    string ContentType,
    long SizeBytes);

public sealed record InvoicePdfReadResult(
    InvoicePdfContent? Pdf,
    string? UnavailableMessage)
{
    public bool IsAvailable => Pdf is not null;
}
