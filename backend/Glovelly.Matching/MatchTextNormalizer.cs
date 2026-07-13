using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Glovelly.Matching;

public static partial class MatchTextNormalizer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a",
        "an",
        "the",
    };

    public static MatchText Normalize(string? value)
    {
        var original = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(original))
        {
            return new MatchText(string.Empty, string.Empty, string.Empty, []);
        }

        var decomposed = original.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var lowered = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        lowered = lowered.Replace("&", " and ", StringComparison.Ordinal);
        lowered = AndWordRegex().Replace(lowered, " and ");
        lowered = ApostropheRegex().Replace(lowered, string.Empty);
        var canonical = NonWordRegex().Replace(lowered, " ");
        canonical = WhitespaceRegex().Replace(canonical, " ").Trim();

        var rawTokens = canonical.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokens = rawTokens.Where(token => !StopWords.Contains(token)).ToList();
        var compact = string.Concat(rawTokens);

        return new MatchText(original, canonical, compact, tokens);
    }

    [GeneratedRegex("\\b(and|&)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AndWordRegex();

    [GeneratedRegex("['’]", RegexOptions.CultureInvariant)]
    private static partial Regex ApostropheRegex();

    [GeneratedRegex("[^\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
