using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class ClientEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public ClientEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateClientSettings_WithInvoicePatterns_PersistsTrimmedPatterns()
    {
        var response = await _client.PutAsJsonAsync($"/clients/{TestData.FoxAndFinchId}", new
        {
            id = TestData.FoxAndFinchId,
            name = "Fox & Finch Events",
            email = "bookings@foxandfinch.co.uk",
            billingAddress = new
            {
                line1 = "12 Chapel Street",
                city = "Manchester",
                stateOrCounty = "Greater Manchester",
                postalCode = "M3 5JZ",
                country = "United Kingdom",
            },
            mileageRate = 0.52m,
            passengerMileageRate = 0.15m,
            invoiceFilenamePattern = "  {ClientName} Invoice {InvoiceNumber}  ",
            invoiceEmailSubjectPattern = "  {ClientName}: invoice {InvoiceNumber}  ",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var client = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "{ClientName} Invoice {InvoiceNumber}",
            client.GetProperty("invoiceFilenamePattern").GetString());
        Assert.Equal(
            "{ClientName}: invoice {InvoiceNumber}",
            client.GetProperty("invoiceEmailSubjectPattern").GetString());
    }

    [Fact]
    public async Task UpdateClientSettings_WithWhitespaceOnlyInvoiceFilenamePattern_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync($"/clients/{TestData.FoxAndFinchId}", new
        {
            id = TestData.FoxAndFinchId,
            name = "Fox & Finch Events",
            email = "bookings@foxandfinch.co.uk",
            billingAddress = new
            {
                line1 = "12 Chapel Street",
                city = "Manchester",
                stateOrCounty = "Greater Manchester",
                postalCode = "M3 5JZ",
                country = "United Kingdom",
            },
            mileageRate = 0.52m,
            passengerMileageRate = 0.15m,
            invoiceFilenamePattern = "   ",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice filename pattern cannot be empty or whitespace.",
            problem.GetProperty("errors").GetProperty("invoiceFilenamePattern")[0].GetString());
    }

    [Fact]
    public async Task UpdateClientSettings_WithWhitespaceOnlyInvoiceEmailSubjectPattern_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync($"/clients/{TestData.FoxAndFinchId}", new
        {
            id = TestData.FoxAndFinchId,
            name = "Fox & Finch Events",
            email = "bookings@foxandfinch.co.uk",
            billingAddress = new
            {
                line1 = "12 Chapel Street",
                city = "Manchester",
                stateOrCounty = "Greater Manchester",
                postalCode = "M3 5JZ",
                country = "United Kingdom",
            },
            mileageRate = 0.52m,
            passengerMileageRate = 0.15m,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceEmailSubjectPattern = "   ",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice email subject pattern cannot be empty or whitespace.",
            problem.GetProperty("errors").GetProperty("invoiceEmailSubjectPattern")[0].GetString());
    }
}
