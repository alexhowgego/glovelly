using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoiceLineGenerationService
{
    Task<List<InvoiceLine>> BuildGeneratedInvoiceLinesForGigAsync(
        Gig gig,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveSystemGeneratedInvoiceLinesForGigAsync(
        Guid gigId,
        CancellationToken cancellationToken = default);

    Task<InvoiceLine> CreateManualAdjustmentAsync(
        Invoice invoice,
        decimal amount,
        string reason,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
