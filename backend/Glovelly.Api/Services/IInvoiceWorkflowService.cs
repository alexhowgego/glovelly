using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoiceWorkflowService
{
    Task<Invoice> GenerateInvoiceForGigAsync(Gig gig, Client client, Guid? userId, CancellationToken cancellationToken = default);
    Task SyncGeneratedInvoiceLinesForGigAsync(Gig gig, Guid? userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveSystemGeneratedInvoiceLinesForGigAsync(Guid gigId, CancellationToken cancellationToken = default);
    Task ReissueInvoiceAsync(Invoice invoice, Client client, Guid? userId, CancellationToken cancellationToken = default);
    Task<InvoiceLine> CreateManualAdjustmentAsync(Invoice invoice, decimal amount, string reason, Guid? userId, CancellationToken cancellationToken = default);
}
