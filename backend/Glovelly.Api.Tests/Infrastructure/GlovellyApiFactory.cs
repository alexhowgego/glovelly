using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Glovelly.Api.Tests.Infrastructure;

public sealed class GlovellyApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"glovelly-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IPolicyEvaluator>();

            services.AddSingleton<IPolicyEvaluator, TestPolicyEvaluator>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
            SeedTestData(dbContext);
        });
    }

    private static void SeedTestData(AppDbContext dbContext)
    {
        var clients = new[]
        {
            new Client
            {
                Id = TestData.FoxAndFinchId,
                Name = "Fox & Finch Events",
                Email = "bookings@foxandfinch.co.uk",
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
                IssueDate = new DateOnly(2026, 4, 1),
                DueDate = new DateOnly(2026, 4, 15),
                Status = InvoiceStatus.Issued,
                Subtotal = 650m,
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
            },
            new Invoice
            {
                Id = TestData.RiversideInvoiceId,
                InvoiceNumber = "GLV-TEST-002",
                ClientId = TestData.RiversideId,
                IssueDate = new DateOnly(2026, 4, 8),
                DueDate = new DateOnly(2026, 4, 22),
                Status = InvoiceStatus.Draft,
                Subtotal = 360m,
                CreatedByUserId = TestAuthContext.UserId,
                UpdatedByUserId = TestAuthContext.UserId,
            }
        };

        dbContext.Clients.AddRange(clients);
        dbContext.Invoices.AddRange(invoices);
        dbContext.SaveChanges();
    }
}
