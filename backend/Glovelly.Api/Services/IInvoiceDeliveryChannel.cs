namespace Glovelly.Api.Services;

public interface IInvoiceDeliveryChannel
{
    InvoiceDeliveryChannel Channel { get; }

    Task DeliverAsync(InvoiceDeliveryRequest request, CancellationToken cancellationToken = default);
}
