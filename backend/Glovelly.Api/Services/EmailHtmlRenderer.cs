using System.Net;

namespace Glovelly.Api.Services;

internal static class EmailHtmlRenderer
{
    public static string RenderDocument(
        string title,
        string intro,
        string contentHtml,
        string eyebrow = "Glovelly")
    {
        var encodedEyebrow = Encode(eyebrow);
        var encodedTitle = Encode(title);
        var encodedIntro = Encode(intro);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                body { margin: 0; padding: 0; background: #f5efe7; color: #21313c; font-family: 'Avenir Next', 'Segoe UI', Arial, sans-serif; }
                .wrap { max-width: 640px; margin: 0 auto; padding: 32px 20px; }
                .card { background: #fffdf9; border: 1px solid #e5d8ca; border-radius: 24px; box-shadow: 0 18px 45px rgba(39, 31, 24, 0.08); }
                .hero { padding: 24px 28px; background: #17324d; color: #ffffff; border-radius: 24px 24px 0 0; }
                .eyebrow { font-size: 12px; letter-spacing: 0.18em; text-transform: uppercase; opacity: 0.82; }
                .hero h1 { margin: 12px 0 0; font-family: Georgia, serif; font-size: 28px; line-height: 1.05; }
                .hero p { margin: 12px 0 0; color: rgba(255, 255, 255, 0.88); font-size: 15px; line-height: 1.6; }
                .message-main { padding: 28px; }
                .message-copy p { margin: 0 0 16px; line-height: 1.6; }
                .attachment-note, .additional-message, .info-note { margin-top: 24px; padding: 16px; background: #fbf8f2; border-radius: 12px; }
                .section-label { margin: 0 0 8px; color: #6c5f50; font-size: 12px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
                .details { width: 100%; border-collapse: collapse; margin-top: 8px; }
                .details th { width: 160px; padding: 10px 12px 10px 0; border-bottom: 1px solid #eee6dc; color: #6c5f50; font-size: 12px; letter-spacing: 0.04em; text-align: left; text-transform: uppercase; vertical-align: top; }
                .details td { padding: 10px 0; border-bottom: 1px solid #eee6dc; vertical-align: top; }
                .button { display: inline-block; padding: 12px 18px; border-radius: 999px; background: #17324d; color: #ffffff !important; font-weight: 700; text-decoration: none; }
                .footer { margin-top: 18px; color: #746b61; font-size: 12px; text-align: center; }
                .footer a { color: #746b61; font-weight: 700; }
              </style>
            </head>
            <body>
              <div class="wrap">
                <div class="card">
                  <div class="hero">
                    <div class="eyebrow">{{encodedEyebrow}}</div>
                    <h1>{{encodedTitle}}</h1>
                    <p>{{encodedIntro}}</p>
                  </div>
                  <div class="message-main">
                    {{contentHtml}}
                  </div>
                </div>
                <div class="footer">Sent with <a href="https://glovelly.net">Glovelly</a></div>
              </div>
            </body>
            </html>
            """;
    }

    public static string PlainTextToHtml(string text)
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
                $"<p>{Encode(paragraph).Replace("\n", "<br>", StringComparison.Ordinal)}</p>"));
    }

    public static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
