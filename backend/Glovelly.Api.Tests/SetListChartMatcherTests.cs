using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
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
}
