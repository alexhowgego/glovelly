using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IBusinessLifecycleSignal
{
    Task TrackGigAsync(Gig gig, CancellationToken cancellationToken = default);

    Task TrackInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
