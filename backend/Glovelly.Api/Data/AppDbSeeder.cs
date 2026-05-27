using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Glovelly.Api.Data;

public static class AppDbSeeder
{
    public static readonly Guid UatRegressionUserId = Guid.Parse("a1111111-1111-4111-8111-111111111111");
    public static readonly Guid UatRegressionClientId = Guid.Parse("a2222222-2222-4222-8222-222222222222");
    public static readonly Guid UatRegressionSellerProfileId = Guid.Parse("a3333333-3333-4333-8333-333333333333");
    public const string UatRegressionGoogleSubject = "glovelly-uat-regression-user";
    public const string UatRegressionEmail = "regression@glovelly.net";
    public const string UatRegressionDisplayName = "Glovelly UAT Regression User";
    private static readonly DateTimeOffset SeededCreatedUtc = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly byte[] SeededPdfContent = "Seeded development invoice PDF placeholder"u8.ToArray();

    public static async Task SeedUatRegressionDataAsync(AppDbContext dbContext)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(value => value.Id == UatRegressionUserId);
        if (user is null)
        {
            user = new User
            {
                Id = UatRegressionUserId,
                CreatedUtc = SeededCreatedUtc.UtcDateTime,
            };
            dbContext.Users.Add(user);
        }

        user.GoogleSubject = UatRegressionGoogleSubject;
        user.Email = UatRegressionEmail;
        user.DisplayName = UatRegressionDisplayName;
        user.MileageRate = 0.45m;
        user.PassengerMileageRate = 0.10m;
        user.TravelOriginPostcode = "BS1 5AA";
        user.DefaultPaymentWindowDays = 14;
        user.InvoiceFilenamePattern = "{invoiceNumber}-{clientName}-{periodDate}";
        user.InvoiceEmailSubjectPattern = "Invoice {invoiceNumber} for {clientName}";
        user.InvoiceReplyToEmail = UatRegressionEmail;
        user.Role = UserRole.User;
        user.IsActive = true;

        var client = await dbContext.Clients.FirstOrDefaultAsync(value => value.Id == UatRegressionClientId);
        if (client is null)
        {
            client = new Client
            {
                Id = UatRegressionClientId,
                CreatedByUserId = UatRegressionUserId,
            };
            dbContext.Clients.Add(client);
        }

        client.Name = "UAT Regression Client";
        client.Email = "accounts+uat@glovelly.net";
        client.MileageRate = 0.45m;
        client.PassengerMileageRate = 0.10m;
        client.InvoiceFilenamePattern = "{invoiceNumber}-{clientName}";
        client.InvoiceEmailSubjectPattern = "UAT invoice {invoiceNumber}";
        client.UpdatedByUserId = UatRegressionUserId;
        client.BillingAddress = new Address
        {
            Line1 = "1 Regression Yard",
            City = "Bristol",
            StateOrCounty = "Bristol",
            PostalCode = "BS1 5AA",
            Country = "United Kingdom"
        };

        var sellerProfile = await dbContext.SellerProfiles
            .FirstOrDefaultAsync(value => value.UserId == UatRegressionUserId);
        if (sellerProfile is null)
        {
            sellerProfile = new SellerProfile
            {
                Id = UatRegressionSellerProfileId,
                UserId = UatRegressionUserId,
                CreatedUtc = SeededCreatedUtc,
                CreatedByUserId = UatRegressionUserId,
            };
            dbContext.SellerProfiles.Add(sellerProfile);
        }

        sellerProfile.SellerName = "Glovelly UAT Music";
        sellerProfile.Email = UatRegressionEmail;
        sellerProfile.Phone = "07123 000000";
        sellerProfile.AccountName = "Glovelly UAT Music";
        sellerProfile.SortCode = "00-00-00";
        sellerProfile.AccountNumber = "00000000";
        sellerProfile.PaymentReferenceNote = "UAT-only invoice payment reference.";
        sellerProfile.Address = new Address
        {
            Line1 = "1 Regression Yard",
            City = "Bristol",
            StateOrCounty = "Bristol",
            PostalCode = "BS1 5AA",
            Country = "United Kingdom"
        };
        sellerProfile.UpdatedUtc = SeededCreatedUtc;
        sellerProfile.UpdatedByUserId = UatRegressionUserId;

