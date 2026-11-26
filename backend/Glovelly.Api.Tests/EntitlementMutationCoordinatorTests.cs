using Glovelly.Api.Data;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class EntitlementMutationCoordinatorTests
{
    [Fact]
    public async Task InMemoryRequestsForOneUserAreSerialized()
    {
        await using var first = InMemoryContext();
        await using var second = InMemoryContext();
        await AssertSerializedAsync(first, second);
    }

    [PostgresAdministrationFact]
    public async Task PostgreSqlRequestsForOneUserAreSerializedAcrossContexts()
    {
        await using var first = PostgresContext();
        await using var second = PostgresContext();
        await AssertSerializedAsync(first, second);
    }

    [PostgresAdministrationFact]
    public async Task ExistingTransactionIsNotCommittedByInnerCoordinator()
    {
        await using var db = PostgresContext();
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var result = await new EntitlementMutationCoordinator(db).ExecuteAsync(
            Guid.NewGuid(), () => Task.FromResult(42), TestContext.Current.CancellationToken);
        Assert.Equal(42, result);
        Assert.Same(transaction, db.Database.CurrentTransaction);
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    private static async Task AssertSerializedAsync(AppDbContext first, AppDbContext second)
    {
        var userId = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = new EntitlementMutationCoordinator(first).ExecuteAsync(userId, async () =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
            return true;
        }, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.CancelAfter(TimeSpan.FromSeconds(1));
            var ran = false;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new EntitlementMutationCoordinator(second).ExecuteAsync(userId, () =>
                {
                    ran = true;
                    return Task.FromResult(true);
                }, cancellation.Token));
            Assert.False(ran);

            // This is not a global quota mutex: unrelated users can proceed.
            if (second.Database.IsRelational())
            {
                using var independentTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                independentTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                var other = await new EntitlementMutationCoordinator(second).ExecuteAsync(
                    Guid.NewGuid(), () => Task.FromResult(true), independentTimeout.Token);
                Assert.True(other);
            }
        }
        finally
        {
            release.TrySetResult();
            await operation;
        }
        Assert.True(await new EntitlementMutationCoordinator(second).ExecuteAsync(
            userId, () => Task.FromResult(true), TestContext.Current.CancellationToken));
    }

    private static AppDbContext InMemoryContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AppDbContext PostgresContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("GLOVELLY_AUTH_TEST_CONNECTION")).Options);
}
