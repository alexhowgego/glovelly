using System.Globalization;
using System.Text;

namespace Glovelly.Api.Services;

public sealed class ExpenseStatementPdfRenderer : IExpenseStatementPdfRenderer
{
    public byte[] Render(ExpenseStatementProjection statement, bool includeReceiptAppendix)
    {
        var document = new SimplePdfDocument();
        var page = document.AddPage();
        var cursorY = 790d;

        DrawLine(page, ref cursorY, "Expense Statement", "F2", 24);
        DrawLine(page, ref cursorY, statement.ClientName, "F2", 14);
        DrawLine(page, ref cursorY, $"Statement date: {statement.StatementDate:yyyy-MM-dd}", "F1", 10);
        cursorY -= 18;

        foreach (var gig in statement.Gigs)
        {
            EnsureSpace(document, ref page, ref cursorY, 88);
            DrawLine(page, ref cursorY, $"{gig.Date:yyyy-MM-dd} - {gig.Title}", "F2", 13);
            DrawLine(page, ref cursorY, gig.Venue, "F1", 10);

            foreach (var expense in gig.Expenses)
            {
                EnsureSpace(document, ref page, ref cursorY, 42);
                DrawLine(
                    page,
                    ref cursorY,
                    $"{expense.Description}    {FormatCurrency(expense.Amount)}",
                    "F1",
                    10);

                if (expense.Attachments.Count > 0)
                {
                    DrawLine(
                        page,
                        ref cursorY,
                        $"{expense.Attachments.Count} receipt attachment(s)",
                        "F1",
                        9);
                }
            }

            DrawLine(page, ref cursorY, $"Gig total: {FormatCurrency(gig.Total)}", "F2", 10);
            cursorY -= 12;
        }

        EnsureSpace(document, ref page, ref cursorY, 56);
        DrawLine(page, ref cursorY, $"Expenses: {statement.ExpenseCount}", "F2", 11);
        DrawLine(page, ref cursorY, $"Receipt attachments: {statement.ReceiptAttachmentCount}", "F2", 11);
        DrawLine(page, ref cursorY, $"Total: {FormatCurrency(statement.Total)}", "F2", 16);

        if (includeReceiptAppendix && statement.ReceiptAttachmentCount > 0)
        {
            page = document.AddPage();
            cursorY = 790d;
            DrawLine(page, ref cursorY, "Receipt Appendix", "F2", 20);
            cursorY -= 12;

            foreach (var gig in statement.Gigs)
            {
                foreach (var expense in gig.Expenses.Where(expense => expense.Attachments.Count > 0))
                {
                    EnsureSpace(document, ref page, ref cursorY, 56 + (expense.Attachments.Count * 14));
                    DrawLine(page, ref cursorY, $"{gig.Title} - {expense.Description}", "F2", 11);

                    foreach (var attachment in expense.Attachments)
                    {
                        DrawLine(
                            page,
                            ref cursorY,
                            $"{attachment.FileName} ({attachment.ContentType}, {FormatBytes(attachment.SizeBytes)})",
                            "F1",
                            9);
                    }

                    cursorY -= 8;
                }
            }
        }

        return document.Build();
    }

    private static void EnsureSpace(SimplePdfDocument document, ref SimplePdfPage page, ref double cursorY, double requiredHeight)
    {
        if (cursorY - requiredHeight >= 52)
        {
            return;
        }

        page = document.AddPage();
        cursorY = 790;
        DrawLine(page, ref cursorY, "Expense Statement continued", "F2", 14);
        cursorY -= 10;
    }

    private static void DrawLine(SimplePdfPage page, ref double cursorY, string text, string fontName, double fontSize)
    {
        page.DrawText(48, cursorY, text, fontName, fontSize, fontName == "F2" ? 0.1 : 0.18);
        cursorY -= fontSize + 7;
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"GBP {amount:0.00}";
    }

    private static string FormatBytes(long byteCount)
    {
        const decimal oneMegabyte = 1024m * 1024m;
        return byteCount < oneMegabyte
            ? $"{byteCount} bytes"
            : $"{byteCount / oneMegabyte:0.##} MB";
    }

    private sealed class SimplePdfDocument
    {
        private readonly List<string> _pageContents = [];

        public SimplePdfPage AddPage()
        {
            _pageContents.Add(string.Empty);
            var pageIndex = _pageContents.Count - 1;
            return new SimplePdfPage(value => _pageContents[pageIndex] += value);
        }

        public byte[] Build()
        {
            var pageCount = _pageContents.Count;
            var objects = new List<string>
            {
                "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
                $"2 0 obj << /Type /Pages /Count {pageCount} /Kids [{string.Join(' ', Enumerable.Range(0, pageCount).Select(index => $"{5 + (index * 2)} 0 R"))}] >> endobj",
                "3 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj",
                "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> endobj"
            };

            for (var index = 0; index < pageCount; index++)
            {
                var pageObjectId = 5 + (index * 2);
                var contentObjectId = pageObjectId + 1;
                var content = _pageContents[index];
                objects.Add(
                    $"{pageObjectId} 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjectId} 0 R >> endobj");
                objects.Add(
                    $"{contentObjectId} 0 obj << /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream\nendobj");
            }

            var pdfBuilder = new StringBuilder();
            pdfBuilder.Append("%PDF-1.4\n");

            var offsets = new List<int> { 0 };
            foreach (var pdfObject in objects)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(pdfBuilder.ToString()));
                pdfBuilder.Append(pdfObject);
                pdfBuilder.Append('\n');
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(pdfBuilder.ToString());
            pdfBuilder.Append($"xref\n0 {objects.Count + 1}\n");
            pdfBuilder.Append("0000000000 65535 f \n");

            foreach (var offset in offsets.Skip(1))
            {
                pdfBuilder.Append($"{offset:D10} 00000 n \n");
            }

            pdfBuilder.Append("trailer\n");
            pdfBuilder.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            pdfBuilder.Append("startxref\n");
            pdfBuilder.Append($"{xrefOffset}\n");
            pdfBuilder.Append("%%EOF");

            return Encoding.ASCII.GetBytes(pdfBuilder.ToString());
        }
    }

    private sealed class SimplePdfPage(Action<string> append)
    {
        private readonly Action<string> _append = append;

        public void DrawText(double x, double y, string text, string fontName, double fontSize, double gray)
        {
            _append($"{Format(gray)} g BT /{fontName} {Format(fontSize)} Tf {Format(x)} {Format(y)} Td ({EscapePdfText(text)}) Tj ET\n");
        }

        private static string Format(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