        await dbContext.SaveChangesAsync();
    }

    public static async Task SeedAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        IExpenseAttachmentStore? attachmentStore = null,
        IBlobStore? blobStore = null)
    {
        await dbContext.Database.EnsureCreatedAsync();

        var seededAdminUserId = await SeedDevelopmentAdminUserAsync(dbContext, configuration);
        await SeedDevelopmentSellerProfileAsync(dbContext, seededAdminUserId);

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
                InvoiceFilenamePattern = "{invoiceNumber}-{clientName}-{periodDate}",
                InvoiceEmailSubjectPattern = "Invoice {invoiceNumber} for {clientName}",
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
                InvoiceFilenamePattern = "{clientName}-{invoiceNumber}",
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
                MileageRate = 0.42m,
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
                FirstIssuedUtc = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                FirstIssuedByUserId = seededAdminUserId,
                StatusUpdatedUtc = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                DeliveryCount = 1,
                LastDeliveryChannel = "Email",
                LastDeliveryRecipient = "bookings@foxandfinch.co.uk",
                LastDeliveredUtc = new DateTimeOffset(2026, 4, 1, 9, 15, 0, TimeSpan.Zero),
                LastDeliveredByUserId = seededAdminUserId,
                Description = "In respect of Spring Product Launch at Albert Hall, Manchester on 2026-04-18.",
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId
            },
            new Invoice
            {
                Id = Guid.Parse("e62bf1d1-fb44-484c-b869-720ddf447e1a"),
                InvoiceNumber = "GLV-2026-002",
                ClientId = riversideId,
                InvoiceDate = new DateOnly(2026, 4, 8),
                DueDate = new DateOnly(2026, 4, 22),
                Status = InvoiceStatus.Draft,
                Description = "In respect of Community Residency Weekend at Riverside Arts Centre on 2026-05-11.",
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId
            },
            new Invoice
            {
                Id = Guid.Parse("83c5e4e0-6749-4efe-bb15-e7a63f49f3cb"),
                InvoiceNumber = "GLV-2026-003",
                ClientId = northlightId,
                InvoiceDate = new DateOnly(2026, 4, 18),
                DueDate = new DateOnly(2026, 5, 2),
                Status = InvoiceStatus.Draft,
                Description = "Draft invoice for Lakeside Wedding Reception.",
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId
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
                Description = "Performance fee for Spring Product Launch (2026-04-18)",
                Quantity = 1m,
                UnitPrice = 650m,
                GigId = Guid.Parse("387e596f-a262-4e3e-b224-493a46daf7a1"),
                CalculationNotes = null,
                IsSystemGenerated = true,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
            },
            new InvoiceLine
            {
                Id = Guid.Parse("272dcfb3-a93d-428f-85ad-c6a34ed10879"),
                InvoiceId = invoices[0].Id,
                SortOrder = 2,
                Type = InvoiceLineType.Mileage,
                Description = "Mileage for Spring Product Launch",
                Quantity = 24m,
                UnitPrice = 0.45m,
                GigId = Guid.Parse("387e596f-a262-4e3e-b224-493a46daf7a1"),
                CalculationNotes = "24 miles at 0.45 per mile.",
                IsSystemGenerated = true,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
            },
            new InvoiceLine
            {
                Id = Guid.Parse("13a20439-2947-4e5e-932c-65de19f820a9"),
                InvoiceId = invoices[0].Id,
                SortOrder = 3,
                Type = InvoiceLineType.PassengerMileage,
                Description = "Passenger mileage for Spring Product Launch",
                Quantity = 24m,
                UnitPrice = 0.10m,
                GigId = Guid.Parse("387e596f-a262-4e3e-b224-493a46daf7a1"),
                CalculationNotes = "1 passenger x 24 miles.",
                IsSystemGenerated = true,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
            },
            new InvoiceLine
            {
                Id = Guid.Parse("7b65d5cb-4b64-44ef-8abc-b4fd22195255"),
                InvoiceId = invoices[0].Id,
                SortOrder = 4,
                Type = InvoiceLineType.MiscExpense,
                Description = "Parking",
                Quantity = 1m,
                UnitPrice = 18m,
                GigId = Guid.Parse("387e596f-a262-4e3e-b224-493a46daf7a1"),
                IsSystemGenerated = true,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
            },
            new InvoiceLine
            {
                Id = Guid.Parse("dc494fd6-fd8e-4a8f-a770-b318af98f40d"),
                InvoiceId = invoices[1].Id,
                SortOrder = 1,
                Type = InvoiceLineType.PerformanceFee,
                Description = "Performance fee for Community Residency Weekend (2026-05-11)",
                Quantity = 1m,
                UnitPrice = 360m,
                GigId = Guid.Parse("51a47dea-4758-4d92-92bf-2be38a0af476"),
                IsSystemGenerated = true,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
            },
            new InvoiceLine
            {
                Id = Guid.Parse("d7ff757b-cc41-4b86-af4b-f4a91343812b"),
                InvoiceId = invoices[1].Id,
                SortOrder = 2,
                Type = InvoiceLineType.Mileage,
                Description = "Mileage for Community Residency Weekend",
                Quantity = 12m,
                UnitPrice = 0.42m,
                GigId = Guid.Parse("51a47dea-4758-4d92-92bf-2be38a0af476"),
                CalculationNotes = "12 miles at 0.42 per mile.",
                IsSystemGenerated = true,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
            },
            new InvoiceLine
            {
                Id = Guid.Parse("7d55dd6e-ae83-462e-ac91-8fd140a6f5e9"),
                InvoiceId = invoices[1].Id,
                SortOrder = 3,
                Type = InvoiceLineType.MiscExpense,
                Description = "Workshop materials",
                Quantity = 1m,
                UnitPrice = 42m,
                GigId = Guid.Parse("51a47dea-4758-4d92-92bf-2be38a0af476"),
                IsSystemGenerated = true,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
            },
            new InvoiceLine
            {
                Id = Guid.Parse("8ae836fd-936a-4492-ae8d-4b02107c61af"),
                InvoiceId = invoices[1].Id,
                SortOrder = 4,
                Type = InvoiceLineType.ManualAdjustment,
                Description = "Manual adjustment: venue-funded outreach discount",
                Quantity = 1m,
                UnitPrice = -25m,
                CalculationNotes = "Seeded example of a user-entered invoice adjustment.",
                IsSystemGenerated = false,
                CreatedByUserId = seededAdminUserId,
                CreatedUtc = SeededCreatedUtc
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
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId,
                Expenses =
                [
                    new GigExpense
                    {
                        Id = Guid.Parse("2d7c95d8-d8a0-4ef2-a13a-7d9cb7065c1d"),
                        SortOrder = 1,
                        Description = "Parking",
                        Amount = 18m,
                        ReimbursementStatus = GigExpenseReimbursementStatus.Unreimbursed,
                        Attachments =
                        [
                            new ExpenseAttachment
                            {
                                Id = Guid.Parse("1b952a78-6533-4db4-8aae-c3e5e362d517"),
                                FileName = "spring-launch-parking.pdf",
                                ContentType = "application/pdf",
                                SizeBytes = 48219,
                                StorageKey = "development/receipts/spring-launch-parking.pdf",
                                CreatedAt = new DateTimeOffset(2026, 4, 18, 23, 5, 0, TimeSpan.Zero)
                            }
                        ]
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
                PassengerCount = 2,
                Notes = "Ceremony plus evening set.",
                WasDriving = true,
                Status = GigStatus.Confirmed,
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId,
                Expenses =
                [
                    new GigExpense
                    {
                        Id = Guid.Parse("76d060b3-b900-4e81-a230-f506051422ec"),
                        SortOrder = 1,
                        Description = "Strings",
                        Amount = 24m,
                        ReimbursementStatus = GigExpenseReimbursementStatus.NotClaimable,
                        ReimbursementUpdatedByUserId = seededAdminUserId,
                        ReimbursementUpdatedAt = new DateTimeOffset(2026, 4, 20, 14, 0, 0, TimeSpan.Zero),
                        ReimbursementNote = "Personal consumables, not billed to the client."
                    }
                ]
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
                PassengerCount = 0,
                Notes = "Two workshops and one closing performance.",
                WasDriving = true,
                Status = GigStatus.Confirmed,
                InvoicedAt = new DateTimeOffset(2026, 4, 8, 10, 30, 0, TimeSpan.Zero),
                CreatedByUserId = seededAdminUserId,
                UpdatedByUserId = seededAdminUserId,
                Expenses =
                [
                    new GigExpense
                    {
                        Id = Guid.Parse("01c57be3-5c68-4b18-9cae-5948365c264f"),
                        SortOrder = 1,
                        Description = "Workshop materials",
                        Amount = 42m,
                        ReimbursementStatus = GigExpenseReimbursementStatus.Unreimbursed,
                        Attachments =
                        [
                            new ExpenseAttachment
                            {
                                Id = Guid.Parse("f6c9e875-40a3-408f-9183-a09cb5255128"),
                                FileName = "riverside-materials.jpg",
                                ContentType = "image/jpeg",
                                SizeBytes = 301144,
                                StorageKey = "development/receipts/riverside-materials.jpg",
                                CreatedAt = new DateTimeOffset(2026, 5, 11, 18, 30, 0, TimeSpan.Zero)
                            }
                        ]
                    },
                    new GigExpense
                    {
                        Id = Guid.Parse("c980e792-6ace-4fd0-bf76-1573fb6ddb5d"),
                        SortOrder = 2,
                        Description = "Lunch",
                        Amount = 14.50m,
                        ReimbursementStatus = GigExpenseReimbursementStatus.Reimbursed,
                        ReimbursementInvoiceId = invoices[1].Id,
                        ReimbursementUpdatedByUserId = seededAdminUserId,
                        ReimbursedAt = new DateTimeOffset(2026, 4, 8, 10, 30, 0, TimeSpan.Zero),
                        ReimbursementUpdatedAt = new DateTimeOffset(2026, 4, 8, 10, 30, 0, TimeSpan.Zero),
                        ReimbursementMethod = "Invoice GLV-2026-002",
                        ReimbursementNote = "Seeded example of a reimbursed expense that should not regenerate as a chargeable invoice line."
                    }
                ]
            }
        };

        dbContext.Clients.AddRange(clients);
        dbContext.Invoices.AddRange(invoices);
        if (seededAdminUserId.HasValue)
        {
            await SeedInvoicePdfAsync(invoices[0], seededAdminUserId.Value, blobStore);
            await SeedInvoicePdfAsync(invoices[1], seededAdminUserId.Value, blobStore);
        }
        dbContext.InvoiceLines.AddRange(invoiceLines);
        dbContext.Gigs.AddRange(gigs);

        await dbContext.SaveChangesAsync();

        if (attachmentStore is not null)
        {
            await SeedExpenseAttachmentBlobsAsync(gigs, attachmentStore);
        }
    }

    private static async Task SeedInvoicePdfAsync(Invoice invoice, Guid userId, IBlobStore? blobStore)
    {
        if (blobStore is null)
        {
            return;
        }

        var key = InvoicePdfStorage.BuildStorageKey(invoice, userId);
        await blobStore.SaveAsync(new BlobWriteRequest(
            key,
            new MemoryStream(SeededPdfContent, writable: false),
            InvoicePdfStorage.ContentType,
            SeededPdfContent.Length));

        invoice.PdfStorageKey = key;
        invoice.PdfFileName = $"{invoice.InvoiceNumber}.pdf";
        invoice.PdfContentType = InvoicePdfStorage.ContentType;
        invoice.PdfSizeBytes = SeededPdfContent.Length;
        invoice.PdfGeneratedAt = invoice.FirstIssuedUtc ?? SeededCreatedUtc;
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

    private static async Task SeedDevelopmentSellerProfileAsync(AppDbContext dbContext, Guid? seededAdminUserId)
    {
        if (!seededAdminUserId.HasValue ||
            await dbContext.SellerProfiles.AnyAsync(profile => profile.UserId == seededAdminUserId.Value))
        {
            return;
        }

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.Parse("8763be83-8ff3-42ac-9711-7dc13b443e5f"),
            UserId = seededAdminUserId.Value,
            SellerName = "Glovelly Music",
            Email = "alex@glovelly.local",
            Phone = "07123 456789",
            AccountName = "Glovelly Music",
            SortCode = "12-34-56",
            AccountNumber = "12345678",
            PaymentReferenceNote = "Please use the invoice number as the payment reference.",
            Address = new Address
            {
                Line1 = "Studio 4",
                Line2 = "The Old Mill",
                City = "Bristol",
                StateOrCounty = "Bristol",
                PostalCode = "BS1 5AA",
                Country = "United Kingdom"
            },
            CreatedUtc = SeededCreatedUtc,
            UpdatedUtc = SeededCreatedUtc,
            CreatedByUserId = seededAdminUserId,
            UpdatedByUserId = seededAdminUserId
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedExpenseAttachmentBlobsAsync(
        IEnumerable<Gig> gigs,
        IExpenseAttachmentStore attachmentStore)
    {
        foreach (var attachment in gigs
                     .SelectMany(gig => gig.Expenses)
                     .SelectMany(expense => expense.Attachments))
        {
            var content = Encoding.UTF8.GetBytes($"Development receipt placeholder for {attachment.FileName}");
            await using var stream = new MemoryStream(content);
            await attachmentStore.SaveAsync(attachment.StorageKey, stream, attachment.ContentType);
        }
    }
}
