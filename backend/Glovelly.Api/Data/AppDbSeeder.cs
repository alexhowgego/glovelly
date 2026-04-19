using Glovelly.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Glovelly.Api.Data;

public static class AppDbSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, IConfiguration configuration)
    {
        await dbContext.Database.EnsureCreatedAsync();

        var seededAdminUserId = await SeedDevelopmentAdminUserAsync(dbContext, configuration);

        if (await dbContext.Clients.AnyAsync())
        {
            return;
        }

        var foxAndFinchId = Guid.Parse("4a034228-78db-4db3-a132-41c97bb4d5cf");
        var northlightId = Guid.Parse("44b70ccb-d718-48c2-9703-8f965983e694");
        var riversideId = Guid.Parse("0b1f8afb-8979-4eef-a5b9-08b624b1b706");

        var clients = new[]
        {
            new Client
            {
                Id = foxAndFinchId,
                Name = "Fox & Finch Events",
                Email = "bookings@foxandfinch.co.uk",
                MileageRate = 0.45m,
                PassengerMileageRate = 0.10m,
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId,
                BillingAddress = new Address
                {
                    Line1 = "12 Chapel Street",
                    City = "Manchester",
                    StateOrCounty = "Greater Manchester",
                    PostalCode = "M3 5JZ",
                    Country = "United Kingdom"
                }
            },
            new Client
            {
                Id = northlightId,
                Name = "Northlight Weddings",
                Email = "accounts@northlightweddings.com",
                MileageRate = 0.45m,
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId,
                BillingAddress = new Address
                {
                    Line1 = "7 Hawthorn Mews",
                    City = "Leeds",
                    StateOrCounty = "West Yorkshire",
                    PostalCode = "LS1 4PR",
                    Country = "United Kingdom"
                }
            },
            new Client
            {
                Id = riversideId,
                Name = "Riverside Arts Centre",
                Email = "finance@riversidearts.org",
                PassengerMileageRate = 0.08m,
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId,
                BillingAddress = new Address
                {
                    Line1 = "84 Mill Lane",
                    Line2 = "Studio 3",
                    City = "Bristol",
                    StateOrCounty = "Bristol",
                    PostalCode = "BS1 6QX",
                    Country = "United Kingdom"
                }
            }
        };

        var invoices = new[]
        {
            new Invoice
            {
                Id = Guid.Parse("e7de8340-196d-4e37-b80e-2234e523ad87"),
                InvoiceNumber = "GLV-2026-001",
                ClientId = foxAndFinchId,
                InvoiceDate = new DateOnly(2026, 4, 1),
                DueDate = new DateOnly(2026, 4, 15),
                Status = InvoiceStatus.Issued,
                Description = "In respect of services provided during April 2026."
            },
            new Invoice
            {
                Id = Guid.Parse("e62bf1d1-fb44-484c-b869-720ddf447e1a"),
                InvoiceNumber = "GLV-2026-002",
                ClientId = riversideId,
                InvoiceDate = new DateOnly(2026, 4, 8),
                DueDate = new DateOnly(2026, 4, 22),
                Status = InvoiceStatus.Draft,
                Description = "Community residency weekend."
            }
        };

        var invoiceLines = new[]
        {
            new InvoiceLine
            {
                Id = Guid.Parse("20eb2ba9-7e46-44bb-90c2-0f8fe3e70654"),
                InvoiceId = invoices[0].Id,
                SortOrder = 1,
                Type = InvoiceLineType.PerformanceFee,
                Description = "Performance fee",
                Quantity = 1m,
                UnitPrice = 650m,
                GigId = Guid.Parse("387e596f-a262-4e3e-b224-493a46daf7a1")
            },
            new InvoiceLine
            {
                Id = Guid.Parse("272dcfb3-a93d-428f-85ad-c6a34ed10879"),
                InvoiceId = invoices[0].Id,
                SortOrder = 2,
                Type = InvoiceLineType.Mileage,
                Description = "Travel contribution",
                Quantity = 1m,
                UnitPrice = 95m,
                CalculationNotes = "Flat contribution agreed for Manchester travel."
            },
            new InvoiceLine
            {
                Id = Guid.Parse("dc494fd6-fd8e-4a8f-a770-b318af98f40d"),
                InvoiceId = invoices[1].Id,
                SortOrder = 1,
                Type = InvoiceLineType.PerformanceFee,
                Description = "Workshop session",
                Quantity = 2m,
                UnitPrice = 180m,
                GigId = Guid.Parse("51a47dea-4758-4d92-92bf-2be38a0af476")
            }
        };

        var gigs = new[]
        {
            new Gig
            {
                Id = Guid.Parse("387e596f-a262-4e3e-b224-493a46daf7a1"),
                ClientId = foxAndFinchId,
                InvoiceId = invoices[0].Id,
                Title = "Spring Product Launch",
                Date = new DateOnly(2026, 4, 18),
                Venue = "Albert Hall, Manchester",
                Fee = 650m,
                TravelMiles = 24m,
                PassengerCount = 1,
                Notes = "Evening set with acoustic opener.",
                WasDriving = true,
                Status = GigStatus.Confirmed,
                InvoicedAt = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                Expenses =
                [
                    new GigExpense
                    {
                        Id = Guid.Parse("2d7c95d8-d8a0-4ef2-a13a-7d9cb7065c1d"),
                        SortOrder = 1,
                        Description = "Parking",
                        Amount = 18m
                    }
                ]
            },
            new Gig
            {
                Id = Guid.Parse("58dc078d-bf9d-4dfb-b515-c9ec9cb2d75b"),
                ClientId = northlightId,
                Title = "Lakeside Wedding Reception",
                Date = new DateOnly(2026, 5, 2),
                Venue = "The Glasshouse, Windermere",
                Fee = 920m,
                TravelMiles = 78m,
                Notes = "Ceremony plus evening set.",
                WasDriving = true,
                Status = GigStatus.Confirmed
            },
            new Gig
            {
                Id = Guid.Parse("51a47dea-4758-4d92-92bf-2be38a0af476"),
                ClientId = riversideId,
                InvoiceId = invoices[1].Id,
                Title = "Community Residency Weekend",
                Date = new DateOnly(2026, 5, 11),
                Venue = "Riverside Arts Centre",
                Fee = 360m,
                TravelMiles = 12m,
                Notes = "Two workshops and one closing performance.",
                WasDriving = false,
                Status = GigStatus.Draft,
                InvoicedAt = new DateTimeOffset(2026, 4, 8, 10, 30, 0, TimeSpan.Zero)
            }
        };

        dbContext.Clients.AddRange(clients);
        dbContext.Invoices.AddRange(invoices);
        dbContext.InvoiceLines.AddRange(invoiceLines);
        dbContext.Gigs.AddRange(gigs);

        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid?> SeedDevelopmentAdminUserAsync(AppDbContext dbContext, IConfiguration configuration)
    {
        var googleSubject = configuration["DevelopmentSeeding:AdminGoogleSubject"]?.Trim();
        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            return null;
        }

        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.GoogleSubject == googleSubject);

        if (existingUser is not null)
        {
            if (!existingUser.IsActive || existingUser.Role != UserRole.Admin)
            {
                existingUser.IsActive = true;
                existingUser.Role = UserRole.Admin;
                await dbContext.SaveChangesAsync();
            }

            return existingUser.Id;
        }

        var email = configuration["DevelopmentSeeding:AdminEmail"]?.Trim();
        var displayName = configuration["DevelopmentSeeding:AdminDisplayName"]?.Trim();

        var seededUser = new User
        {
            Id = Guid.NewGuid(),
            GoogleSubject = googleSubject,
            Email = string.IsNullOrWhiteSpace(email) ? "local-admin@glovelly.local" : email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
            MileageRate = 0.45m,
            PassengerMileageRate = 0.10m,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
        };

        dbContext.Users.Add(seededUser);

        await dbContext.SaveChangesAsync();

        return seededUser.Id;
    }
}
