using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoiceProfileDefaultsService
{
    Task<int> ResolvePaymentWindowDaysAsync(Guid? userId, CancellationToken cancellationToken = default);
    Task<SellerProfile?> ResolveSellerProfileAsync(Guid? userId, CancellationToken cancellationToken = default);
}
