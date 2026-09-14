using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class DocumentationFixtureSeederTests
{
    [Fact]
    public async Task ResetAndSeedAsync_RecreatesTheSameFixtureAndLeavesOtherUsersUntouched()
    {
        await using var db = CreateDbContext();
        var attachmentStore = new InMemoryExpenseAttachmentStore();
        var pdfService = new InvoicePdfService(new InMemoryBlobStore(), TimeProvider.System);
        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@example.test",
            IsActive = true,
        };
        db.Users.Add(otherUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await DocumentationFixtureSeeder.ResetAndSeedAsync(db, attachmentStore, pdfService, TestContext.Current.CancellationToken);
        var firstInvoice = await db.Invoices.SingleAsync(value => value.Id == DocumentationFixtureSeeder.InvoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceDocumentState.Current, firstInvoice.DocumentState);
        Assert.NotNull(await pdfService.OpenReadAsync(firstInvoice, TestContext.Current.CancellationToken));

        db.Gigs.Add(new Gig
        {
            Id = Guid.NewGuid(),
            ClientId = DocumentationFixtureSeeder.ClientId,
            CreatedByUserId = DocumentationFixtureSeeder.UserId,
            UpdatedByUserId = DocumentationFixtureSeeder.UserId,
            Title = "Discard this capture state",
            Venue = "Temporary venue",
            Date = new DateOnly(2026, 4, 19),
            Fee = 1m,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await DocumentationFixtureSeeder.ResetAndSeedAsync(db, attachmentStore, pdfService, TestContext.Current.CancellationToken);

        Assert.Equal(2, await db.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(DocumentationFixtureSeeder.DisplayName, await db.Users.Where(value => value.Id == DocumentationFixtureSeeder.UserId).Select(value => value.DisplayName).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Clients.Where(value => value.CreatedByUserId == DocumentationFixtureSeeder.UserId).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Gigs.Where(value => value.CreatedByUserId == DocumentationFixtureSeeder.UserId).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Invoices.Where(value => value.CreatedByUserId == DocumentationFixtureSeeder.UserId).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("other@example.test", await db.Users.Where(value => value.Id == otherUser.Id).Select(value => value.Email).SingleAsync(TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"documentation-fixture-{Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
