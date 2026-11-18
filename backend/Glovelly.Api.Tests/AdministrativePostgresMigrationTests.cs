using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AdministrativePostgresMigrationTests
{
    [PostgresAdministrationFact]
    public async Task LegacyAdminsBecomeUserAdministratorsWithoutElevation()
    {
        var token = TestContext.Current.CancellationToken;
        var name = $"glovelly_auth_test_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GLOVELLY_AUTH_TEST_CONNECTION"))
        {
            Database = "postgres",
            Pooling = false
        };
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync(token);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
        {
            await create.ExecuteNonQueryAsync(token);
        }
        try
        {
            connection.Database = name;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection.ConnectionString,
                    value => value.MigrationsAssembly(typeof(DesignTimeAppDbContextFactory).Assembly.GetName().Name))
                .Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261028214653_AddBackgroundIntakeAnalysis", token);
            var adminId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            db.Users.AddRange(
                new User { Id = adminId, Email = "admin@example.test", Role = UserRole.Admin, IsActive = true, CreatedUtc = DateTime.UtcNow },
                new User { Id = userId, Email = "user@example.test", Role = UserRole.User, IsActive = true, CreatedUtc = DateTime.UtcNow });
            await db.SaveChangesAsync(token);
            await migrator.MigrateAsync(null, token);
            var assignments = await db.UserAdministrativePermissionSets.AsNoTracking().ToListAsync(token);
            var assignment = Assert.Single(assignments);
            Assert.Equal(adminId, assignment.UserId);
            Assert.Equal(AdministrativePermissionSet.UserAdministrator, assignment.PermissionSet);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\"", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
