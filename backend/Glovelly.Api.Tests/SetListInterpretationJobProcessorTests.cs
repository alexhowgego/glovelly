using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SetListInterpretationJobProcessorTests
{
    [Fact]
    public async Task ProcessAsync_PersistsSafeFailureAndRetainsSourceGrid()
    {
        await using var db = CreateDbContext();
        var job = CreateJob(SetListInterpretationJobStatus.Pending, DateTimeOffset.UtcNow.AddDays(7));
        job.SourceGridJson = JsonSerializer.Serialize(
            new GoogleSheetGrid("Set list", 1, 1, [new GoogleSheetGridCell(1, 1, "A1", "Sensitive unpublished song")]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.SetListInterpretationJobs.Add(job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var events = new CapturingWorkspaceEventPublisher();
        var processor = new SetListInterpretationJobProcessor(
            db,
            new ThrowingInterpreter(),
            events,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<SetListInterpretationJobProcessor>.Instance);

        await processor.ProcessAsync(job.Id, TestContext.Current.CancellationToken);

        var stored = await db.SetListInterpretationJobs.AsNoTracking().SingleAsync(value => value.Id == job.Id, TestContext.Current.CancellationToken);
        Assert.Equal(SetListInterpretationJobStatus.Failed, stored.Status);
        Assert.Contains("Set-list interpretation failed", stored.SafeErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive unpublished song", stored.SafeErrorMessage, StringComparison.Ordinal);
        Assert.Null(stored.ResultJson);
        Assert.NotEqual("{}", stored.SourceGridJson);
        Assert.Contains(events.Events, value => value.Event.Action == "failed" && value.UserId == job.UserId);
    }

    [Fact]
    public async Task CleanupAsync_RemovesExpiredCompletedAndFailedSourceGrids()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using var db = CreateDbContext();
        var completed = CreateJob(SetListInterpretationJobStatus.Completed, now.AddMinutes(-1));
        var failed = CreateJob(SetListInterpretationJobStatus.Failed, now.AddMinutes(-1));
        var retained = CreateJob(SetListInterpretationJobStatus.Failed, now.AddDays(1));
        db.SetListInterpretationJobs.AddRange(completed, failed, retained);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await new SetListInterpretationRetentionService(db, new FixedTimeProvider(now)).CleanupAsync(TestContext.Current.CancellationToken);

        var jobs = await db.SetListInterpretationJobs.AsNoTracking().ToDictionaryAsync(value => value.Id, TestContext.Current.CancellationToken);
        Assert.Equal("{}", jobs[completed.Id].SourceGridJson);
        Assert.Equal("{}", jobs[failed.Id].SourceGridJson);
        Assert.NotEqual("{}", jobs[retained.Id].SourceGridJson);
    }

    private static AppDbContext CreateDbContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"set-list-interpretation-{Guid.NewGuid()}")
            .Options);
    }

    private static SetListInterpretationJob CreateJob(SetListInterpretationJobStatus status, DateTimeOffset expiresAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        GigId = Guid.NewGuid(),
        GigExternalResourceId = Guid.NewGuid(),
        SpreadsheetId = "spreadsheet",
        WorksheetName = "Set list",
        Status = status,
        SourceGridJson = "{\"worksheetName\":\"Set list\",\"rowCount\":1,\"columnCount\":1,\"cells\":[]}",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        SourceGridExpiresAtUtc = expiresAtUtc,
    };

    private sealed class ThrowingInterpreter : ISetListInterpreter
    {
        public Task<IReadOnlyList<SetListImportItemDraft>> InterpretAsync(GoogleSheetGrid grid, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Provider response included Sensitive unpublished song.");
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
