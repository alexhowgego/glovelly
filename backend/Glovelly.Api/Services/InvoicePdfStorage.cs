using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public static class InvoicePdfStorage
{
    public const string ContentType = "application/pdf";

    public static string BuildStorageKey(Invoice invoice, Guid? userId)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var ownerSegment = userId.HasValue
            ? userId.Value.ToString("N")
            : invoice.CreatedByUserId?.ToString("N") ?? "unassigned";

        return $"users/{ownerSegment}/invoices/{invoice.Id:D}/invoice.pdf";
    }
}
