using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class EntitlementPostgresMigrationTests
{
    [PostgresAdministrationFact]
    public async Task EmptyDatabaseReceivesCatalogueWithoutFounderGrants()
    {
        await WithDatabaseAsync(async db =>
        {
            var token = TestContext.Current.CancellationToken;
            await db.Database.MigrateAsync(token);
            Assert.Equal(3, await db.Plans.CountAsync(token));
            Assert.Empty(await db.UserSubscriptions.ToListAsync(token));
            Assert.Empty(await db.LicenceChanges.ToListAsync(token));
        });
    }

    [PostgresAdministrationFact]
    public async Task FounderRolloutAndHistoricalUsageAreOneTimeAndPreciselyScoped()
    {
        await WithDatabaseAsync(async db =>
        {
            var token = TestContext.Current.CancellationToken;
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261121091126_AddDraftInvoicePdfWatermark", token);
            var founder = NewUser("founder", "bound", true);
            var inactive = NewUser("inactive", "inactive-bound", true);
            inactive.IsActive = false;
            var unbound = NewUser("unbound", null, true);
            var unlogged = NewUser("unlogged", "never-logged", false);
            var blank = NewUser("blank", "", true);
            db.Users.AddRange(founder, inactive, unbound, unlogged, blank);
            var client = new Client { Id = Guid.NewGuid(), Name = "Migration client" };
            db.Clients.Add(client);
            await db.SaveChangesAsync(token);
            var issuedAt = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
            var attributed = await InsertOldInvoiceAsync(db, client.Id, InvoiceStatus.Issued, founder.Id, issuedAt);
            var sharedAttributed = await InsertOldInvoiceAsync(db, client.Id, InvoiceStatus.Paid, null, issuedAt,
                firstIssuer: founder.Id);
            var ambiguous = await InsertOldInvoiceAsync(db, client.Id, InvoiceStatus.Issued, null, issuedAt);
            var legacy = await InsertOldInvoiceAsync(db, client.Id, InvoiceStatus.Issued, founder.Id, null);
            var redraft = await InsertOldInvoiceAsync(db, client.Id, InvoiceStatus.Draft, founder.Id, null, reissues: 1);
            var cancelled = await InsertOldInvoiceAsync(db, client.Id, InvoiceStatus.Cancelled, founder.Id, null);
            await migrator.MigrateAsync(null, token);

            var grants = await db.UserSubscriptions.AsNoTracking().ToListAsync(token);
            Assert.Equal(2, grants.Count);
            Assert.All(grants, grant =>
            {
                Assert.Contains(grant.UserId, new[] { founder.Id, inactive.Id });
                Assert.Equal(Plan.Pro, grant.PlanCode);
                Assert.Equal("Founder", grant.Reason);
                Assert.Equal("Manual", grant.Source);
                Assert.Null(grant.EndsAtUtc);
            });
            Assert.False((await db.Users.AsNoTracking().SingleAsync(value => value.Id == inactive.Id, token)).IsActive);
            var audit = await db.LicenceChanges.AsNoTracking().ToListAsync(token);
            Assert.Equal(2, audit.Count);
            Assert.All(audit, entry =>
            {
                Assert.Equal("FounderRollout20261122", entry.Source);
                Assert.Null(entry.ActorUserId);
                Assert.Null(entry.PreviousAssignment);
                using var snapshot = JsonDocument.Parse(entry.NewAssignment);
                Assert.Equal(Plan.Pro, snapshot.RootElement.GetProperty("planCode").GetString());
            });

            var usage = await db.UsageLedgerEntries.AsNoTracking().ToListAsync(token);
            Assert.Equal(2, usage.Count);
            Assert.All(usage, entry =>
            {
                Assert.Equal(founder.Id, entry.UserId);
                Assert.Equal(issuedAt, entry.OccurredAtUtc);
                Assert.Contains(entry.SourceInvoiceId, new[] { attributed, sharedAttributed });
            });
            var invoices = await db.Invoices.AsNoTracking().ToDictionaryAsync(value => value.Id, token);
            Assert.True(invoices[legacy].HasLegacyIssuance);
            Assert.True(invoices[redraft].HasLegacyIssuance);
            Assert.False(invoices[cancelled].HasLegacyIssuance);
            Assert.Null(invoices[legacy].FirstIssuedUtc);
            Assert.Null(invoices[ambiguous].CreatedByUserId);

            // Restart/migration replay cannot overwrite a later operator decision.
            var changed = await db.UserSubscriptions.SingleAsync(value => value.UserId == founder.Id, token);
            changed.PlanCode = Plan.Free;
            unlogged.LastLoginUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(token);
            await migrator.MigrateAsync(null, token);
            Assert.Equal(Plan.Free, (await db.UserSubscriptions.AsNoTracking()
                .SingleAsync(value => value.UserId == founder.Id, token)).PlanCode);
            Assert.False(await db.UserSubscriptions.AnyAsync(value => value.UserId == unlogged.Id, token));
            Assert.Equal(2, await db.LicenceChanges.CountAsync(token));
            Assert.Equal(2, await db.UsageLedgerEntries.CountAsync(token));

            // Deleting an invoice must not refund; deleting its account removes live
            // subscription/usage, but audit snapshots have no user FK and survive.
            await db.Invoices.ExecuteDeleteAsync(token);
            Assert.Equal(2, await db.UsageLedgerEntries.CountAsync(token));
            await db.Users.Where(value => value.Id == founder.Id).ExecuteDeleteAsync(token);
            Assert.False(await db.UserSubscriptions.AnyAsync(value => value.UserId == founder.Id, token));
            Assert.Empty(await db.UsageLedgerEntries.ToListAsync(token));
            Assert.Equal(2, await db.LicenceChanges.CountAsync(token));
        });
    }

    private static User NewUser(string name, string? subject, bool loggedIn) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{name}@example.test",
        GoogleSubject = subject,
        CreatedUtc = DateTime.UtcNow,
        LastLoginUtc = loggedIn ? DateTime.UtcNow : null,
    };

    private static async Task<Guid> InsertOldInvoiceAsync(
        AppDbContext db, Guid clientId, InvoiceStatus status, Guid? owner,
        DateTimeOffset? firstIssued, int reissues = 0, Guid? firstIssuer = null)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Invoices" ("Id", "InvoiceNumber", "ClientId", "InvoiceDate", "DueDate", "Status",
                "CreatedByUserId", "FirstIssuedUtc", "FirstIssuedByUserId", "ReissueCount", "DeliveryCount",
                "GigLinesNeedRefresh")
            VALUES ({id}, {id.ToString()}, {clientId}, '2026-10-01', '2026-10-15', {status.ToString()},
                {owner}, {firstIssued}, {firstIssuer}, {reissues}, 0, false);
            """, TestContext.Current.CancellationToken);
        return id;
    }

    internal static async Task WithDatabaseAsync(Func<AppDbContext, Task> action)
    {
        var token = TestContext.Current.CancellationToken;
        var name = $"glovelly_entitlement_test_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GLOVELLY_AUTH_TEST_CONNECTION"))
        {
            Database = "postgres",
            Pooling = false,
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
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection.ConnectionString,
                    value => value.MigrationsAssembly(typeof(DesignTimeAppDbContextFactory).Assembly.GetName().Name)).Options);
            await action(db);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\"", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
