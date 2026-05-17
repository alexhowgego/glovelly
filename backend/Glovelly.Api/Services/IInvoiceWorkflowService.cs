using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public interface IInvoiceWorkflowService
{
    Task<Invoice> GenerateInvoiceForGigAsync(Gig gig, Client client, Guid? userId, CancellationToken cancellationToken = default);
    Task<GenerateInvoiceFromGigSelectionResult> GenerateInvoiceFromGigSelectionAsync(
        IReadOnlyCollection<Guid> gigIds,
        Guid? userId,
        CancellationToken cancellationToken = default);
    Task SyncGeneratedInvoiceLinesForGigAsync(Gig gig, Guid? userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveSystemGeneratedInvoiceLinesForGigAsync(Guid gigId, CancellationToken cancellationToken = default);
    Task IssueInvoiceAsync(Invoice invoice, Client client, Guid? userId, CancellationToken cancellationToken = default);
    Task RedraftInvoiceAsync(Invoice invoice, Client client, Guid? userId, CancellationToken cancellationToken = default);
    Task ReissueInvoiceAsync(Invoice invoice, Client client, Guid? userId, CancellationToken cancellationToken = default);
    Task<InvoiceLine> CreateManualAdjustmentAsync(Invoice invoice, decimal amount, string reason, Guid? userId, CancellationToken cancellationToken = default);
}

public sealed record GenerateInvoiceFromGigSelectionResult(
    GenerateInvoiceFromGigSelectionStatus Status,
    Invoice? Invoice = null,
    Dictionary<string, string[]>? ValidationErrors = null,
    string? ConflictMessage = null);

public enum GenerateInvoiceFromGigSelectionStatus
{
    Created,
    ValidationFailed,
    Conflict,
}
