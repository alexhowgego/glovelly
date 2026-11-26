using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class DraftInvoicePdfMigrationTests
{
    [PostgresAdministrationFact]
    public async Task LegacyDraftsAreInvalidatedWithoutChangingBusinessDataOrIssuedDocuments()
    {
        var token = TestContext.Current.CancellationToken;
        var name = $"glovelly_watermark_test_{Guid.NewGuid():N}";
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
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection.ConnectionString,
                    value => value.MigrationsAssembly(typeof(DesignTimeAppDbContextFactory).Assembly.GetName().Name)).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261113230009_CapabilityAdministration", token);
            var client = new Client { Id = Guid.NewGuid(), Name = "Watermark migration", Email = "migration@example.test" };
            db.Clients.Add(client);
            await db.SaveChangesAsync(token);
            var ids = new List<Guid>();
            foreach (var (status, previouslyIssued) in Enum.GetValues<InvoiceStatus>()
                .Select(status => (status, true)).Append((InvoiceStatus.Cancelled, false)))
            {
                var id = Guid.NewGuid();
                ids.Add(id);
                DateTimeOffset? firstIssued = previouslyIssued
                    ? new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero) : null;
                // Raw SQL deliberately targets the old schema, which lacks the new mapped property.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Invoices" ("Id", "InvoiceNumber", "ClientId", "InvoiceDate", "DueDate",
                        "Status", "Description", "ReissueCount", "DeliveryCount", "FirstIssuedUtc",
                        "LastDeliveredUtc", "PdfStorageKey", "PdfDocumentRevision", "DocumentRevision", "DocumentState",
                        "GigLinesNeedRefresh")
                    VALUES ({id}, {status.ToString()}, {client.Id}, '2026-04-01', '2026-04-15',
                        {status.ToString()}, 'Unchanged content', 2, 3, {firstIssued},
                        '2026-04-02T09:00:00Z', {id.ToString()}, 4, 4, 'Current', false);
                    """, token);
            }
            await migrator.MigrateAsync(null, token);
            var invoices = await db.Invoices.AsNoTracking().OrderBy(value => value.InvoiceNumber).ToListAsync(token);
            Assert.Equal(ids.Count, invoices.Count);
            foreach (var invoice in invoices)
            {
                Assert.Null(invoice.PdfHasDraftWatermark);
                Assert.Equal("Unchanged content", invoice.Description);
                Assert.Equal(new DateOnly(2026, 4, 1), invoice.InvoiceDate);
                Assert.Equal(new DateOnly(2026, 4, 15), invoice.DueDate);
                Assert.Equal(2, invoice.ReissueCount);
                Assert.Equal(3, invoice.DeliveryCount);
                if (invoice.Status != InvoiceStatus.Cancelled || invoice.FirstIssuedUtc.HasValue)
                    Assert.Equal(new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), invoice.FirstIssuedUtc);
                Assert.Equal(new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.Zero), invoice.LastDeliveredUtc);
                Assert.Equal(invoice.Id.ToString(), invoice.PdfStorageKey);
                Assert.Equal(4, invoice.PdfDocumentRevision);
                // Assert this migration's original invalidation rule, before
                // later migrations introduce trusted legacy issuance evidence.
                if (invoice.Status == InvoiceStatus.Draft ||
                    (invoice.Status == InvoiceStatus.Cancelled && !invoice.FirstIssuedUtc.HasValue))
                {
                    Assert.Equal(5, invoice.DocumentRevision);
                    Assert.Equal(InvoiceDocumentState.Failed, invoice.DocumentState);
                    Assert.Contains("Regenerate", invoice.DocumentFailureMessage);
                }
                else
                {
                    Assert.Equal(4, invoice.DocumentRevision);
                    Assert.Equal(InvoiceDocumentState.Current, invoice.DocumentState);
                    Assert.Null(invoice.DocumentFailureMessage);
                }
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\"", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
