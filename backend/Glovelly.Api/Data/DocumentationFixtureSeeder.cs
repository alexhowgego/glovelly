using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

public static class DocumentationFixtureSeeder
{
    public static readonly Guid UserId = Guid.Parse("d1111111-1111-4111-8111-111111111111");
    public static readonly Guid ClientId = Guid.Parse("d2222222-2222-4222-8222-222222222222");
    public static readonly Guid GigId = Guid.Parse("d3333333-3333-4333-8333-333333333333");
    public static readonly Guid InvoiceId = Guid.Parse("d4444444-4444-4444-8444-444444444444");
    public const string GoogleSubject = "glovelly-documentation-user";
    public const string Email = "docs@glovelly.net";
    public const string DisplayName = "Glovelly Docs";

    private static readonly DateTimeOffset SeededAt = new(2026, 4, 6, 9, 0, 0, TimeSpan.Zero);

    public static async Task ResetAndSeedAsync(
        AppDbContext db,
        IExpenseAttachmentStore attachmentStore,
        IInvoicePdfService invoicePdfService,
        CancellationToken cancellationToken = default)
    {
        await ResetAsync(db, attachmentStore, invoicePdfService, cancellationToken);

        var user = await EnsureUserAsync(db, cancellationToken);
        var client = new Client
        {
            Id = ClientId,
            Name = "The Lantern Quartet",
            Email = "accounts@lanternquartet.example",
            CreatedByUserId = UserId,
            UpdatedByUserId = UserId,
            BillingAddress = new Address
            {
                Line1 = "12 Chapel Street",
                City = "Leeds",
                StateOrCounty = "West Yorkshire",
                PostalCode = "LS1 2AB",
                Country = "United Kingdom",
            },
        };
        var gig = new Gig
        {
            Id = GigId,
            ClientId = ClientId,
            CreatedByUserId = UserId,
            UpdatedByUserId = UserId,
            Title = "Spring concert",
            Date = new DateOnly(2026, 4, 18),
            Venue = "The Lantern, Leeds",
            Fee = 350m,
            WasDriving = true,
            TravelMiles = 24m,
            PassengerCount = 1,
            Status = GigStatus.Confirmed,
            Expenses =
            [
                new GigExpense
                {
                    Id = Guid.Parse("d5555555-5555-4555-8555-555555555555"),
                    Description = "Travel and parking",
                    Amount = 48.20m,
                    SortOrder = 0,
                },
            ],
        };
        var invoice = new Invoice
        {
            Id = InvoiceId,
            InvoiceNumber = "GLV-202604-001",
            ClientId = ClientId,
            CreatedByUserId = UserId,
            UpdatedByUserId = UserId,
            InvoiceDate = new DateOnly(2026, 4, 18),
            DueDate = new DateOnly(2026, 5, 2),
            Status = InvoiceStatus.Draft,
            Description = "Spring concert",
            Lines =
            [
                new InvoiceLine
                {
                    Id = Guid.Parse("d6666666-6666-4666-8666-666666666666"),
                    Description = "Spring concert performance fee",
                    Quantity = 1,
                    UnitPrice = 350m,
                    Type = InvoiceLineType.PerformanceFee,
                    GigId = GigId,
                },
            ],
        };
        var sellerProfile = new SellerProfile
        {
            Id = Guid.Parse("d7777777-7777-4777-8777-777777777777"),
            UserId = UserId,
            SellerName = "Glovelly Docs Music",
            Email = Email,
            AccountName = "Glovelly Docs Music",
            SortCode = "00-00-00",
            AccountNumber = "00000000",
            Address = new Address
            {
                Line1 = "1 Guide Lane",
                City = "Bristol",
                StateOrCounty = "Bristol",
                PostalCode = "BS1 5AA",
                Country = "United Kingdom",
            },
            CreatedUtc = SeededAt,
            UpdatedUtc = SeededAt,
            CreatedByUserId = UserId,
            UpdatedByUserId = UserId,
        };

        db.Clients.Add(client);
        db.Gigs.Add(gig);
        db.Invoices.Add(invoice);
        db.SellerProfiles.Add(sellerProfile);
        await db.SaveChangesAsync(cancellationToken);

        gig.InvoiceId = invoice.Id;
        gig.InvoicedAt = SeededAt;
        await invoicePdfService.SaveGeneratedPdfAsync(
            invoice,
            UserId,
            "%PDF-1.1\n1 0 obj<</Type/Catalog>>endobj\n%%EOF\n"u8.ToArray(),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task ResetAsync(
        AppDbContext db,
        IExpenseAttachmentStore attachmentStore,
        IInvoicePdfService invoicePdfService,
        CancellationToken cancellationToken = default)
    {
        await UserFixtureReset.ResetAsync(db, UserId, attachmentStore, invoicePdfService, cancellationToken);

        var user = await db.Users.SingleOrDefaultAsync(value => value.Id == UserId, cancellationToken);
        if (user is not null)
        {
            ApplyUserDefaults(user);
        }

    }

    private static async Task<User> EnsureUserAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(value => value.Id == UserId, cancellationToken);
        if (user is null)
        {
            user = new User { Id = UserId, CreatedUtc = SeededAt.UtcDateTime };
            db.Users.Add(user);
        }

        ApplyUserDefaults(user);
        return user;
    }

    private static void ApplyUserDefaults(User user)
    {
        user.GoogleSubject = GoogleSubject;
        user.Email = Email;
        user.DisplayName = DisplayName;
        user.MileageRate = 0.45m;
        user.PassengerMileageRate = 0.05m;
        user.TravelOriginPostcode = "BS1 5AA";
        user.DefaultPaymentWindowDays = 14;
        user.InvoiceFilenamePattern = "{invoiceNumber}-{clientName}";
        user.InvoiceEmailSubjectPattern = "Invoice {invoiceNumber} for {clientName}";
        user.InvoiceEmailBodyTemplate = "Hello {clientName},\n\nPlease find your invoice attached.";
        user.InvoiceReplyToEmail = Email;
        user.Role = UserRole.User;
        user.IsActive = true;
        user.LastLoginUtc = null;
    }
}
