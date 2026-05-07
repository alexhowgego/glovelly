using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public sealed record InvoiceDeliveryRequest(
    Invoice Invoice,
    Client Client,
    Guid? UserId,
    string? Message,
    string EmailSubject,
    string AttachmentFileName,
    InvoiceEmailSenderIdentity SenderIdentity,
    IReadOnlyList<InvoiceExpenseReceiptAttachment> ExpenseReceiptAttachments);

public sealed record InvoiceExpenseReceiptAttachment(
    string ExpenseDescription,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageKey);
