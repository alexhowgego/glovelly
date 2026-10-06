using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Glovelly.Api.Services;

// Session-scoped PostgreSQL locking serializes acquisition as well as application
// across replicas. No transaction stays open while the AI provider is running.
public sealed class IntakeCommandScope(AppDbContext db) : IAsyncDisposable
{
    private string? _key;
    private NpgsqlConnection? _connection;
    public async Task AcquireAsync(Guid userId, CancellationToken token)
    {
        if (!db.Database.IsRelational()) return;
        _connection = new NpgsqlConnection(PostgresDirectConnection.ForSession(db.Database.GetConnectionString()!));
        await _connection.OpenAsync(token);
        _key = $"glovelly:intake:{userId}";
        await ExecuteAsync("SELECT pg_advisory_lock(hashtextextended(@key, 0))", token);
    }
    private async Task ExecuteAsync(string sql, CancellationToken token)
    {
        await using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = _key;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(token);
    }
    public async ValueTask DisposeAsync()
    {
        if (_connection is null) return;
        try { if (_key is not null) await ExecuteAsync("SELECT pg_advisory_unlock(hashtextextended(@key, 0))", CancellationToken.None); }
        finally { await _connection.DisposeAsync(); _connection = null; _key = null; }
    }
}
