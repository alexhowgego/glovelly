namespace Glovelly.Api.Services;

public interface IInvoiceDeliveryChannel
{
    InvoiceDeliveryChannel Channel { get; }

    Task<InvoiceDeliveryResult> DeliverAsync(
        InvoiceDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
