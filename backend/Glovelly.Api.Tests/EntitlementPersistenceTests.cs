using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class EntitlementPersistenceTests
{
    [Fact]
    public async Task CatalogueIsSeededForInMemoryFixtures()
    {
        await using var db = CreateDatabase();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var plans = await db.Plans.OrderBy(value => value.Code).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, plans.Count);
        var free = Assert.Single(plans, value => value.Code == Plan.Free);
        Assert.Equal(2, free.MonthlyFirstIssueLimit);
        Assert.False(free.PaidIncomeSummary);
        Assert.All(plans.Where(value => value.Code != Plan.Free), value =>
        {
            Assert.Null(value.MonthlyFirstIssueLimit);
            Assert.True(value.PaidIncomeSummary);
        });
    }

    [Fact]
    public void UsageHasUniqueSourceAndNoInvoiceRelationship()
    {
        using var db = CreateDatabase();
        var entity = db.Model.FindEntityType(typeof(UsageLedgerEntry))!;
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(value => value.Name).SequenceEqual(
                [nameof(UsageLedgerEntry.Metric), nameof(UsageLedgerEntry.SourceInvoiceId)]));
        Assert.DoesNotContain(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Invoice));
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void AuditHasNoUserForeignKeysAndAssignmentUsesConcurrencyVersion()
    {
        using var db = CreateDatabase();
        Assert.Empty(db.Model.FindEntityType(typeof(LicenceChange))!.GetForeignKeys());
        Assert.True(db.Model.FindEntityType(typeof(UserSubscription))!
            .FindProperty(nameof(UserSubscription.Version))!.IsConcurrencyToken);
    }

    [Fact]
    public void LegacyExemptionCannotBeInjectedThroughInvoiceJson()
    {
        var invoice = JsonSerializer.Deserialize<Invoice>("""{"HasLegacyIssuance":true}""")!;
        Assert.False(invoice.HasLegacyIssuance);
        Assert.DoesNotContain("HasLegacyIssuance", JsonSerializer.Serialize(new Invoice { HasLegacyIssuance = true }));
    }

    private static AppDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
