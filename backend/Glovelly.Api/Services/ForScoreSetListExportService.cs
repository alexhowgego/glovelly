using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Glovelly.Api.Services;

public interface IForScoreSetListExportService
{
    byte[] BuildExport(string title, IReadOnlyList<ForScoreSetListExportItem> items);
    string BuildFileName(string title);
}

public sealed class ForScoreSetListExportService : IForScoreSetListExportService
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public byte[] BuildExport(string title, IReadOnlyList<ForScoreSetListExportItem> items)
    {
        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("forScore",
                new XAttribute("kind", "setlist"),
                new XAttribute("version", "1.0"),
                new XAttribute("title", title),
                items.Select(item => new XElement("score",
                    new XAttribute("title", item.Title),
                    new XAttribute("path", item.Path)))));

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings
        {
            Encoding = Utf8NoBom,
            Indent = true,
            OmitXmlDeclaration = false,
        }))
        {
            document.Save(writer);
        }

        return output.ToArray();
    }

    public string BuildFileName(string title)
    {
        var sanitized = SanitizeFileName(title);
        return string.IsNullOrWhiteSpace(sanitized) ? "setlist.4ss" : $"{sanitized}.4ss";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(invalidCharacters.Contains(character) ? '-' : character);
        }

        var sanitized = builder.ToString().Trim(' ', '.', '-');
        return sanitized.Length <= 120 ? sanitized : sanitized[..120].Trim(' ', '.', '-');
    }
}

public sealed record ForScoreSetListExportItem(string Title, string Path);
