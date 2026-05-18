using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class InvoicePdfService(IBlobStore blobStore, TimeProvider timeProvider) : IInvoicePdfService
{
    public async Task SaveGeneratedPdfAsync(
        Invoice invoice,
        Guid? userId,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(content);

        var key = InvoicePdfStorage.BuildStorageKey(invoice, userId);
        await blobStore.SaveAsync(
            new BlobWriteRequest(key, new MemoryStream(content, writable: false), InvoicePdfStorage.ContentType, content.Length),
            cancellationToken);

        invoice.PdfStorageKey = key;
        invoice.PdfFileName = $"{invoice.InvoiceNumber}.pdf";
        invoice.PdfContentType = InvoicePdfStorage.ContentType;
        invoice.PdfSizeBytes = content.Length;
        invoice.PdfGeneratedAt = timeProvider.GetUtcNow();
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

    public Task DeleteAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return string.IsNullOrWhiteSpace(invoice.PdfStorageKey)
            ? Task.CompletedTask
            : blobStore.DeleteAsync(invoice.PdfStorageKey, cancellationToken);
    }
}
