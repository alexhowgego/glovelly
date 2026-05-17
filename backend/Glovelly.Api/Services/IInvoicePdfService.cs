using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoicePdfService
{
    Task SaveGeneratedPdfAsync(
        Invoice invoice,
        Guid? userId,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<InvoicePdfContent?> OpenReadAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Invoice invoice, CancellationToken cancellationToken = default);
}

public sealed record InvoicePdfContent(
    Stream Content,
    string ContentType,
    long SizeBytes);
