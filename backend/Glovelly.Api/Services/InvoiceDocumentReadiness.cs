using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public static class InvoiceDocumentReadiness
{
    public static bool HasCompatibleWatermark(Invoice invoice) => invoice.HasCompatiblePdfWatermark;

    public static bool IsCurrent(Invoice invoice) => invoice.IsPdfCurrent;

    public static string UnavailableMessage(Invoice invoice) => invoice.DocumentState switch
    {
        InvoiceDocumentState.Regenerating => "Invoice PDF is regenerating. Try again when it is complete.",
        InvoiceDocumentState.Failed => invoice.DocumentFailureMessage ?? "Invoice PDF regeneration failed. Try regenerating it again.",
        InvoiceDocumentState.Missing => "Invoice PDF is missing.",
        _ when !HasCompatibleWatermark(invoice) => "Regenerate this invoice PDF to match its watermark status.",
        _ => "Invoice PDF is unavailable.",
    };
}
