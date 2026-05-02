namespace Glovelly.Api.Services;

public sealed record InvoiceDeliveryResult(
    string Recipient,
    string? FileId = null,
    string? FileName = null,
    string? WebViewLink = null);
