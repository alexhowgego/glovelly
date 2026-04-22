using System.Globalization;
using System.Text.RegularExpressions;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

internal static partial class InvoicePdfFilenameBuilder
{
    public static readonly string[] SupportedTokens =
    [
        "{InvoiceNumber}",
        "{InvoiceId}",
        "{ClientName}",
        "{Month}",
        "{MonthName}",
        "{Year}",
        "{InvoiceDate}"
    ];

    public const string DefaultInvoiceNumber = "INV-2026-001";

    public static string Build(Invoice invoice, Client? client, string? defaultPattern = null)
    {
        var pattern = client?.InvoiceFilenamePattern ?? defaultPattern;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return AppendPdfExtension(Sanitize(invoice.InvoiceNumber));
        }

        if (ContainsUnsupportedTokens(pattern))
        {
            return AppendPdfExtension(Sanitize(invoice.InvoiceNumber));
        }

        var resolved = ResolveTokens(pattern, invoice, client);
        var sanitized = Sanitize(resolved);

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return AppendPdfExtension(Sanitize(invoice.InvoiceNumber));
        }

        return AppendPdfExtension(sanitized);
    }

    public static string BuildPreview(
        string? pattern,
        string? clientName,
        DateOnly? invoiceDate = null,
        string? defaultPattern = null)
    {
        var previewInvoice = new Invoice
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            InvoiceNumber = DefaultInvoiceNumber,
            InvoiceDate = invoiceDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
        };

        var previewClient = new Client
        {
            Name = clientName ?? "Client Name",
            InvoiceFilenamePattern = pattern,
        };

        return Build(previewInvoice, previewClient, defaultPattern);
    }

    private static bool ContainsUnsupportedTokens(string pattern)
    {
        var matches = TokenRegex().Matches(pattern);
        foreach (Match match in matches)
        {
            if (!SupportedTokens.Contains(match.Value, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveTokens(string pattern, Invoice invoice, Client? client)
    {
        var invoiceDate = invoice.InvoiceDate.ToDateTime(TimeOnly.MinValue);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{InvoiceNumber}"] = invoice.InvoiceNumber,
            ["{InvoiceId}"] = invoice.Id.ToString(),
            ["{ClientName}"] = client?.Name ?? string.Empty,
            ["{Month}"] = invoiceDate.ToString("MM", CultureInfo.InvariantCulture),
            ["{MonthName}"] = invoiceDate.ToString("MMMM", CultureInfo.InvariantCulture),
            ["{Year}"] = invoiceDate.ToString("yyyy", CultureInfo.InvariantCulture),
            ["{InvoiceDate}"] = invoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        var resolved = pattern;
        foreach (var token in SupportedTokens)
        {
            resolved = resolved.Replace(token, replacements[token], StringComparison.Ordinal);
        }

        return resolved;
    }

    private static string AppendPdfExtension(string filename)
    {
        if (filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return filename;
        }

        return $"{filename}.pdf";
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = InvalidFilenameCharactersRegex().Replace(value, "-");
        sanitized = CollapseWhitespaceRegex().Replace(sanitized, " ").Trim(' ', '.');

        return sanitized;
    }

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex InvalidFilenameCharactersRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();

    [GeneratedRegex(@"\{[^{}]+\}")]
    private static partial Regex TokenRegex();
}
