using System.Globalization;
using System.Text.RegularExpressions;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

internal static partial class InvoiceEmailSubjectBuilder
{
    private const string DefaultPattern = "Invoice {InvoiceNumber} from Glovelly";

    public static string Build(
        Invoice invoice,
        Client? client,
        string? defaultPattern = null,
        DateOnly? periodDate = null)
    {
        var pattern = client?.InvoiceEmailSubjectPattern ?? defaultPattern;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = DefaultPattern;
        }

        if (ContainsUnsupportedTokens(pattern))
        {
            pattern = DefaultPattern;
        }

        var resolved = ResolveTokens(pattern, invoice, client, periodDate)
            .ReplaceLineEndings(" ")
            .Trim();

        return string.IsNullOrWhiteSpace(resolved)
            ? ResolveTokens(DefaultPattern, invoice, client, periodDate)
            : resolved;
    }

    private static bool ContainsUnsupportedTokens(string pattern)
    {
        var matches = TokenRegex().Matches(pattern);
        foreach (Match match in matches)
        {
            if (!InvoicePdfFilenameBuilder.SupportedTokens.Contains(match.Value, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveTokens(
        string pattern,
        Invoice invoice,
        Client? client,
        DateOnly? periodDate)
    {
        var invoiceDate = invoice.InvoiceDate.ToDateTime(TimeOnly.MinValue);
        var effectivePeriodDate = (periodDate ?? invoice.InvoiceDate).ToDateTime(TimeOnly.MinValue);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{InvoiceNumber}"] = invoice.InvoiceNumber,
            ["{InvoiceId}"] = invoice.Id.ToString(),
            ["{ClientName}"] = client?.Name ?? string.Empty,
            ["{Month}"] = effectivePeriodDate.ToString("MM", CultureInfo.InvariantCulture),
            ["{MonthName}"] = effectivePeriodDate.ToString("MMMM", CultureInfo.InvariantCulture),
            ["{Year}"] = effectivePeriodDate.ToString("yyyy", CultureInfo.InvariantCulture),
            ["{InvoiceDate}"] = invoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        var resolved = pattern;
        foreach (var token in InvoicePdfFilenameBuilder.SupportedTokens)
        {
            resolved = resolved.Replace(token, replacements[token], StringComparison.Ordinal);
        }

        return resolved;
    }

    [GeneratedRegex(@"\{[^{}]+\}")]
    private static partial Regex TokenRegex();
}
