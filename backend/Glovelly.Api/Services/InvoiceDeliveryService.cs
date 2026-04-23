using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed class InvoiceDeliveryService(
    IEnumerable<IInvoiceDeliveryChannel> deliveryChannels,
    TimeProvider timeProvider) : IInvoiceDeliveryService
{
    public async Task DeliverAsync(
        InvoiceDeliveryChannel channel,
        Invoice invoice,
        Client client,
        Guid? userId,
        string? message,
        string attachmentFileName,
        InvoiceEmailSenderIdentity senderIdentity,
        CancellationToken cancellationToken = default)
    {
        var deliveryChannel = deliveryChannels.FirstOrDefault(value => value.Channel == channel)
            ?? throw new InvalidOperationException($"Invoice delivery channel {channel} is not registered.");

        await deliveryChannel.DeliverAsync(
            new InvoiceDeliveryRequest(invoice, client, userId, message, attachmentFileName, senderIdentity),
            cancellationToken);

        invoice.DeliveryCount += 1;
        invoice.LastDeliveryChannel = channel.ToString();
        invoice.LastDeliveryRecipient = client.Email.Trim();
        invoice.LastDeliveredUtc = timeProvider.GetUtcNow();
        invoice.LastDeliveredByUserId = userId;
    }
}
