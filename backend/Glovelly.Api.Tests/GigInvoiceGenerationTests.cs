using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GigInvoiceGenerationTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public GigInvoiceGenerationTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateInvoice_FromGig_CreatesInvoiceLinesPdfAndGigLink()
    {
        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "One-off corporate booking",
            date = "2026-06-20",
            venue = "King's House",
            fee = 500.00m,
            travelMiles = 15.5m,
            passengerCount = 1,
            notes = "Invoice generation flow",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var generateResponse = await _client.PostAsync($"/gigs/{gigId}/generate-invoice", content: null);

        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);

        var invoice = await generateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal("Draft", invoice.GetProperty("status").GetString());
        Assert.Equal(TestData.FoxAndFinchId, invoice.GetProperty("clientId").GetGuid());
        Assert.Equal("In respect of One-off corporate booking at King's House on 2026-06-20.", invoice.GetProperty("description").GetString());
        Assert.False(string.IsNullOrWhiteSpace(invoice.GetProperty("invoiceNumber").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(invoice.GetProperty("pdfBlob").GetString()));

        var lines = invoice.GetProperty("lines").EnumerateArray().OrderBy(line => line.GetProperty("sortOrder").GetInt32()).ToArray();
        Assert.Equal(3, lines.Length);

        Assert.Equal("PerformanceFee", lines[0].GetProperty("type").GetString());
        Assert.Equal(500.00m, lines[0].GetProperty("lineTotal").GetDecimal());
        Assert.Equal("Mileage", lines[1].GetProperty("type").GetString());
        Assert.Equal(15.5m, lines[1].GetProperty("quantity").GetDecimal());
        Assert.Equal("PassengerMileage", lines[2].GetProperty("type").GetString());
        Assert.Equal(15.5m, lines[2].GetProperty("quantity").GetDecimal());
        Assert.Equal(510.385m, invoice.GetProperty("total").GetDecimal());

        var refreshedGigResponse = await _client.GetAsync($"/gigs/{gigId}");
        refreshedGigResponse.EnsureSuccessStatusCode();

        var refreshedGig = await refreshedGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(invoice.GetProperty("id").GetGuid(), refreshedGig.GetProperty("invoiceId").GetGuid());
        Assert.True(refreshedGig.GetProperty("isInvoiced").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(refreshedGig.GetProperty("invoicedAt").GetString()));

        var pdfResponse = await _client.GetAsync($"/invoices/{invoice.GetProperty("id").GetGuid()}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        Assert.True((await pdfResponse.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task GenerateInvoice_FromGig_WhenAlreadyInvoiced_ReturnsConflict()
    {
        var createGigResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Already linked booking",
            date = "2026-06-21",
            venue = "Town Hall",
            fee = 280.00m,
            travelMiles = 0m,
            notes = "First invoice should win",
            wasDriving = false,
            status = 1,
            invoicedAt = (string?)null,
        });

        createGigResponse.EnsureSuccessStatusCode();

        var createdGig = await createGigResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var firstResponse = await _client.PostAsync($"/gigs/{gigId}/generate-invoice", content: null);
        firstResponse.EnsureSuccessStatusCode();

        var duplicateResponse = await _client.PostAsync($"/gigs/{gigId}/generate-invoice", content: null);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var conflict = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("This gig has already been invoiced.", conflict.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.String, conflict.GetProperty("invoiceId").ValueKind);
    }
}
