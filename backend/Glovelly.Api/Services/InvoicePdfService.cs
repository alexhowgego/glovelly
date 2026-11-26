using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class InvoicePdfService(IBlobStore blobStore, TimeProvider timeProvider) : IInvoicePdfService
{
    public async Task SaveGeneratedPdfAsync(
        Invoice invoice,
        Guid? userId,
        byte[] content,
        bool hasDraftWatermark,
        CancellationToken cancellationToken = default)
    {
        var candidate = await PrepareAsync(invoice, userId, content, hasDraftWatermark, cancellationToken);
        ApplyCandidate(invoice, candidate);
    }

    public async Task<InvoicePdfCandidate> PrepareAsync(
        Invoice invoice, Guid? userId, byte[] content, bool hasDraftWatermark,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(content);
        if (hasDraftWatermark != invoice.RequiresDraftWatermark)
        {
            throw new ArgumentException("Generated PDF watermark must match the invoice state.", nameof(hasDraftWatermark));
        }

        var key = InvoicePdfStorage.BuildStorageKey(invoice, userId);
        await blobStore.SaveAsync(
            new BlobWriteRequest(key, new MemoryStream(content, writable: false), InvoicePdfStorage.ContentType, content.Length),
            cancellationToken);

        return new InvoicePdfCandidate(key, $"{invoice.InvoiceNumber}.pdf", content.Length,
            timeProvider.GetUtcNow(), invoice.DocumentRevision, hasDraftWatermark);
    }

    public void ApplyCandidate(Invoice invoice, InvoicePdfCandidate candidate)
    {
        if (candidate.DocumentRevision != invoice.DocumentRevision ||
            candidate.HasDraftWatermark != invoice.RequiresDraftWatermark)
        {
            throw new InvalidOperationException("The prepared PDF no longer matches the invoice.");
        }
        invoice.PdfStorageKey = candidate.StorageKey;
        invoice.PdfFileName = candidate.FileName;
        invoice.PdfContentType = InvoicePdfStorage.ContentType;
        invoice.PdfSizeBytes = candidate.SizeBytes;
        invoice.PdfGeneratedAt = candidate.GeneratedAtUtc;
        invoice.PdfDocumentRevision = candidate.DocumentRevision;
        invoice.PdfHasDraftWatermark = candidate.HasDraftWatermark;
        invoice.DocumentState = InvoiceDocumentState.Current;
        invoice.DocumentFailureMessage = null;
    }

    public async Task<InvoicePdfContent?> OpenReadAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (!string.IsNullOrWhiteSpace(invoice.PdfStorageKey))
        {
            var blob = await blobStore.OpenReadAsync(invoice.PdfStorageKey, cancellationToken);
            return new InvoicePdfContent(
                blob.Content,
                string.IsNullOrWhiteSpace(blob.ContentType) ? InvoicePdfStorage.ContentType : blob.ContentType,
                blob.SizeBytes ?? invoice.PdfSizeBytes ?? 0);
        }

        return null;
    }

    public async Task<InvoicePdfReadResult> OpenCurrentReadAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (!InvoiceDocumentReadiness.IsCurrent(invoice))
        {
            return new InvoicePdfReadResult(null, InvoiceDocumentReadiness.UnavailableMessage(invoice));
        }

        try
        {
            var pdf = await OpenReadAsync(invoice, cancellationToken);
            return pdf is null
                ? new InvoicePdfReadResult(null, "Invoice PDF is missing.")
                : new InvoicePdfReadResult(pdf, null);
        }
        catch (FileNotFoundException)
        {
            return new InvoicePdfReadResult(null, "Invoice PDF is missing.");
        }
    }

    public Task DeleteAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return string.IsNullOrWhiteSpace(invoice.PdfStorageKey)
            ? Task.CompletedTask
            : blobStore.DeleteAsync(invoice.PdfStorageKey, cancellationToken);
    }
}
