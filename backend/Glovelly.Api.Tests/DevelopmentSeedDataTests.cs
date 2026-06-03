using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class DevelopmentSeedDataTests
{
    [Fact]
    public async Task SeedAsync_RiversideInvoiceRefresh_DoesNotDuplicateGeneratedPerformanceFee()
    {
        await using var dbContext = CreateDbContext();
        var configuration = CreateSeedConfiguration();

        var attachmentStore = new InMemoryExpenseAttachmentStore();
        await AppDbSeeder.SeedAsync(dbContext, configuration, attachmentStore);

        var seededAdminUserId = await dbContext.Users
            .Where(user => user.GoogleSubject == TestAuthContext.DefaultSubject)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        var invoice = await dbContext.Invoices
            .Include(value => value.Lines)
            .SingleAsync(value => value.InvoiceNumber == "GLV-2026-002", TestContext.Current.CancellationToken);
        var gig = await dbContext.Gigs
            .Include(value => value.Expenses)
            .ThenInclude(value => value.Attachments)
            .SingleAsync(value => value.InvoiceId == invoice.Id, TestContext.Current.CancellationToken);

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.NotNull(await dbContext.SellerProfiles.SingleOrDefaultAsync(value => value.UserId == seededAdminUserId, TestContext.Current.CancellationToken));
        Assert.Contains(gig.Expenses, expense => expense.Attachments.Count == 1);
        Assert.Contains(gig.Expenses, expense => expense.ReimbursementStatus == GigExpenseReimbursementStatus.Reimbursed);
        var seededAttachment = gig.Expenses.SelectMany(expense => expense.Attachments).Single();
        await using (var attachmentContent = (await attachmentStore.OpenReadAsync(seededAttachment.StorageKey, TestContext.Current.CancellationToken)).Content)
        {
            Assert.True(attachmentContent.Length > 0);
        }

        var workflowService = CreateWorkflowService(dbContext);
        var lunchExpense = gig.Expenses.Single(expense => expense.Description == "Lunch");
        lunchExpense.ReimbursementStatus = GigExpenseReimbursementStatus.Unreimbursed;
        lunchExpense.ReimbursedAt = null;
        lunchExpense.ReimbursementMethod = null;
        lunchExpense.ReimbursementNote = null;
        lunchExpense.ReimbursementInvoiceId = null;
        await workflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, seededAdminUserId, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refreshedLines = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoice.Id)
            .OrderBy(line => line.SortOrder)
            .ToListAsync(TestContext.Current.CancellationToken);
        var refreshedGigLines = refreshedLines
            .Where(line => line.GigId == gig.Id)
            .ToList();

        Assert.Single(refreshedGigLines, line => line.Type == InvoiceLineType.PerformanceFee);
        Assert.Single(refreshedLines, line => line.Type == InvoiceLineType.ManualAdjustment);
        Assert.Single(refreshedGigLines, line => line.Description == "Lunch");
        Assert.All(refreshedGigLines, line => Assert.True(line.IsSystemGenerated));
        Assert.All(refreshedGigLines, line => Assert.NotEqual(default, line.CreatedUtc));
    }

    [Fact]
    public async Task SeedAsync_RiversideDrivingToggle_RestoresMileageLinesWhenDrivingIsReenabled()
    {
        await using var dbContext = CreateDbContext();
        var configuration = CreateSeedConfiguration();
        var attachmentStore = new InMemoryExpenseAttachmentStore();
        await AppDbSeeder.SeedAsync(dbContext, configuration, attachmentStore);

        var seededAdminUserId = await dbContext.Users
            .Where(user => user.GoogleSubject == TestAuthContext.DefaultSubject)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        var invoice = await dbContext.Invoices.SingleAsync(value => value.InvoiceNumber == "GLV-2026-002", TestContext.Current.CancellationToken);
        var gig = await dbContext.Gigs
            .Include(value => value.Expenses)
            .SingleAsync(value => value.InvoiceId == invoice.Id, TestContext.Current.CancellationToken);
        var workflowService = CreateWorkflowService(dbContext);

        Assert.True(gig.WasDriving);
        Assert.True(gig.TravelMiles > 0);

        gig.WasDriving = false;
        await workflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, seededAdminUserId, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var nonDrivingLines = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoice.Id && line.GigId == gig.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(nonDrivingLines, line => line.Type == InvoiceLineType.Mileage);

        gig.WasDriving = true;
        await workflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, seededAdminUserId, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var drivingLines = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoice.Id && line.GigId == gig.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(drivingLines, line =>
            line.Type == InvoiceLineType.Mileage &&
            line.Quantity == gig.TravelMiles);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"glovelly-development-seed-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static InvoiceWorkflowService CreateWorkflowService(AppDbContext dbContext)
    {
        return new InvoiceWorkflowService(
            dbContext,
            new InvoiceNumberService(dbContext),
            new InvoiceLineGenerationService(dbContext, Options.Create(new InvoiceRateSettings())),
            new InvoiceProfileDefaultsService(dbContext),
            new InvoicePdfRenderer(),
            new InvoicePdfService(new InMemoryBlobStore(), TimeProvider.System));
    }

    private static IConfiguration CreateSeedConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevelopmentSeeding:AdminGoogleSubject"] = TestAuthContext.DefaultSubject,
                ["DevelopmentSeeding:AdminEmail"] = "local-admin@glovelly.local",
                ["DevelopmentSeeding:AdminDisplayName"] = "Local Admin",
            })
            .Build();
    }
}
