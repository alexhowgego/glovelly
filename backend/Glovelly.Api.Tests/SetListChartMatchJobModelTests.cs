using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SetListChartMatchJobModelTests : IClassFixture<GlovellyApiFactory>
{
    private readonly GlovellyApiFactory _factory;

    public SetListChartMatchJobModelTests(GlovellyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Job_PersistsInputResultStatusOwnerGigCorrelationAndTimestamps()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gigId = await SeedGigAsync(db);
        var jobId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);
        var inputJson = JsonSerializer.Serialize(new[]
        {
            new { sourceRowNumber = 3, kind = "Song", include = true, title = "L-O-V-E", padNumber = "74-G", key = "G" },
        });
        var resultJson = JsonSerializer.Serialize(new[]
        {
            new { sourceRowNumber = 3, status = "Suggested", reason = "Suggested by title similarity." },
        });

        db.SetListChartMatchJobs.Add(new SetListChartMatchJob
        {
            Id = jobId,
            UserId = TestAuthContext.UserId,
            GigId = gigId,
            Status = SetListChartMatchJobStatus.Pending,
            InputJson = inputJson,
            CorrelationId = "job-test-correlation",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var job = await db.SetListChartMatchJobs.SingleAsync(value => value.Id == jobId, TestContext.Current.CancellationToken);
        job.Status = SetListChartMatchJobStatus.Completed;
        job.ResultJson = resultJson;
        job.StartedAtUtc = now.AddSeconds(1);
        job.CompletedAtUtc = now.AddSeconds(2);
        job.UpdatedAtUtc = now.AddSeconds(2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.ChangeTracker.Clear();
        var stored = await db.SetListChartMatchJobs.AsNoTracking().SingleAsync(value => value.Id == jobId, TestContext.Current.CancellationToken);
        Assert.Equal(TestAuthContext.UserId, stored.UserId);
        Assert.Equal(gigId, stored.GigId);
        Assert.Equal(SetListChartMatchJobStatus.Completed, stored.Status);
        Assert.Equal("job-test-correlation", stored.CorrelationId);
        Assert.Equal(now, stored.CreatedAtUtc);
        Assert.Equal(now.AddSeconds(1), stored.StartedAtUtc);
        Assert.Equal(now.AddSeconds(2), stored.CompletedAtUtc);
        Assert.Contains("sourceRowNumber", stored.InputJson, StringComparison.Ordinal);
        Assert.Contains("Suggested", stored.ResultJson, StringComparison.Ordinal);
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
            Title = "Job model test gig",
            Date = new DateOnly(2026, 7, 12),
            Venue = "Test venue",
            Fee = 100,
            Status = GigStatus.Confirmed,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return gigId;
    }
}
