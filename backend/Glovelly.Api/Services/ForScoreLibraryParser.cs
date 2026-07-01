using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Claunia.PropertyList;

namespace Glovelly.Api.Services;

public interface IForScoreLibraryParser
{
    Task<ForScoreLibraryParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}

public sealed partial class ForScoreLibraryParser : IForScoreLibraryParser
{
    private static readonly byte[] GzipMagic = [0x1f, 0x8b];

    public async Task<ForScoreLibraryParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var input = new MemoryStream();
        await stream.CopyToAsync(input, cancellationToken);
        var bytes = input.ToArray();
        var gzipOffset = FindGzipOffset(bytes);
        if (gzipOffset < 0)
        {
            throw new ForScoreLibraryParseException("The file does not contain a supported forScore backup payload.");
        }

        var backupVersion = TryReadBackupVersion(bytes.AsSpan(0, gzipOffset));
        byte[] payload;
        try
        {
            using var gzipInput = new MemoryStream(bytes, gzipOffset, bytes.Length - gzipOffset);
            using var gzip = new GZipStream(gzipInput, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            await gzip.CopyToAsync(decompressed, cancellationToken);
            payload = decompressed.ToArray();
        }
        catch (InvalidDataException exception)
        {
            throw new ForScoreLibraryParseException("The forScore backup payload could not be decompressed.", exception);
        }

        Dictionary<string, object?> plist;
        try
        {
            plist = ReadPropertyListDictionary(payload);
        }
        catch (PropertyListException exception)
        {
            throw new ForScoreLibraryParseException(exception.Message, exception);
        }

        return ExtractCharts(plist, backupVersion);
    }

    private static Dictionary<string, object?> ReadPropertyListDictionary(byte[] payload)
    {
        return PropertyListParser.Parse(payload).ToObject() is Dictionary<string, object> plist
            ? plist.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal)
            : throw new ForScoreLibraryParseException("The forScore backup payload is not a metadata dictionary.");
    }

    public static string NormalizeTitle(string value)
    {
        var normalized = value.Trim();
        if (normalized.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        normalized = normalized.Replace('&', ' ');
        normalized = NonWordRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim().ToUpperInvariant();
        return normalized;
    }

    private static ForScoreLibraryParseResult ExtractCharts(Dictionary<string, object?> plist, string? backupVersion)
    {
        var fieldsByPath = new SortedDictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var warnings = new List<string>();

        foreach (var (key, value) in plist)
        {
            if (key.StartsWith('&') || value is byte[])
            {
                continue;
            }

            var keyParts = key.Split('|');
            if (keyParts.Length != 2)
            {
                continue;
            }

            var filePath = keyParts[0].Trim();
            var field = keyParts[1].Trim();
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            if (!fieldsByPath.TryGetValue(filePath, out var fields))
            {
                fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                fieldsByPath[filePath] = fields;
            }

            fields[field] = value;
        }

        var charts = new List<ForScoreLibraryChartDraft>();
        foreach (var (filePath, fields) in fieldsByPath)
        {
            var title = Convert.ToString(fields.GetValueOrDefault("title"), CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                warnings.Add($"Skipped chart metadata for '{filePath}' because it did not include a title.");
                continue;
            }

            charts.Add(new ForScoreLibraryChartDraft(
                filePath,
                title,
                NormalizeTitle(title),
                Convert.ToString(fields.GetValueOrDefault("keywords"), CultureInfo.InvariantCulture)?.Trim(),
                fields.GetValueOrDefault("added") as DateTimeOffset?,
                ConvertToNullableInt(fields.GetValueOrDefault("printNumber")),
                ConvertToNullableInt(fields.GetValueOrDefault("version"))));
        }

        if (charts.Count == 0)
        {
            throw new ForScoreLibraryParseException("The forScore backup did not contain any importable charts.");
        }

        return new ForScoreLibraryParseResult(backupVersion, charts, warnings);
    }

    private static int FindGzipOffset(byte[] bytes)
    {
        for (var index = 0; index < bytes.Length - 1; index++)
        {
            if (bytes[index] == GzipMagic[0] && bytes[index + 1] == GzipMagic[1])
            {
                return index;
            }
        }

        return -1;
    }

    private static string? TryReadBackupVersion(ReadOnlySpan<byte> wrapper)
    {
        var text = System.Text.Encoding.ASCII.GetString(wrapper);
        var match = BackupVersionRegex().Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int? ConvertToNullableInt(object? value) => value switch
    {
        null => null,
        int intValue => intValue,
        long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
        _ => null,
    };

    [GeneratedRegex("4SB(V\\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BackupVersionRegex();

    [GeneratedRegex("[^\\p{L}\\p{N}]+")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}

public sealed record ForScoreLibraryParseResult(
    string? BackupVersion,
    IReadOnlyList<ForScoreLibraryChartDraft> Charts,
    IReadOnlyList<string> Warnings);

public sealed record ForScoreLibraryChartDraft(
    string FilePath,
    string Title,
    string NormalizedTitle,
    string? Keywords,
    DateTimeOffset? AddedAt,
    int? PrintNumber,
    int? Version);

public sealed class ForScoreLibraryParseException : Exception
{
    public ForScoreLibraryParseException(string message) : base(message)
    {
    }

    public ForScoreLibraryParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
