using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoiceDeliveryService
{
    Task<InvoiceDeliveryResult> DeliverAsync(
        InvoiceDeliveryChannel channel,
        Invoice invoice,
        Client client,
        Guid? userId,
        string? message,
        string emailSubject,
        string attachmentFileName,
        InvoiceEmailSenderIdentity senderIdentity,
        CancellationToken cancellationToken = default,
        IReadOnlyList<InvoiceExpenseReceiptAttachment>? expenseReceiptAttachments = null);
}
