using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
            .SingleAsync();
        var invoice = await dbContext.Invoices
            .Include(value => value.Lines)
            .SingleAsync(value => value.InvoiceNumber == "GLV-2026-002");
        var gig = await dbContext.Gigs
            .Include(value => value.Expenses)
            .ThenInclude(value => value.Attachments)
            .SingleAsync(value => value.InvoiceId == invoice.Id);

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.NotNull(await dbContext.SellerProfiles.SingleOrDefaultAsync(value => value.UserId == seededAdminUserId));
        Assert.Contains(gig.Expenses, expense => expense.Attachments.Count == 1);
        Assert.Contains(gig.Expenses, expense => expense.ReimbursementStatus == GigExpenseReimbursementStatus.Reimbursed);
        var seededAttachment = gig.Expenses.SelectMany(expense => expense.Attachments).Single();
        await using (var attachmentContent = (await attachmentStore.OpenReadAsync(seededAttachment.StorageKey)).Content)
        {
            Assert.True(attachmentContent.Length > 0);
        }

        var workflowService = new InvoiceWorkflowService(dbContext);
        var lunchExpense = gig.Expenses.Single(expense => expense.Description == "Lunch");
        lunchExpense.ReimbursementStatus = GigExpenseReimbursementStatus.Unreimbursed;
        lunchExpense.ReimbursedAt = null;
        lunchExpense.ReimbursementMethod = null;
        lunchExpense.ReimbursementNote = null;
        lunchExpense.ReimbursementInvoiceId = null;
        await workflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, seededAdminUserId);
        await dbContext.SaveChangesAsync();

        var refreshedLines = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoice.Id)
            .OrderBy(line => line.SortOrder)
            .ToListAsync();
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
            .SingleAsync();
        var invoice = await dbContext.Invoices.SingleAsync(value => value.InvoiceNumber == "GLV-2026-002");
        var gig = await dbContext.Gigs
            .Include(value => value.Expenses)
            .SingleAsync(value => value.InvoiceId == invoice.Id);
        var workflowService = new InvoiceWorkflowService(dbContext);

        Assert.True(gig.WasDriving);
        Assert.True(gig.TravelMiles > 0);

        gig.WasDriving = false;
        await workflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, seededAdminUserId);
        await dbContext.SaveChangesAsync();

        var nonDrivingLines = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoice.Id && line.GigId == gig.Id)
            .ToListAsync();
        Assert.DoesNotContain(nonDrivingLines, line => line.Type == InvoiceLineType.Mileage);

        gig.WasDriving = true;
        await workflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, seededAdminUserId);
        await dbContext.SaveChangesAsync();

        var drivingLines = await dbContext.InvoiceLines
            .Where(line => line.InvoiceId == invoice.Id && line.GigId == gig.Id)
            .ToListAsync();
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
