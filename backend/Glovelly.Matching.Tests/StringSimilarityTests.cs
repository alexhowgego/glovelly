using Xunit;

namespace Glovelly.Matching.Tests;

public sealed class StringSimilarityTests
{
    [Theory]
    [InlineData("L-O-V-E", "LOVE")]
    [InlineData("Jump Jive & Wail", "Jump Jive And Wail")]
    public void Compare_TreatsCommonChartTitleVariantsAsStrongMatches(string left, string right)
    {
        var score = StringSimilarity.Compare(left, right);

        Assert.Equal(1, score.CompactScore);
        Assert.True(score.BestScore >= 0.95);
    }

    [Fact]
    public void Compare_ScoresDescriptorHeavyTitlesByTokenOverlap()
    {
        var score = StringSimilarity.Compare(
            "I Bet You Look Good on the Dancefloor - FULL SONG",
            "I Bet You Look Good on the Dancefloor");

        Assert.True(score.TokenOverlapScore >= 0.85);
        Assert.True(score.BestScore >= 0.85);
    }

    [Fact]
    public void Compare_ScoresUnrelatedTitlesLow()
    {
        var score = StringSimilarity.Compare("Valerie", "I Wanna Dance With Somebody");

        Assert.True(score.BestScore < 0.35);
    }
}
