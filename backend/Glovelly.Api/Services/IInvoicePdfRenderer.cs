using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoicePdfRenderer
{
    byte[] RenderInvoicePdf(
        Invoice invoice,
        Client client,
        Gig? gig,
        IReadOnlyCollection<InvoiceLine> lines,
        SellerProfile? sellerProfile);
}
