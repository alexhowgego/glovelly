using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Glovelly.Api.Models;

namespace Glovelly.Api.Services;

public static partial class InvoiceEmailTemplateRenderer
{
    public const int MaxBodyTemplateLength = 4000;

    private const string DefaultBodyTemplate = """
        Hi {{CustomerName}},

        Please find invoice {{InvoiceNumber}} attached.

        Let me know if you have any questions.

        Many thanks,
        {{BusinessName}}
        """;

    private static readonly string[] SupportedTokens =
    [
        "CustomerName",
        "InvoiceNumber",
        "InvoiceDate",
        "DueDate",
        "BusinessName",
        "AmountDue",
    ];

    public static InvoiceEmailRenderResult Render(
        Invoice invoice,
        Client client,
        string? bodyTemplate,
        string? businessName,
        string? additionalMessage = null,
        bool includeReceipts = false)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(client);

        var effectiveTemplate = string.IsNullOrWhiteSpace(bodyTemplate)
            ? DefaultBodyTemplate
            : bodyTemplate.Trim();
        var effectiveBusinessName = string.IsNullOrWhiteSpace(businessName)
            ? "Glovelly"
            : businessName.Trim();
        var resolvedBody = ResolveTokens(effectiveTemplate, invoice, client, effectiveBusinessName).Trim();

        var plainTextBody = BuildPlainTextBody(invoice, resolvedBody, additionalMessage, includeReceipts);
        var htmlBody = BuildHtmlBody(invoice, resolvedBody, additionalMessage, includeReceipts);

        return new InvoiceEmailRenderResult(plainTextBody, htmlBody);
    }

    public static bool TryValidateBodyTemplate(
        string? bodyTemplate,
        out Dictionary<string, string[]> errors,
        string fieldName = "invoiceEmailBodyTemplate")
    {
        errors = new Dictionary<string, string[]>();

        if (bodyTemplate is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(bodyTemplate))
        {
            errors[fieldName] = ["Invoice email body template cannot be empty or whitespace."];
            return true;
        }

        if (bodyTemplate.Length > MaxBodyTemplateLength)
        {
            errors[fieldName] = [$"Invoice email body template must be {MaxBodyTemplateLength} characters or fewer."];
            return true;
        }

        var unsupportedTokens = FindUnsupportedTokens(bodyTemplate);
        if (unsupportedTokens.Count > 0)
        {
            errors[fieldName] = [$"Unsupported invoice email token: {unsupportedTokens[0]}."];
            return true;
        }

        return false;
    }

    public static IReadOnlyList<string> GetSupportedTokenPlaceholders()
    {
        return SupportedTokens.Select(token => "{{" + token + "}}").ToArray();
    }

    private static string BuildPlainTextBody(
        Invoice invoice,
        string resolvedBody,
        string? additionalMessage,
        bool includeReceipts)
    {
        var builder = new StringBuilder(resolvedBody)
            .AppendLine()
            .AppendLine()
            .AppendLine($"Invoice {invoice.InvoiceNumber} is attached as a PDF.");

        if (includeReceipts)
        {
            builder.AppendLine("Expense receipts are attached in a separate ZIP file.");
        }

        if (!string.IsNullOrWhiteSpace(additionalMessage))
        {
            builder
                .AppendLine()
                .AppendLine("Additional message:")
                .AppendLine(additionalMessage.Trim());
        }

        return builder.ToString();
    }

    private static string BuildHtmlBody(
        Invoice invoice,
        string resolvedBody,
        string? additionalMessage,
        bool includeReceipts)
    {
        var bodyHtml = PlainTextToHtml(resolvedBody);
        var additionalMessageHtml = string.IsNullOrWhiteSpace(additionalMessage)
            ? string.Empty
            : $"""
                <div class="additional-message">
                  <p class="section-label">Additional message</p>
                  {PlainTextToHtml(additionalMessage.Trim())}
                </div>
                """;
        var receiptNote = includeReceipts
            ? "<p>Expense receipts are attached in a separate ZIP file.</p>"
            : string.Empty;

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                body { margin: 0; padding: 0; background: #f6f3ee; color: #202020; font-family: Arial, sans-serif; }
                .wrap { max-width: 640px; margin: 0 auto; padding: 32px 20px; }
                .card { background: #ffffff; border: 1px solid #e5ded2; border-radius: 16px; padding: 28px; }
                .content p { margin: 0 0 16px; line-height: 1.55; }
                .attachment-note, .additional-message { margin-top: 24px; padding: 16px; background: #fbf8f2; border-radius: 12px; }
                .section-label { margin: 0 0 8px; color: #6c5f50; font-size: 12px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
                .footer { margin-top: 18px; color: #746b61; font-size: 12px; text-align: center; }
                .footer a { color: #746b61; font-weight: 700; }
              </style>
            </head>
            <body>
              <div class="wrap">
                <div class="card">
                  <div class="content">
                    {{bodyHtml}}
                  </div>
                  <div class="attachment-note">
                    <p class="section-label">Attachment</p>
                    <p>Invoice {{WebUtility.HtmlEncode(invoice.InvoiceNumber)}} is attached as a PDF.</p>
                    {{receiptNote}}
                  </div>
                  {{additionalMessageHtml}}
                </div>
                <div class="footer">Sent with <a href="https://glovelly.net">Glovelly</a></div>
              </div>
            </body>
            </html>
            """;
    }

    private static string PlainTextToHtml(string text)
    {
        var normalized = text.ReplaceLineEndings("\n").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var paragraphs = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(
            Environment.NewLine,
            paragraphs.Select(paragraph =>
                $"<p>{WebUtility.HtmlEncode(paragraph).Replace("\n", "<br>", StringComparison.Ordinal)}</p>"));
    }

    private static string ResolveTokens(
        string template,
        Invoice invoice,
        Client client,
        string businessName)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerName"] = client.Name,
            ["InvoiceNumber"] = invoice.InvoiceNumber,
            ["InvoiceDate"] = invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["DueDate"] = invoice.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["BusinessName"] = businessName,
            ["AmountDue"] = invoice.Total.ToString("C", CultureInfo.GetCultureInfo("en-GB")),
        };

        return TokenRegex().Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            return replacements.TryGetValue(token, out var replacement)
                ? replacement
                : match.Value;
        });
    }

    private static IReadOnlyList<string> FindUnsupportedTokens(string template)
    {
        var supported = SupportedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return TokenRegex()
            .Matches(template)
            .Select(match => match.Groups[1].Value)
            .Where(token => !supported.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(token => "{{" + token + "}}")
            .ToArray();
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9]*)\s*\}\}")]
    private static partial Regex TokenRegex();
}

public sealed record InvoiceEmailRenderResult(string PlainTextBody, string HtmlBody);
