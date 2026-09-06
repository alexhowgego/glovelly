using System.IO.Compression;

namespace Glovelly.Api.Services;

public interface IInvoiceReceiptArchiveService
{
    Task<EmailAttachment> CreateAsync(
        InvoiceDeliveryRequest request,
        CancellationToken cancellationToken = default);

    string GetFileName(string invoiceNumber);
}

public sealed class InvoiceReceiptArchiveService(
    IExpenseAttachmentStore expenseAttachmentStore) : IInvoiceReceiptArchiveService
{
    public async Task<EmailAttachment> CreateAsync(
        InvoiceDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var receipt in request.ExpenseReceiptAttachments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.CreateEntry(
                    BuildUniqueZipEntryName(receipt, usedEntryNames),
                    CompressionLevel.SmallestSize);
                await using var entryStream = entry.Open();
                var content = await expenseAttachmentStore.OpenReadAsync(receipt.StorageKey, cancellationToken);
                await using (content.Content)
                {
                    await content.Content.CopyToAsync(entryStream, cancellationToken);
                }
            }
        }

        return new EmailAttachment(GetFileName(request.Invoice.InvoiceNumber), "application/zip", zipStream.ToArray());
    }

    public string GetFileName(string invoiceNumber) =>
        $"Invoice-{SanitizeFileNamePart(invoiceNumber, "Invoice")}-Receipts.zip";

    private static string BuildUniqueZipEntryName(
        InvoiceExpenseReceiptAttachment receipt,
        HashSet<string> usedEntryNames)
    {
        var baseName = TrimFileNamePart(
            $"{SanitizeFileNamePart(receipt.ExpenseDescription, "Expense")}-{SanitizeFileNamePart(receipt.FileName, "receipt")}",
            180);
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
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(invalidCharacters.Contains(character) || char.IsControl(character)
                ? '-'
                : character);
        }

        var sanitized = builder.ToString().Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string TrimFileNamePart(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].Trim(' ', '.', '-');
}
