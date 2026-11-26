using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

// One shared lock serializes both target privilege checks and the global last-super invariant.
public sealed class AdministrativeMutationCoordinator(AppDbContext db)
{
    // ASCII "GlovAdm": the database-wide advisory lock for administrative mutations.
    // Keep this value stable: old and new instances must coordinate during rolling deployments.
    private const long AdministrativeMutationLockKey = 0x476C6F7641646D;
    private static readonly SemaphoreSlim InMemoryLock = new(1, 1);

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsRelational())
        {
            await InMemoryLock.WaitAsync(cancellationToken);
            try
            {
                return await action();
            }
            finally
            {
                InMemoryLock.Release();
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({AdministrativeMutationLockKey})", cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
