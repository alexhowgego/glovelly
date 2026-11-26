using System.Buffers.Binary;
using System.Security.Cryptography;
using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public sealed class EntitlementMutationCoordinator(AppDbContext db)
{
    // Two-int advisory-lock namespace ASCII "GlEn", disjoint from the existing
    // single-bigint administrative namespace. Stable SHA-256 GUID text key.
    // Order: administrative coordinator -> entitlement user -> invoice.
    private const int LockNamespace = 0x476C456E;
    // Bounded stripes avoid retaining one semaphore forever per seen user.
    // Hash collisions only serialize unrelated fixture users, never bypass a lock.
    private static readonly SemaphoreSlim[] InMemoryLocks =
        Enumerable.Range(0, 1024).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private static readonly SemaphoreSlim[] InMemoryInvoiceLocks =
        Enumerable.Range(0, 1024).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public Task<T> ExecuteInvoiceAsync<T>(
        Guid userId, Guid invoiceId, Func<Task<T>> action, CancellationToken cancellationToken = default) =>
        ExecuteAsync(userId, async () =>
        {
            // Shared legacy invoices may have different billing users. Lock the
            // row after the user lock and before inspecting issuance identity.
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Invoices\" WHERE \"Id\" = {invoiceId} FOR UPDATE", cancellationToken);
                return await action();
            }
            var key = BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(invoiceId.ToString("N"))));
            var gate = InMemoryInvoiceLocks[(uint)key % InMemoryInvoiceLocks.Length];
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await action();
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(
        Guid userId, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var key = BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(userId.ToString("N"))));
        if (!db.Database.IsRelational())
        {
            var gate = InMemoryLocks[(uint)key % InMemoryLocks.Length];
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await action();
            }
            finally
            {
                gate.Release();
            }
        }

        // Licence assignment already owns the administrative transaction. Its
        // outer coordinator commits; never begin or commit a nested transaction.
        if (db.Database.CurrentTransaction is not null)
        {
            await AcquireAsync(key, cancellationToken);
            return await action();
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireAsync(key, cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private Task AcquireAsync(int key, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({LockNamespace}, {key})", cancellationToken);
}
