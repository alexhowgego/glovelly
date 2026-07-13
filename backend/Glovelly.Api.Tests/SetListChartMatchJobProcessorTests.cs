using System.Text.Json;
using System.Text.Json.Serialization;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SetListChartMatchJobProcessorTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly GlovellyApiFactory _factory;

    public SetListChartMatchJobProcessorTests(GlovellyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessAsync_PassesWholeSetInputAndCompletesWithResults()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gigId = await SeedGigAsync(db);
        var jobId = await SeedJobAsync(db, gigId, [
            new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "First"),
            new SetListChartMatchInput(null, 2, GigSetListItemKind.Song, true, "Second"),
            new SetListChartMatchInput(null, 3, GigSetListItemKind.Comment, false, "Break"),
        ]);
        var matcher = new CapturingMatcher([
            new SetListChartMatchResult(null, 1, ForScoreMappingStatus.NeedsReview, ForScoreMappingConfidence.Low, "Choose the matching forScore chart.", null, []),
            new SetListChartMatchResult(null, 2, ForScoreMappingStatus.MissingFromLatestLibrary, ForScoreMappingConfidence.None, "No chart found.", null, []),
            new SetListChartMatchResult(null, 3, ForScoreMappingStatus.NotApplicable, ForScoreMappingConfidence.None, "Only included songs can be linked.", null, []),
        ]);
        var publisher = new CapturingWorkspaceEventPublisher();
        var processor = new SetListChartMatchJobProcessor(db, matcher, publisher, new FixedTimeProvider(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero)), NullLogger<SetListChartMatchJobProcessor>.Instance);

        await processor.ProcessAsync(jobId, TestContext.Current.CancellationToken);

        Assert.True(matcher.UseConfiguredRanker);
        Assert.Equal(3, matcher.Items.Count);
        var stored = await db.SetListChartMatchJobs.AsNoTracking().SingleAsync(job => job.Id == jobId, TestContext.Current.CancellationToken);
        Assert.Equal(SetListChartMatchJobStatus.Completed, stored.Status);
        Assert.NotNull(stored.ResultJson);
        var result = JsonSerializer.Deserialize<IReadOnlyList<SetListChartMatchResult>>(stored.ResultJson!, JsonOptions);
        Assert.Equal(3, result?.Count);
        Assert.Contains(publisher.Events, value => value.Event.Action == "started" && value.UserId == TestAuthContext.UserId);
        Assert.Contains(publisher.Events, value => value.Event.Action == "completed" && value.Event.Metadata?["gigId"] == gigId.ToString());
    }

    [Fact]
    public async Task ProcessAsync_PersistsSafeFailure()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gigId = await SeedGigAsync(db);
        var jobId = await SeedJobAsync(db, gigId, [new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "Sensitive title")]);
        var publisher = new CapturingWorkspaceEventPublisher();
        var processor = new SetListChartMatchJobProcessor(db, new ThrowingMatcher(), publisher, new FixedTimeProvider(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero)), NullLogger<SetListChartMatchJobProcessor>.Instance);

        await processor.ProcessAsync(jobId, TestContext.Current.CancellationToken);

        var stored = await db.SetListChartMatchJobs.AsNoTracking().SingleAsync(job => job.Id == jobId, TestContext.Current.CancellationToken);
        Assert.Equal(SetListChartMatchJobStatus.Failed, stored.Status);
        Assert.Contains("Chart matching failed", stored.SafeErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive title", stored.SafeErrorMessage, StringComparison.Ordinal);
        Assert.Null(stored.ResultJson);
        Assert.Contains(publisher.Events, value => value.Event.Action == "failed" && value.UserId == TestAuthContext.UserId);
    }

    [Fact]
    public async Task FailStaleRunningJobsAsync_MarksStaleJobsFailed()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gigId = await SeedGigAsync(db);
        var now = new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);
        var jobId = await SeedJobAsync(db, gigId, [new SetListChartMatchInput(null, 1, GigSetListItemKind.Song, true, "First")], SetListChartMatchJobStatus.Running, now.AddHours(-1));
        var publisher = new CapturingWorkspaceEventPublisher();
        var processor = new SetListChartMatchJobProcessor(db, new CapturingMatcher([]), publisher, new FixedTimeProvider(now), NullLogger<SetListChartMatchJobProcessor>.Instance);

        await processor.FailStaleRunningJobsAsync(TestContext.Current.CancellationToken);

        var stored = await db.SetListChartMatchJobs.AsNoTracking().SingleAsync(job => job.Id == jobId, TestContext.Current.CancellationToken);
        Assert.Equal(SetListChartMatchJobStatus.Failed, stored.Status);
        Assert.Contains("stopped before it completed", stored.SafeErrorMessage, StringComparison.Ordinal);
        Assert.Contains(publisher.Events, value => value.Event.Action == "failed" && value.UserId == TestAuthContext.UserId);
    }

    private static async Task<Guid> SeedGigAsync(AppDbContext db)
    {
        var gigId = Guid.NewGuid();
        db.Gigs.Add(new Gig
        {
            Id = gigId,
            ClientId = TestData.FoxAndFinchId,
            CreatedByUserId = TestAuthContext.UserId,
            UpdatedByUserId = TestAuthContext.UserId,
            Title = "Job processor test gig",
            Date = new DateOnly(2026, 7, 12),
            Venue = "Test venue",
            Fee = 100,
            Status = GigStatus.Confirmed,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return gigId;
    }

    private static async Task<Guid> SeedJobAsync(
        AppDbContext db,
        Guid gigId,
        IReadOnlyList<SetListChartMatchInput> input,
        SetListChartMatchJobStatus status = SetListChartMatchJobStatus.Pending,
        DateTimeOffset? startedAtUtc = null)
    {
        var now = new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero);
        var job = new SetListChartMatchJob
        {
            Id = Guid.NewGuid(),
            UserId = TestAuthContext.UserId,
            GigId = gigId,
            Status = status,
            InputJson = JsonSerializer.Serialize(input, JsonOptions),
            CorrelationId = "processor-test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            StartedAtUtc = startedAtUtc,
        };
        db.SetListChartMatchJobs.Add(job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return job.Id;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class CapturingMatcher(IReadOnlyList<SetListChartMatchResult> results) : ISetListChartMatcher
    {
        public IReadOnlyList<SetListChartMatchInput> Items { get; private set; } = [];
        public bool UseConfiguredRanker { get; private set; }

        public Task<IReadOnlyList<SetListChartMatchResult>> MatchAsync(Guid? userId, IReadOnlyList<SetListChartMatchInput> items, CancellationToken cancellationToken = default, bool useConfiguredRanker = true)
        {
            Items = items;
            UseConfiguredRanker = useConfiguredRanker;
            return Task.FromResult(results);
        }
    }

    private sealed class ThrowingMatcher : ISetListChartMatcher
    {
        public Task<IReadOnlyList<SetListChartMatchResult>> MatchAsync(Guid? userId, IReadOnlyList<SetListChartMatchInput> items, CancellationToken cancellationToken = default, bool useConfiguredRanker = true)
        {
            throw new InvalidOperationException("Sensitive provider response for Sensitive title.");
        }
    }

    private sealed class CapturingWorkspaceEventPublisher : IWorkspaceEventPublisher
    {
        public List<(Guid? UserId, WorkspaceEvent Event)> Events { get; } = [];

        public Task PublishAsync(Guid? userId, WorkspaceEvent workspaceEvent, CancellationToken cancellationToken = default)
        {
            Events.Add((userId, workspaceEvent));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
