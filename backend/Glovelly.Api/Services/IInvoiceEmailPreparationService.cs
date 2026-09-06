using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoiceEmailPreparationService
{
    Task<InvoiceEmailPreparation> PrepareAsync(
        Invoice invoice,
        Guid? userId,
        string? message,
        bool includeReceipts,
        CancellationToken cancellationToken = default);
}

public sealed record InvoiceEmailPreparation(
    InvoiceDeliveryRequest DeliveryRequest,
    InvoiceEmailRenderResult RenderedEmail,
    long PdfSizeBytes);

public sealed class InvoiceEmailPreparationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
