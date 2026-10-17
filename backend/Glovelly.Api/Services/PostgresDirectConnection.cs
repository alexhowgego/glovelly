using Npgsql;

namespace Glovelly.Api.Services;

// Neon transaction pooling does not preserve SET/search_path or session locks
// between statements. Session-scoped operations must use its direct endpoint.
public static class PostgresDirectConnection
{
    public static string ForSession(string connectionString)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (settings.Host is { } host && host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase)) settings.Host = host.Replace("-pooler.", ".", StringComparison.OrdinalIgnoreCase);
        return settings.ConnectionString;
    }
}
