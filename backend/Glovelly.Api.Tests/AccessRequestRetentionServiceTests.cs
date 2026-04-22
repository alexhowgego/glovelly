using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AccessRequestRetentionServiceTests
{
    [Fact]
    public async Task CleanupIfDueAsync_DeletesRequestsOlderThanRetentionWindow_WhenProbeFindsStaleRows()
    {
        var now = new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateDbContext();
        dbContext.AccessRequests.AddRange(
            CreateRequest("expired@glovelly.local", now.AddDays(-183)),
            CreateRequest("boundary@glovelly.local", now.AddDays(-180)),
            CreateRequest("recent@glovelly.local", now.AddDays(-30)));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, now);

        await service.CleanupIfDueAsync(CancellationToken.None);

        var remainingEmails = await dbContext.AccessRequests
            .OrderBy(value => value.RequestedAtUtc)
            .Select(value => value.NormalizedEmail)
            .ToListAsync();

        Assert.Equal(
            ["boundary@glovelly.local", "recent@glovelly.local"],
            remainingEmails);
    }

    [Fact]
    public async Task CleanupIfDueAsync_SkipsCleanupWhenNoRowsExceedRetentionPlusSlack()
    {
        var now = new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateDbContext();
        dbContext.AccessRequests.AddRange(
            CreateRequest("expired@glovelly.local", now.AddDays(-181)),
            CreateRequest("recent@glovelly.local", now.AddDays(-10)));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, now);

        await service.CleanupIfDueAsync(CancellationToken.None);

        Assert.Equal(2, await dbContext.AccessRequests.CountAsync());
    }

    private static AccessRequestRetentionService CreateService(AppDbContext dbContext, DateTimeOffset now)
    {
        return new AccessRequestRetentionService(
            dbContext,
            new FixedTimeProvider(now),
            Options.Create(new AccessRequestProtectionSettings()),
            NullLogger<AccessRequestRetentionService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"access-request-retention-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static AccessRequest CreateRequest(string email, DateTimeOffset requestedAtUtc)
    {
        return new AccessRequest
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email,
            RequestedAtUtc = requestedAtUtc
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
