using Xunit;

namespace Glovelly.Matching.Tests;

public sealed class MatchTextNormalizerTests
{
    [Theory]
    [InlineData("L-O-V-E", "LOVE")]
    [InlineData("Jump Jive & Wail", "Jump Jive And Wail")]
    public void Normalize_ProducesComparableCompactForms(string left, string right)
    {
        var normalizedLeft = MatchTextNormalizer.Normalize(left);
        var normalizedRight = MatchTextNormalizer.Normalize(right);

        Assert.Equal(normalizedLeft.Compact, normalizedRight.Compact);
    }

    [Fact]
    public void Normalize_PreservesOriginalAndBuildsTokens()
    {
        var normalized = MatchTextNormalizer.Normalize("I Bet You Look Good on the Dancefloor - FULL SONG");

        Assert.Equal("I Bet You Look Good on the Dancefloor - FULL SONG", normalized.Original);
        Assert.Contains("dancefloor", normalized.Tokens);
        Assert.Contains("full", normalized.Tokens);
        Assert.DoesNotContain("the", normalized.Tokens);
    }
}
