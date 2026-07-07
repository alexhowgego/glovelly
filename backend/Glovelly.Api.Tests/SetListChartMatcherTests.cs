using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SetListChartMatcherTests : IClassFixture<GlovellyApiFactory>
{
    private readonly GlovellyApiFactory _factory;

    public SetListChartMatcherTests(GlovellyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MatchAsync_ReturnsExpectedStates()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshot = new ForScoreLibrarySnapshot
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = TestAuthContext.UserId,
            OriginalFileName = "library.4sb",
            SourceFormat = "FourSb",
            IsActive = true,
            ChartCount = 3,
            WarningsJson = "[]",
            ImportedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Charts =
            [
                new ForScoreChart { Id = Guid.NewGuid(), SortOrder = 0, FilePath = "Exact.pdf", Title = "Exact", NormalizedTitle = "exact" },
                new ForScoreChart { Id = Guid.NewGuid(), SortOrder = 1, FilePath = "Valerie.pdf", Title = "Valerie", NormalizedTitle = "valerie" },
                new ForScoreChart { Id = Guid.NewGuid(), SortOrder = 2, FilePath = "Valerie Amy.pdf", Title = "Valerie", NormalizedTitle = "valerie" },
            ],
        };
        db.ForScoreLibrarySnapshots.Add(snapshot);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var matcher = scope.ServiceProvider.GetRequiredService<ISetListChartMatcher>();
        var results = await matcher.MatchAsync(TestAuthContext.UserId,
        [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "Exact"),
            new SetListChartMatchInput(null, 2, GigSetListItemKind.Song, true, "Valerie"),
            new SetListChartMatchInput(null, 3, GigSetListItemKind.Song, true, "Unknown"),
            new SetListChartMatchInput(null, 4, GigSetListItemKind.Comment, false, "Break"),
        ], TestContext.Current.CancellationToken);

        Assert.Equal(ForScoreMappingStatus.Suggested, results[0].Status);
        Assert.Equal(ForScoreMappingStatus.NeedsReview, results[1].Status);
        Assert.Equal(ForScoreMappingStatus.MissingFromLatestLibrary, results[2].Status);
        Assert.Equal(ForScoreMappingStatus.NotApplicable, results[3].Status);
    }

    [Fact]
    public async Task MatchAsync_ReturnsNoActiveLibraryState()
    {
        using var scope = _factory.Services.CreateScope();
        var matcher = scope.ServiceProvider.GetRequiredService<ISetListChartMatcher>();

        var results = await matcher.MatchAsync(Guid.NewGuid(),
        [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "Song"),
        ], TestContext.Current.CancellationToken);

        Assert.Equal(ForScoreMappingStatus.NoActiveLibrary, Assert.Single(results).Status);
    }

    [Theory]
    [InlineData("LOVE", "B-061 L-O-V-E.pdf", "L-O-V-E")]
    [InlineData("Jump Jive & Wail", "B-017 Jump Jive And Wail.pdf", "Jump Jive And Wail")]
    public async Task MatchAsync_RetrievesCommonTitleVariants(string rowTitle, string chartPath, string chartTitle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSnapshotAsync(db, (chartPath, chartTitle, null));

        var matcher = scope.ServiceProvider.GetRequiredService<ISetListChartMatcher>();
        var results = await matcher.MatchAsync(TestAuthContext.UserId,
        [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, rowTitle),
        ], TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(ForScoreMappingStatus.Suggested, result.Status);
        Assert.Equal(chartTitle, result.SelectedChart?.Title);
        Assert.Contains(result.Candidates, candidate => candidate.Evidence.Any(value => value.Contains("title", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task MatchAsync_ChartNumberOutranksTitleOnlyDuplicate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSnapshotAsync(db,
            ("Other/I Wanna Dance With Somebody.pdf", "I Wanna Dance With Somebody", null),
            ("Bella/B-104 Whitney.pdf", "Whitney", null));

        var matcher = scope.ServiceProvider.GetRequiredService<ISetListChartMatcher>();
        var results = await matcher.MatchAsync(TestAuthContext.UserId,
        [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "I Wanna Dance With Somebody", "104.0", "C"),
        ], TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("Bella/B-104 Whitney.pdf", result.SelectedChart?.FilePath);
        Assert.Contains(result.Candidates.Single(candidate => candidate.Chart.Id == result.SelectedChart?.Id).Evidence, value => value == "exact_chart_number");
    }

    [Fact]
    public async Task MatchAsync_NearbyChartNumberIsCandidateButNotSelectedAlone()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSnapshotAsync(db, ("Bella/B-018 Wrong Song.pdf", "Wrong Song", null));

        var matcher = scope.ServiceProvider.GetRequiredService<ISetListChartMatcher>();
        var results = await matcher.MatchAsync(TestAuthContext.UserId,
        [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "Jump Jive & Wail", "17", "Bb"),
        ], TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(ForScoreMappingStatus.NeedsReview, result.Status);
        Assert.Null(result.SelectedChart);
        Assert.Contains(result.Candidates, candidate => candidate.Evidence.Contains("nearby_chart_number"));
    }

    [Fact]
    public async Task MatchAsync_InvalidRankerSelectedChartFallsBackToReview()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedSnapshotAsync(db, ("Exact.pdf", "Exact", null));

        var matcher = new SetListChartMatcher(db, new InvalidRanker(), new DeterministicSetListChartContextualRanker(), NullLogger<SetListChartMatcher>.Instance);
        var results = await matcher.MatchAsync(TestAuthContext.UserId,
        [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "Exact"),
        ], TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(ForScoreMappingStatus.NeedsReview, result.Status);
        Assert.Null(result.SelectedChart);
    }

    private static async Task SeedSnapshotAsync(AppDbContext db, params (string FilePath, string Title, string? Keywords)[] charts)
    {
        db.ForScoreLibrarySnapshots.Add(new ForScoreLibrarySnapshot
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = TestAuthContext.UserId,
            OriginalFileName = "library.4sb",
            SourceFormat = "FourSb",
            IsActive = true,
            ChartCount = charts.Length,
            WarningsJson = "[]",
            ImportedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Charts = charts.Select((chart, index) => new ForScoreChart
            {
                Id = Guid.NewGuid(),
                SortOrder = index,
                FilePath = chart.FilePath,
                Title = chart.Title,
                NormalizedTitle = chart.Title.ToUpperInvariant(),
                Keywords = chart.Keywords,
            }).ToList(),
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class InvalidRanker : ISetListChartContextualRanker
    {
        public Task<IReadOnlyList<SetListChartRankingDecision>> RankAsync(SetListChartRankingRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SetListChartRankingDecision>>(
                request.CandidateSets
                    .Select(value => new SetListChartRankingDecision(
                        value.Input.SourceRowNumber,
                        Guid.NewGuid(),
                        ForScoreMappingStatus.Suggested,
                        ForScoreMappingConfidence.High,
                        "Invalid external response."))
                    .ToList());
        }
    }
}
