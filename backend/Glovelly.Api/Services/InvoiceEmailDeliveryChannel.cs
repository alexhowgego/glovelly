using Glovelly.Api.Models;
using System.Text;

namespace Glovelly.Api.Services;

public sealed class InvoiceEmailDeliveryChannel(IEmailSender emailSender) : IInvoiceDeliveryChannel
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

        await emailSender.SendAsync(
            new EmailMessage(
                To: [new EmailAddress(client.Email.Trim(), client.Name.Trim())],
                Subject: $"Invoice {invoice.InvoiceNumber} from Glovelly",
                PlainTextBody: BuildPlainTextBody(invoice, request.Message),
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
