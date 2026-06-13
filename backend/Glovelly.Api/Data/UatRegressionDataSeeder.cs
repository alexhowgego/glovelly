using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Data;

public sealed record UatRegressionSeedContext(AppDbContext DbContext);

public sealed record UatRegressionSeedFixture(User User, Client Client, SellerProfile SellerProfile, Gig Gig);

public static class UatRegressionDataSeeder
{
    public static readonly Guid UserId = Guid.Parse("a1111111-1111-4111-8111-111111111111");
    public static readonly Guid ClientId = Guid.Parse("a2222222-2222-4222-8222-222222222222");
    public static readonly Guid SellerProfileId = Guid.Parse("a3333333-3333-4333-8333-333333333333");
    public static readonly Guid GigId = Guid.Parse("a4444444-4444-4444-8444-444444444444");
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
            },
            new Gig
            {
                Id = GigId,
                ClientId = ClientId,
                Title = "UAT Linked Resources Gig",
                Date = new DateOnly(2026, 6, 20),
                Venue = "Regression Hall, Bristol",
                Fee = 250m,
                TravelMiles = 6m,
                PassengerCount = 0,
                Notes = "Seeded UAT gig for linked external resource checks.",
                WasDriving = true,
                Status = GigStatus.Confirmed,
                CreatedByUserId = UserId,
                UpdatedByUserId = UserId,
                ExternalResources =
                [
                    new GigExternalResource
                    {
                        Id = Guid.Parse("a5555555-5555-4555-8555-555555555555"),
                        ResourceType = GigExternalResourceType.GoogleSheet,
                        Purpose = GigExternalResourcePurpose.SetList,
                        Title = "UAT primary set list",
                        Url = "https://docs.google.com/spreadsheets/d/uat-primary-set-list",
                        Notes = "Seeded primary set list for staging smoke checks.",
                        IsPrimary = true,
                        CreatedAt = SeededCreatedUtc,
                        UpdatedAt = SeededCreatedUtc,
                    },
                    new GigExternalResource
                    {
                        Id = Guid.Parse("a6666666-6666-4666-8666-666666666666"),
                        ResourceType = GigExternalResourceType.GoogleDoc,
                        Purpose = GigExternalResourcePurpose.GigPlan,
                        Title = "UAT gig plan",
                        Url = "https://docs.google.com/document/d/uat-gig-plan",
                        Notes = "Seeded gig plan link for staging smoke checks.",
                        IsPrimary = true,
                        CreatedAt = SeededCreatedUtc,
                        UpdatedAt = SeededCreatedUtc,
                    }
                ]
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

        var gig = await dbContext.Gigs
            .Include(value => value.ExternalResources)
            .FirstOrDefaultAsync(value => value.Id == GigId);
        if (gig is null)
        {
            gig = fixture.Gig;
            dbContext.Gigs.Add(gig);
        }
        else
        {
            ApplyGigFixture(gig, fixture.Gig);
            UpsertExternalResources(gig, fixture.Gig.ExternalResources);
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

    private static void ApplyGigFixture(Gig target, Gig fixture)
    {
        target.ClientId = fixture.ClientId;
        target.Title = fixture.Title;
        target.Date = fixture.Date;
        target.Venue = fixture.Venue;
        target.Fee = fixture.Fee;
        target.TravelMiles = fixture.TravelMiles;
        target.PassengerCount = fixture.PassengerCount;
        target.Notes = fixture.Notes;
        target.WasDriving = fixture.WasDriving;
        target.Status = fixture.Status;
        target.UpdatedByUserId = fixture.UpdatedByUserId;
    }

    private static void UpsertExternalResources(Gig target, IEnumerable<GigExternalResource> fixtures)
    {
        foreach (var fixture in fixtures)
        {
            var resource = target.ExternalResources.FirstOrDefault(value => value.Id == fixture.Id);
            if (resource is null)
            {
                target.ExternalResources.Add(fixture);
                continue;
            }

            resource.ResourceType = fixture.ResourceType;
            resource.Purpose = fixture.Purpose;
            resource.Title = fixture.Title;
            resource.Url = fixture.Url;
            resource.Notes = fixture.Notes;
            resource.IsPrimary = fixture.IsPrimary;
            resource.UpdatedAt = fixture.UpdatedAt;
        }
    }
}
