using Glovelly.Api.Models;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Services;

public sealed class InvoiceEmailDeliveryChannel(
    IEmailSender emailSender,
    IInvoiceReceiptArchiveService invoiceReceiptArchiveService,
    IInvoicePdfService invoicePdfService,
    IOptions<EmailSettings> emailSettingsAccessor) : IInvoiceDeliveryChannel
{
    public InvoiceDeliveryChannel Channel => InvoiceDeliveryChannel.Email;

    public async Task<InvoiceDeliveryResult> DeliverAsync(
        InvoiceDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoice = request.Invoice;
        var client = request.Client;
        if (string.IsNullOrWhiteSpace(client.Email))
        {
            throw new InvalidOperationException("Invoice recipient email is missing.");
        }

        var configuredFrom = EmailSenderSupport.ResolveConfiguredFromAddress(
            emailSettingsAccessor.Value,
            EmailUseCase.Invoices);
        var attachments = await BuildAttachmentsAsync(request, cancellationToken);

        var renderedEmail = InvoiceEmailTemplateRenderer.Render(
            invoice,
            client,
            request.EmailBodyTemplate,
            request.BusinessName,
            request.Message,
            request.ExpenseReceiptAttachments.Count > 0);

        await emailSender.SendAsync(
            new EmailMessage(
                To: [new EmailAddress(client.Email.Trim(), client.Name.Trim())],
                Subject: request.EmailSubject,
                PlainTextBody: renderedEmail.PlainTextBody,
                From: new EmailAddress(
                    configuredFrom.Address,
                    request.SenderIdentity.FromDisplayName),
                ReplyTo: string.IsNullOrWhiteSpace(request.SenderIdentity.ReplyToEmail)
                    ? null
                    : new EmailAddress(
                        request.SenderIdentity.ReplyToEmail!,
                        request.SenderIdentity.ReplyToDisplayName),
                HtmlBody: renderedEmail.HtmlBody,
                Attachments: attachments),
            cancellationToken);

        return new InvoiceDeliveryResult(client.Email.Trim());
    }

    private async Task<IReadOnlyList<EmailAttachment>> BuildAttachmentsAsync(
        InvoiceDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var invoicePdfResult = await invoicePdfService.OpenCurrentReadAsync(request.Invoice, cancellationToken);
        var invoicePdf = invoicePdfResult.Pdf
            ?? throw new InvalidOperationException(invoicePdfResult.UnavailableMessage);
        await using var invoicePdfContent = invoicePdf.Content;
        using var invoicePdfMemory = new MemoryStream();
        await invoicePdf.Content.CopyToAsync(invoicePdfMemory, cancellationToken);

        var attachments = new List<EmailAttachment>
        {
            new(
                request.AttachmentFileName,
                invoicePdf.ContentType,
                invoicePdfMemory.ToArray())
        };

        if (request.ExpenseReceiptAttachments.Count > 0)
        {
            attachments.Add(await invoiceReceiptArchiveService.CreateAsync(request, cancellationToken));
        }

        var maxTotalAttachmentBytes = emailSettingsAccessor.Value.MaxTotalAttachmentBytes;
        var totalAttachmentBytes = attachments.Sum(attachment => (long)attachment.Content.Length);
        if (maxTotalAttachmentBytes > 0 && totalAttachmentBytes > maxTotalAttachmentBytes)
        {
            throw new InvoiceEmailAttachmentLimitExceededException(
                totalAttachmentBytes,
                maxTotalAttachmentBytes);
        }

        return attachments;
    }

}

public sealed class InvoiceEmailAttachmentLimitExceededException(
    long totalAttachmentBytes,
    long maxTotalAttachmentBytes)
    : InvalidOperationException(
        $"Invoice email attachments total {totalAttachmentBytes} bytes, exceeding the {maxTotalAttachmentBytes} byte limit.")
{
    public long TotalAttachmentBytes { get; } = totalAttachmentBytes;
    public long MaxTotalAttachmentBytes { get; } = maxTotalAttachmentBytes;
}
