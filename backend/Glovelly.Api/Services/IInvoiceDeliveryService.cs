using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoiceDeliveryService
{
    Task DeliverAsync(
        InvoiceDeliveryChannel channel,
        Invoice invoice,
        Client client,
        Guid? userId,
        string? message,
        string attachmentFileName,
        CancellationToken cancellationToken = default);
}
