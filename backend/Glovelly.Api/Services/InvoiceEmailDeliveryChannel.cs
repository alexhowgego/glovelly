using Glovelly.Api.Models;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text;

namespace Glovelly.Api.Services;

public sealed class InvoiceEmailDeliveryChannel(
    IEmailSender emailSender,
    IExpenseAttachmentStore expenseAttachmentStore,
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
        var invoicePdf = await invoicePdfService.OpenReadAsync(request.Invoice, cancellationToken)
            ?? throw new InvalidOperationException("Invoice PDF is missing.");
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
            attachments.Add(await BuildReceiptZipAttachmentAsync(request, cancellationToken));
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

    private async Task<EmailAttachment> BuildReceiptZipAttachmentAsync(
        InvoiceDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var receipt in request.ExpenseReceiptAttachments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entryName = BuildUniqueZipEntryName(receipt, usedEntryNames);
                var entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
                await using var entryStream = entry.Open();
                var content = await expenseAttachmentStore.OpenReadAsync(receipt.StorageKey, cancellationToken);
                await using (content.Content)
                {
                    await content.Content.CopyToAsync(entryStream, cancellationToken);
                }
            }
        }

        return new EmailAttachment(
            $"Invoice-{SanitizeFileNamePart(request.Invoice.InvoiceNumber, "Invoice")}-Receipts.zip",
            "application/zip",
            zipStream.ToArray());
    }

    private static string BuildUniqueZipEntryName(
        InvoiceExpenseReceiptAttachment receipt,
        HashSet<string> usedEntryNames)
    {
        var expenseDescription = SanitizeFileNamePart(receipt.ExpenseDescription, "Expense");
        var originalFileName = SanitizeFileNamePart(receipt.FileName, "receipt");
        var baseName = TrimFileNamePart($"{expenseDescription}-{originalFileName}", 180);
        var candidate = baseName;
        var suffix = 2;

        while (!usedEntryNames.Add(candidate))
        {
            var extension = Path.GetExtension(baseName);
            var nameWithoutExtension = string.IsNullOrWhiteSpace(extension)
                ? baseName
                : baseName[..^extension.Length];
            candidate = $"{TrimFileNamePart(nameWithoutExtension, 170)}-{suffix++}{extension}";
        }

        return candidate;
    }

    private static string SanitizeFileNamePart(string value, string fallback)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            trimmed = fallback;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(invalidCharacters.Contains(character) || char.IsControl(character)
                ? '-'
                : character);
        }

        var sanitized = builder.ToString().Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(sanitized)
            ? fallback
            : sanitized;
    }

    private static string TrimFileNamePart(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength].Trim(' ', '.', '-');
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
