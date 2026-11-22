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

        invoice.PdfStorageKey = key;
        invoice.PdfFileName = $"{invoice.InvoiceNumber}.pdf";
        invoice.PdfContentType = InvoicePdfStorage.ContentType;
        invoice.PdfSizeBytes = content.Length;
        invoice.PdfGeneratedAt = timeProvider.GetUtcNow();
        invoice.PdfDocumentRevision = invoice.DocumentRevision;
        invoice.PdfHasDraftWatermark = hasDraftWatermark;
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
