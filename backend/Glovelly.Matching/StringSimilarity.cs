namespace Glovelly.Matching;

public static class StringSimilarity
{
    public static StringSimilarityScore Compare(string? left, string? right) => Compare(
        MatchTextNormalizer.Normalize(left),
        MatchTextNormalizer.Normalize(right));

    public static StringSimilarityScore Compare(MatchText left, MatchText right)
    {
        var compactScore = CompactScore(left.Compact, right.Compact);
        var tokenOverlapScore = TokenOverlapScore(left.Tokens, right.Tokens);
        var editDistanceScore = EditDistanceScore(left.Canonical, right.Canonical);
        return new StringSimilarityScore(
            compactScore,
            tokenOverlapScore,
            editDistanceScore,
            Math.Max(compactScore, Math.Max(tokenOverlapScore, editDistanceScore)));
    }

    private static double CompactScore(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1;
        }

        return left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal)
            ? (double)Math.Min(left.Length, right.Length) / Math.Max(left.Length, right.Length)
            : 0;
    }

    private static double TokenOverlapScore(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        var rightSet = right.ToHashSet(StringComparer.Ordinal);
        leftSet.IntersectWith(rightSet);
        return (double)(2 * leftSet.Count) / (left.Count + right.Count);
    }

    private static double EditDistanceScore(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        var distance = LevenshteinDistance(left, right);
        return 1 - ((double)distance / Math.Max(left.Length, right.Length));
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var cost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}

public sealed record StringSimilarityScore(
    double CompactScore,
    double TokenOverlapScore,
    double EditDistanceScore,
    double BestScore);
