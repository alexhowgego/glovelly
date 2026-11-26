namespace Glovelly.Api.Services;

public sealed class InvoiceAllowanceExceededException(EntitlementSnapshot snapshot)
    : Exception("Monthly first-issue invoice allowance reached.")
{
    public EntitlementSnapshot Snapshot { get; } = snapshot;
}
