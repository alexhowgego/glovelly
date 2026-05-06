using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace Glovelly.Api.Tests.Infrastructure;

public sealed class GlovellyApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"glovelly-tests-{Guid.NewGuid()}";
    private readonly object _databaseResetLock = new();
    private readonly FakeEmailSender _fakeEmailSender = new();

    internal FakeEmailSender Emails => _fakeEmailSender;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IPolicyEvaluator>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IExpenseAttachmentStore>();

            services.AddSingleton<IPolicyEvaluator, TestPolicyEvaluator>();
            services.AddSingleton<IEmailSender>(_fakeEmailSender);
            services.AddSingleton<IExpenseAttachmentStore, InMemoryExpenseAttachmentStore>();
            services.PostConfigure<EmailSettings>(settings =>
            {
                settings.AccessRequests.FromAddress = "access@glovelly.test";
                settings.AccessRequests.FromDisplayName = "Glovelly Access";
                settings.Invoices.FromAddress = "invoices@glovelly.test";
                settings.Invoices.FromDisplayName = "Glovelly Invoices";
            });

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            ResetDatabase(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        });
    }

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();

        lock (_databaseResetLock)
        {
            using var scope = Services.CreateScope();
            ResetDatabase(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            _fakeEmailSender.SentEmails.Clear();
            _fakeEmailSender.ExceptionToThrow = null;
        }

        return client;
    }

    private static void ResetDatabase(AppDbContext dbContext)
    {
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        SeedTestData(dbContext);
    }

    private static void SeedTestData(AppDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            Id = TestAuthContext.UserId,
            Email = "test-admin@glovelly.local",
            DisplayName = "Test Admin",
            MileageRate = 0.45m,
            PassengerMileageRate = 0.10m,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        var clients = new[]
        {
            new Client
            {
                Id = TestData.FoxAndFinchId,
                Name = "Fox & Finch Events",
                Email = "bookings@foxandfinch.co.uk",
                MileageRate = 0.52m,
                PassengerMileageRate = 0.15m,
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
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
                Id = TestData.RiversideId,
                Name = "Riverside Arts Centre",
                Email = "finance@riversidearts.org",
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
                BillingAddress = new Address
                {
                    Line1 = "84 Mill Lane",
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
                Id = TestData.FoxInvoiceId,
                InvoiceNumber = "GLV-TEST-001",
                ClientId = TestData.FoxAndFinchId,
                InvoiceDate = new DateOnly(2026, 4, 1),
                DueDate = new DateOnly(2026, 4, 15),
                Status = InvoiceStatus.Issued,
                FirstIssuedUtc = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                FirstIssuedByUserId = TestAuthContext.UserId,
                Description = "Fox & Finch April services.",
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
            },
            new Invoice
            {
                Id = TestData.RiversideInvoiceId,
                InvoiceNumber = "GLV-TEST-002",
                ClientId = TestData.RiversideId,
                InvoiceDate = new DateOnly(2026, 4, 8),
                DueDate = new DateOnly(2026, 4, 22),
                Status = InvoiceStatus.Draft,
                Description = "Riverside residency draft.",
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
            }
        };

        dbContext.Clients.AddRange(clients);
        dbContext.Invoices.AddRange(invoices);
        dbContext.SaveChanges();
    }
}
