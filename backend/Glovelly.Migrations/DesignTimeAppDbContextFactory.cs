using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Glovelly.Migrations;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(args);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(DesignTimeAppDbContextFactory).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }

    private static string ResolveConnectionString(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--connection", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return Environment.GetEnvironmentVariable("ConnectionStrings__Glovelly")
               ?? Environment.GetEnvironmentVariable("GLOVELLY_MIGRATIONS_CONNECTION")
               ?? "Host=localhost;Database=glovelly_migrations_design_time;Username=postgres;Password=postgres";
    }
}
