using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public static class InvoiceDeliveryPolicy
{
    public static string? GetError(Invoice invoice) => invoice.Status switch
    {
        InvoiceStatus.Issued or InvoiceStatus.Paid or InvoiceStatus.Overdue => null,
        InvoiceStatus.Cancelled => "Restore this cancelled invoice to Draft and issue it before delivery.",
        _ => "Issue this invoice before delivery.",
    };
}
