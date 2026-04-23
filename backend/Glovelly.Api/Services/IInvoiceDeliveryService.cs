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
        InvoiceEmailSenderIdentity senderIdentity,
        CancellationToken cancellationToken = default);
}
