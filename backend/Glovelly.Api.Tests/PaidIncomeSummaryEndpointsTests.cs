using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class PaidIncomeSummaryEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public PaidIncomeSummaryEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(2026, 4, 6, 2026, 4, 6, 2027, 4, 5)]
    [InlineData(2027, 4, 5, 2026, 4, 6, 2027, 4, 5)]
    public void FinancialYear_UsesInclusiveSixthAprilToFifthAprilBoundaries(
        int year,
        int month,
        int day,
        int expectedStartYear,
        int expectedStartMonth,
        int expectedStartDay,
        int expectedEndYear,
        int expectedEndMonth,
        int expectedEndDay)
    {
        var period = UkFinancialYear.ForDate(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(expectedStartYear, expectedStartMonth, expectedStartDay), period.Start);
        Assert.Equal(new DateOnly(expectedEndYear, expectedEndMonth, expectedEndDay), period.End);
    }

    [Fact]
    public void FinancialYear_UsesTheEuropeLondonLocalDate()
    {
        var period = UkFinancialYear.Current(
            new FixedTimeProvider(new DateTimeOffset(2026, 4, 5, 23, 30, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 4, 6), period.Start);
        Assert.Equal(new DateOnly(2027, 4, 5), period.End);
    }

    [Fact]
    public async Task GetPaidIncomeSummary_IncludesOnlyVisiblePaidInvoicesWithinTheFinancialYear()
    {
        var includedInvoiceId = Guid.NewGuid();
        var secondIncludedInvoiceId = Guid.NewGuid();
        var excludedPaidDateInvoiceId = Guid.NewGuid();
        var excludedStatusInvoiceId = Guid.NewGuid();
        var missingPaidDateInvoiceId = Guid.NewGuid();
        var alternateUserInvoiceId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AddInvoice(db, includedInvoiceId, InvoiceStatus.Paid, new DateOnly(2025, 4, 6), 125m, TestAuthContext.UserId);
            AddInvoice(db, secondIncludedInvoiceId, InvoiceStatus.Paid, new DateOnly(2026, 4, 5), 75m, TestAuthContext.UserId);
            AddInvoice(db, excludedPaidDateInvoiceId, InvoiceStatus.Paid, new DateOnly(2025, 4, 5), 250m, TestAuthContext.UserId);
            AddInvoice(db, excludedStatusInvoiceId, InvoiceStatus.Issued, new DateOnly(2026, 1, 1), 375m, TestAuthContext.UserId);
            AddInvoice(db, missingPaidDateInvoiceId, InvoiceStatus.Paid, null, 425m, TestAuthContext.UserId);
            AddInvoice(db, alternateUserInvoiceId, InvoiceStatus.Paid, new DateOnly(2026, 1, 1), 500m, TestAuthContext.AlternateUserId);
            db.SaveChanges();
        }

        var response = await _client.GetAsync("/invoices/paid-income-summary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("2025-04-06", summary.GetProperty("financialYearStart").GetString());
        Assert.Equal("2026-04-05", summary.GetProperty("financialYearEnd").GetString());
        Assert.Equal(200m, summary.GetProperty("total").GetDecimal());

        var invoiceIds = summary.GetProperty("invoiceIds").EnumerateArray().Select(value => value.GetGuid()).ToList();
        Assert.Equal(2, invoiceIds.Count);
        Assert.Contains(includedInvoiceId, invoiceIds);
        Assert.Contains(secondIncludedInvoiceId, invoiceIds);
        Assert.Equal(125m + 75m, summary.GetProperty("total").GetDecimal());
        Assert.DoesNotContain(excludedPaidDateInvoiceId, invoiceIds);
        Assert.DoesNotContain(excludedStatusInvoiceId, invoiceIds);
        Assert.DoesNotContain(missingPaidDateInvoiceId, invoiceIds);
        Assert.DoesNotContain(alternateUserInvoiceId, invoiceIds);
    }

    [Fact]
    public async Task GetPaidIncomeSummary_IncludesPaidInvoicesWithoutLinesAsZeroIncome()
    {
        var zeroLineInvoiceId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AddInvoice(
                db,
                zeroLineInvoiceId,
                InvoiceStatus.Paid,
                new DateOnly(2026, 1, 1),
                0m,
                TestAuthContext.UserId,
                includeLine: false);
            db.SaveChanges();
        }

        var response = await _client.GetAsync("/invoices/paid-income-summary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(0m, summary.GetProperty("total").GetDecimal());
        Assert.Contains(
            zeroLineInvoiceId,
            summary.GetProperty("invoiceIds").EnumerateArray().Select(value => value.GetGuid()));
    }

    private static void AddInvoice(
        AppDbContext db,
        Guid invoiceId,
        InvoiceStatus status,
        DateOnly? paidOn,
        decimal total,
        Guid ownerId,
        bool includeLine = true)
    {
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            InvoiceNumber = $"TEST-{invoiceId:N}",
            ClientId = TestData.FoxAndFinchId,
            InvoiceDate = new DateOnly(2026, 1, 1),
            DueDate = new DateOnly(2026, 1, 15),
            Status = status,
            PaidOn = paidOn,
            CreatedByUserId = ownerId,
            UpdatedByUserId = ownerId,
            Lines = includeLine
                ?
                [
                    new InvoiceLine
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoiceId,
                        CreatedByUserId = ownerId,
                        CreatedUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        SortOrder = 1,
                        Type = InvoiceLineType.PerformanceFee,
                        Description = "Test income",
                        Quantity = 1,
                        UnitPrice = total,
                    },
                ]
                : [],
        });
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
