using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Models;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class InvoiceLineEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public InvoiceLineEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateInvoiceLine_WithGigLink_ReturnsDerivedLineTotalAndInvoiceTotal()
    {
        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Invoice line source gig",
            date = "2026-06-10",
            venue = "Assembly Rooms",
            fee = 420.00m,
            travelMiles = 16.00m,
            notes = "Used for invoice line linking",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Object, createdGig.ValueKind);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.FoxInvoiceId,
            sortOrder = 10,
            type = InvoiceLineType.PerformanceFee,
            description = "  Headline performance fee  ",
            quantity = 2.5m,
            unitPrice = 120.00m,
            gigId,
            calculationNotes = "  Snapshot from agreed booking rate.  ",
        });

        Assert.Equal(HttpStatusCode.Created, createLineResponse.StatusCode);

        var createdLine = await createLineResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, createdLine.ValueKind);
        Assert.Equal("Headline performance fee", createdLine.GetProperty("description").GetString());
        Assert.Equal("Snapshot from agreed booking rate.", createdLine.GetProperty("calculationNotes").GetString());
        Assert.Equal("PerformanceFee", createdLine.GetProperty("type").GetString());
        Assert.Equal(gigId, createdLine.GetProperty("gigId").GetGuid());
        Assert.Equal(300.00m, createdLine.GetProperty("lineTotal").GetDecimal());

        var invoiceResponse = await _client.GetAsync($"/invoices/{TestData.FoxInvoiceId}");
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, invoice.ValueKind);
        Assert.Equal(300.00m, invoice.GetProperty("total").GetDecimal());
        Assert.Single(invoice.GetProperty("lines").EnumerateArray());
    }

    [Fact]
    public async Task CreateInvoiceLine_WithGigForDifferentClient_ReturnsValidationProblem()
    {
        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.RiversideId,
            title = "Cross-client gig",
            date = "2026-06-11",
            venue = "Riverside Hall",
            fee = 180.00m,
            travelMiles = 4.00m,
            notes = "Should not attach to fox invoice",
            wasDriving = false,
            status = 0,
            invoicedAt = (string?)null,
        });

        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Object, createdGig.ValueKind);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var createLineResponse = await _client.PostAsJsonAsync("/invoice-lines", new
        {
            invoiceId = TestData.FoxInvoiceId,
            sortOrder = 1,
            type = InvoiceLineType.MiscExpense,
            description = "Cross-client expense",
            quantity = 1m,
            unitPrice = 25.00m,
            gigId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, createLineResponse.StatusCode);

        var problem = await createLineResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, problem.ValueKind);
        Assert.Equal("Gig client must match the invoice client.", problem.GetProperty("errors").GetProperty("gigId")[0].GetString());
    }
}
