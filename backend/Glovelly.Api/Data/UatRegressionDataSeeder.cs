using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

public sealed record UatRegressionSeedContext(AppDbContext DbContext);

public sealed record UatRegressionSeedFixture(User User, Client Client, SellerProfile SellerProfile);

public static class UatRegressionDataSeeder
{
    public static readonly Guid UserId = Guid.Parse("a1111111-1111-4111-8111-111111111111");
    public static readonly Guid ClientId = Guid.Parse("a2222222-2222-4222-8222-222222222222");
    public static readonly Guid SellerProfileId = Guid.Parse("a3333333-3333-4333-8333-333333333333");
    public const string GoogleSubject = "glovelly-uat-regression-user";
    public const string Email = "regression@glovelly.net";
    public const string DisplayName = "Glovelly UAT Regression User";
    private static readonly DateTimeOffset SeededCreatedUtc = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);

    public static async Task SeedAsync(AppDbContext dbContext)
    {
        await SeedAsync(new UatRegressionSeedContext(dbContext));
    }

    public static async Task SeedAsync(UatRegressionSeedContext context)
    {
        await context.DbContext.Database.EnsureCreatedAsync();

        var fixture = BuildFixture();
        await UpsertFixtureAsync(context, fixture);
    }

    private static UatRegressionSeedFixture BuildFixture()
    {
        return new UatRegressionSeedFixture(
            new User
            {
                Id = UserId,
                GoogleSubject = GoogleSubject,
                Email = Email,
                DisplayName = DisplayName,
                MileageRate = 0.45m,
                PassengerMileageRate = 0.10m,
                TravelOriginPostcode = "BS1 5AA",
                DefaultPaymentWindowDays = 14,
                InvoiceFilenamePattern = "{invoiceNumber}-{clientName}-{periodDate}",
                InvoiceEmailSubjectPattern = "Invoice {invoiceNumber} for {clientName}",
                InvoiceReplyToEmail = Email,
                Role = UserRole.User,
                IsActive = true,
                CreatedUtc = SeededCreatedUtc.UtcDateTime,
            },
            new Client
            {
                Id = ClientId,
                Name = "UAT Regression Client",
                Email = "accounts+uat@glovelly.net",
                MileageRate = 0.45m,
                PassengerMileageRate = 0.10m,
                InvoiceFilenamePattern = "{invoiceNumber}-{clientName}",
                InvoiceEmailSubjectPattern = "UAT invoice {invoiceNumber}",
                CreatedByUserId = UserId,
                UpdatedByUserId = UserId,
                BillingAddress = new Address
                {
                    Line1 = "1 Regression Yard",
                    City = "Bristol",
                    StateOrCounty = "Bristol",
                    PostalCode = "BS1 5AA",
                    Country = "United Kingdom"
                },
            },
            new SellerProfile
            {
                Id = SellerProfileId,
                UserId = UserId,
                SellerName = "Glovelly UAT Music",
                Email = Email,
                Phone = "07123 000000",
                AccountName = "Glovelly UAT Music",
                SortCode = "00-00-00",
                AccountNumber = "00000000",
                PaymentReferenceNote = "UAT-only invoice payment reference.",
                Address = new Address
                {
                    Line1 = "1 Regression Yard",
                    City = "Bristol",
                    StateOrCounty = "Bristol",
                    PostalCode = "BS1 5AA",
                    Country = "United Kingdom"
                },
                CreatedUtc = SeededCreatedUtc,
                UpdatedUtc = SeededCreatedUtc,
                CreatedByUserId = UserId,
                UpdatedByUserId = UserId,
            });
    }

    private static async Task UpsertFixtureAsync(
        UatRegressionSeedContext context,
        UatRegressionSeedFixture fixture)
    {
        var dbContext = context.DbContext;
        var user = await dbContext.Users.FirstOrDefaultAsync(value => value.Id == UserId);
        if (user is null)
        {
            user = fixture.User;
            dbContext.Users.Add(user);
        }
        else
        {
            ApplyUserFixture(user, fixture.User);
        }

        var client = await dbContext.Clients.FirstOrDefaultAsync(value => value.Id == ClientId);
        if (client is null)
        {
            client = fixture.Client;
            dbContext.Clients.Add(client);
        }
        else
        {
            ApplyClientFixture(client, fixture.Client);
        }

        var sellerProfile = await dbContext.SellerProfiles
            .FirstOrDefaultAsync(value => value.UserId == UserId);
        if (sellerProfile is null)
        {
            sellerProfile = fixture.SellerProfile;
            dbContext.SellerProfiles.Add(sellerProfile);
        }
        else
        {
            ApplySellerProfileFixture(sellerProfile, fixture.SellerProfile);
        }

        await dbContext.SaveChangesAsync();
    }

    private static void ApplyUserFixture(User target, User fixture)
    {
        target.GoogleSubject = fixture.GoogleSubject;
        target.Email = fixture.Email;
        target.DisplayName = fixture.DisplayName;
        target.MileageRate = fixture.MileageRate;
        target.PassengerMileageRate = fixture.PassengerMileageRate;
        target.TravelOriginPostcode = fixture.TravelOriginPostcode;
        target.DefaultPaymentWindowDays = fixture.DefaultPaymentWindowDays;
        target.InvoiceFilenamePattern = fixture.InvoiceFilenamePattern;
        target.InvoiceEmailSubjectPattern = fixture.InvoiceEmailSubjectPattern;
        target.InvoiceReplyToEmail = fixture.InvoiceReplyToEmail;
        target.Role = fixture.Role;
        target.IsActive = fixture.IsActive;
    }

    private static void ApplyClientFixture(Client target, Client fixture)
    {
        target.Name = fixture.Name;
        target.Email = fixture.Email;
        target.MileageRate = fixture.MileageRate;
        target.PassengerMileageRate = fixture.PassengerMileageRate;
        target.InvoiceFilenamePattern = fixture.InvoiceFilenamePattern;
        target.InvoiceEmailSubjectPattern = fixture.InvoiceEmailSubjectPattern;
        target.UpdatedByUserId = fixture.UpdatedByUserId;
        target.BillingAddress = fixture.BillingAddress;
    }

    private static void ApplySellerProfileFixture(SellerProfile target, SellerProfile fixture)
    {
        target.SellerName = fixture.SellerName;
        target.Email = fixture.Email;
        target.Phone = fixture.Phone;
        target.AccountName = fixture.AccountName;
        target.SortCode = fixture.SortCode;
        target.AccountNumber = fixture.AccountNumber;
        target.PaymentReferenceNote = fixture.PaymentReferenceNote;
        target.Address = fixture.Address;
        target.UpdatedUtc = fixture.UpdatedUtc;
        target.UpdatedByUserId = fixture.UpdatedByUserId;
    }
}
