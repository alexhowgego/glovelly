using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoiceLineGenerationServiceTests
{
    [Fact]
    public async Task BuildGeneratedInvoiceLinesForGigAsync_UsesConfiguredRatesWhenClientAndUserRatesAreMissing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"invoice-line-generation-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var clientId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var gigId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = TestAuthContext.UserId,
            GoogleSubject = TestAuthContext.DefaultSubject,
            Email = TestAuthContext.DefaultEmail,
            Role = UserRole.User,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
        });
        dbContext.Clients.Add(new Client
        {
            Id = clientId,
            Name = "Config Rate Client",
            Email = "client@example.com",
            CreatedByUserId = TestAuthContext.UserId,
        });
        dbContext.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            ClientId = clientId,
            InvoiceNumber = "GLV-CONFIG-001",
            InvoiceDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 6, 15),
            CreatedByUserId = TestAuthContext.UserId,
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var gig = new Gig
        {
            Id = gigId,
            ClientId = clientId,
            InvoiceId = invoiceId,
            Title = "Configured rates booking",
            Date = new DateOnly(2026, 6, 2),
            Venue = "Town Hall",
            Fee = 0m,
            TravelMiles = 10m,
            PassengerCount = 2,
            WasDriving = true,
            Expenses = [],
        };
        var service = new InvoiceLineGenerationService(
            dbContext,
            Options.Create(new InvoiceRateSettings
            {
                DefaultMileageRate = 0.60m,
                DefaultPassengerMileageRate = 0.20m,
            }));

        var lines = await service.BuildGeneratedInvoiceLinesForGigAsync(gig, TestAuthContext.UserId, TestContext.Current.CancellationToken);

        Assert.Equal(2, lines.Count);
        Assert.Equal(InvoiceLineType.Mileage, lines[0].Type);
        Assert.Equal(10m, lines[0].Quantity);
        Assert.Equal(0.60m, lines[0].UnitPrice);
        Assert.Equal(InvoiceLineType.PassengerMileage, lines[1].Type);
        Assert.Equal(20m, lines[1].Quantity);
        Assert.Equal(0.20m, lines[1].UnitPrice);
    }
}
