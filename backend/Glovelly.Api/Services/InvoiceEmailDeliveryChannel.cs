using Glovelly.Api.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace Glovelly.Api.Services;

public sealed class InvoiceEmailDeliveryChannel(
    IEmailSender emailSender,
    IOptions<EmailSettings> emailSettingsAccessor) : IInvoiceDeliveryChannel
{
    public InvoiceDeliveryChannel Channel => InvoiceDeliveryChannel.Email;

    public async Task DeliverAsync(
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

        if (invoice.PdfBlob is null || invoice.PdfBlob.Length == 0)
        {
            throw new InvalidOperationException("Invoice PDF is missing.");
        }

        var configuredFrom = EmailSenderSupport.TryResolveConfiguredFromAddress(emailSettingsAccessor.Value)
            ?? new EmailAddress("invoices@glovelly.local");

        await emailSender.SendAsync(
            new EmailMessage(
                To: [new EmailAddress(client.Email.Trim(), client.Name.Trim())],
                Subject: $"Invoice {invoice.InvoiceNumber} from Glovelly",
                PlainTextBody: BuildPlainTextBody(invoice, request.Message),
                From: new EmailAddress(
                    configuredFrom.Address,
                    request.SenderIdentity.FromDisplayName),
                ReplyTo: string.IsNullOrWhiteSpace(request.SenderIdentity.ReplyToEmail)
                    ? null
                    : new EmailAddress(
                        request.SenderIdentity.ReplyToEmail!,
                        request.SenderIdentity.ReplyToDisplayName),
                Attachments:
                [
                    new EmailAttachment(
                        request.AttachmentFileName,
                        "application/pdf",
                        invoice.PdfBlob)
                ]),
            cancellationToken);
    }

    private static string BuildPlainTextBody(Invoice invoice, string? message)
    {
        var builder = new StringBuilder()
            .AppendLine($"Please find invoice {invoice.InvoiceNumber} attached.")
            .AppendLine()
            .AppendLine("Many thanks,")
            .AppendLine("Glovelly");

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder
                .AppendLine()
                .AppendLine("Message:")
                .AppendLine(message.Trim());
        }

        return builder.ToString();
    }
}
