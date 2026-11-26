namespace Glovelly.Api.Models;

public sealed class UsageLedgerEntry
{
    public const string InvoiceFirstIssued = "InvoiceFirstIssued";

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Metric { get; set; } = InvoiceFirstIssued;
    // Intentionally not an invoice relationship: deletion must not refund usage.
    public Guid SourceInvoiceId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Source { get; set; } = string.Empty;
}
