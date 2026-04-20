using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GigEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public GigEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGig_WithoutInvoice_ReturnsDerivedNotInvoiced()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Uninvoiced rehearsal",
            date = "2026-06-01",
            venue = "Band Room",
            fee = 120.00m,
            travelMiles = 8.50m,
            notes = "Evening prep",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, gig.ValueKind);
        Assert.Equal(JsonValueKind.Null, gig.GetProperty("invoiceId").ValueKind);
        Assert.False(gig.GetProperty("isInvoiced").GetBoolean());
        Assert.Equal(JsonValueKind.Null, gig.GetProperty("invoicedAt").ValueKind);
    }

    [Fact]
    public async Task CreateGig_WithInvoice_ReturnsDerivedInvoicedAndAuditTimestamp()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Invoiced performance",
            date = "2026-06-02",
            venue = "Town Hall",
            fee = 300.00m,
            travelMiles = 14.00m,
            notes = "Festival set",
            wasDriving = false,
            status = 2,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, gig.ValueKind);
        Assert.Equal(TestData.FoxInvoiceId, gig.GetProperty("invoiceId").GetGuid());
        Assert.True(gig.GetProperty("isInvoiced").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(gig.GetProperty("invoicedAt").GetString()));
    }

    [Fact]
    public async Task CreateGig_WithMissingInvoice_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            title = "Bad invoice link",
            date = "2026-06-03",
            venue = "Club",
            fee = 150.00m,
            travelMiles = 10.00m,
            notes = "Test case",
            wasDriving = true,
            status = 0,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, problem.ValueKind);
        Assert.Equal("Invoice does not exist.", problem.GetProperty("errors").GetProperty("invoiceId")[0].GetString());
    }

    [Fact]
    public async Task CreateGig_WithInvoiceForDifferentClient_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.RiversideInvoiceId,
            title = "Cross-client invoice link",
            date = "2026-06-04",
            venue = "Theatre",
            fee = 180.00m,
            travelMiles = 6.00m,
            notes = "Should fail",
            wasDriving = false,
            status = 0,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, problem.ValueKind);
        Assert.Equal("Invoice client must match the gig client.", problem.GetProperty("errors").GetProperty("invoiceId")[0].GetString());
    }

    [Fact]
    public async Task UpdateGig_RemovingInvoice_ClearsInvoicedAt()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Linked gig",
            date = "2026-06-05",
            venue = "Warehouse",
            fee = 225.00m,
            travelMiles = 12.00m,
            notes = "Created for update test",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Object, createdGig.ValueKind);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var updateResponse = await _client.PutAsJsonAsync($"/gigs/{gigId}", new
        {
            id = gigId,
            clientId = TestData.FoxAndFinchId,
            invoiceId = (Guid?)null,
            title = "Linked gig",
            date = "2026-06-05",
            venue = "Warehouse",
            fee = 225.00m,
            travelMiles = 12.00m,
            notes = "Invoice removed",
            wasDriving = true,
            status = 1,
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedGig = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(JsonValueKind.Object, updatedGig.ValueKind);
        Assert.Equal(JsonValueKind.Null, updatedGig.GetProperty("invoiceId").ValueKind);
        Assert.False(updatedGig.GetProperty("isInvoiced").GetBoolean());
        Assert.Equal(JsonValueKind.Null, updatedGig.GetProperty("invoicedAt").ValueKind);
    }

    [Fact]
    public async Task UpdateGig_WithExpenses_ReplacesExpenseCollectionAndReturnsUpdatedGig()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Expense refresh test",
            date = "2026-06-08",
            venue = "Assembly Rooms",
            fee = 180.00m,
            travelMiles = 0.00m,
            notes = "Initial expense set",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Parking",
                    amount = 12.00m,
                },
            },
            invoicedAt = (string?)null,
        });

        createResponse.EnsureSuccessStatusCode();

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = createdGig.GetProperty("id").GetGuid();

        var updateResponse = await _client.PutAsJsonAsync($"/gigs/{gigId}", new
        {
            id = gigId,
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Expense refresh test",
            date = "2026-06-08",
            venue = "Assembly Rooms",
            fee = 180.00m,
            travelMiles = 0.00m,
            passengerCount = (int?)null,
            notes = "Updated expense set",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Tolls",
                    amount = 8.50m,
                },
                new
                {
                    description = "Parking",
                    amount = 14.00m,
                },
            },
            invoicedAt = createdGig.GetProperty("invoicedAt").GetString(),
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedGig = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var updatedExpenses = updatedGig.GetProperty("expenses").EnumerateArray().OrderBy(expense => expense.GetProperty("sortOrder").GetInt32()).ToArray();

        Assert.Equal(2, updatedExpenses.Length);
        Assert.Equal("Tolls", updatedExpenses[0].GetProperty("description").GetString());
        Assert.Equal(8.50m, updatedExpenses[0].GetProperty("amount").GetDecimal());
        Assert.Equal("Parking", updatedExpenses[1].GetProperty("description").GetString());
        Assert.Equal(14.00m, updatedExpenses[1].GetProperty("amount").GetDecimal());

        var invoiceResponse = await _client.GetAsync($"/invoices/{TestData.FoxInvoiceId}");
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = invoice.GetProperty("lines").EnumerateArray().Where(line => line.GetProperty("gigId").GetGuid() == gigId).ToArray();

        Assert.Equal(3, lines.Length);
        Assert.Contains(lines, line => line.GetProperty("description").GetString() == "Performance fee for Expense refresh test (2026-06-08)");
        Assert.Contains(lines, line => line.GetProperty("description").GetString() == "Tolls");
        Assert.Contains(lines, line => line.GetProperty("description").GetString() == "Parking");
    }

    [Fact]
    public async Task CreateGig_WithInvoice_GeneratesMileagePassengerAndExpenseLines()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            invoiceId = TestData.FoxInvoiceId,
            title = "Mileage-rich booking",
            date = "2026-06-12",
            venue = "Civic Hall",
            fee = 300.00m,
            travelMiles = 10.50m,
            passengerCount = 2,
            notes = "Includes two guests and parking",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Parking",
                    amount = 12.00m,
                },
                new
                {
                    description = "Fuel",
                    amount = 20.00m,
                },
            },
            invoicedAt = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var gigId = gig.GetProperty("id").GetGuid();

        var invoiceResponse = await _client.GetAsync($"/invoices/{TestData.FoxInvoiceId}");
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = invoice.GetProperty("lines").EnumerateArray().OrderBy(line => line.GetProperty("sortOrder").GetInt32()).ToArray();

        Assert.Equal(5, lines.Length);
        Assert.All(lines, line => Assert.True(line.GetProperty("isSystemGenerated").GetBoolean()));
        Assert.All(lines, line => Assert.Equal(gigId, line.GetProperty("gigId").GetGuid()));

        Assert.Equal("PerformanceFee", lines[0].GetProperty("type").GetString());
        Assert.Equal(300.00m, lines[0].GetProperty("lineTotal").GetDecimal());

        Assert.Equal("Mileage", lines[1].GetProperty("type").GetString());
        Assert.Equal(10.50m, lines[1].GetProperty("quantity").GetDecimal());
        Assert.Equal(0.52m, lines[1].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(5.46m, lines[1].GetProperty("lineTotal").GetDecimal());

        Assert.Equal("PassengerMileage", lines[2].GetProperty("type").GetString());
        Assert.Equal(21.00m, lines[2].GetProperty("quantity").GetDecimal());
        Assert.Equal(0.15m, lines[2].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(3.15m, lines[2].GetProperty("lineTotal").GetDecimal());

        Assert.Equal("Parking", lines[3].GetProperty("description").GetString());
        Assert.Equal(12.00m, lines[3].GetProperty("lineTotal").GetDecimal());
        Assert.Equal("Fuel", lines[4].GetProperty("description").GetString());
        Assert.Equal(20.00m, lines[4].GetProperty("lineTotal").GetDecimal());

        Assert.Equal(340.61m, invoice.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task CreateGig_UsesUserPricingFallbackWhenClientRatesAreMissing()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.RiversideId,
            invoiceId = TestData.RiversideInvoiceId,
            title = "Residency workshop",
            date = "2026-06-13",
            venue = "River Room",
            fee = 200.00m,
            travelMiles = 8.00m,
            passengerCount = 1,
            notes = "Uses user fallback rates",
            wasDriving = true,
            status = 1,
            expenses = new[]
            {
                new
                {
                    description = "Parking",
                    amount = 9.00m,
                },
            },
            invoicedAt = (string?)null,
        });

        response.EnsureSuccessStatusCode();

        var invoiceResponse = await _client.GetAsync($"/invoices/{TestData.RiversideInvoiceId}");
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lines = invoice.GetProperty("lines").EnumerateArray().OrderBy(line => line.GetProperty("sortOrder").GetInt32()).ToArray();

        Assert.Equal(4, lines.Length);
        Assert.All(lines, line => Assert.True(line.GetProperty("isSystemGenerated").GetBoolean()));

        Assert.Equal(200.00m, lines[0].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(3.60m, lines[1].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(0.80m, lines[2].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(9.00m, lines[3].GetProperty("lineTotal").GetDecimal());
        Assert.Equal(213.40m, invoice.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task CreateGig_WithMissingRequiredFields_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = Guid.Empty,
            title = "   ",
            date = "0001-01-01",
            venue = "",
            fee = -25.00m,
            travelMiles = -3.00m,
            wasDriving = true,
            status = 999,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var errors = problem.GetProperty("errors");

        Assert.Equal("Client is required.", errors.GetProperty("clientId")[0].GetString());
        Assert.Equal("Title is required.", errors.GetProperty("title")[0].GetString());
        Assert.Equal("Date is required.", errors.GetProperty("date")[0].GetString());
        Assert.Equal("Location or venue is required.", errors.GetProperty("venue")[0].GetString());
        Assert.Equal("Fee cannot be negative.", errors.GetProperty("fee")[0].GetString());
        Assert.Equal("Travel miles cannot be negative.", errors.GetProperty("travelMiles")[0].GetString());
        Assert.Equal("Status is invalid.", errors.GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task CreateGig_PersistsAndAppearsInGigList()
    {
        var createResponse = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Summer garden party",
            date = "2026-07-11",
            venue = "Botanical Gardens",
            fee = 425.00m,
            notes = "Outdoor afternoon set",
            wasDriving = true,
            status = 1,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdGig = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var createdGigId = createdGig.GetProperty("id").GetGuid();

        var listResponse = await _client.GetAsync("/gigs");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var gigs = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var savedGig = gigs.EnumerateArray().FirstOrDefault(gig => gig.GetProperty("id").GetGuid() == createdGigId);

        Assert.Equal(JsonValueKind.Object, savedGig.ValueKind);
        Assert.Equal(TestData.FoxAndFinchId, savedGig.GetProperty("clientId").GetGuid());
        Assert.Equal("Summer garden party", savedGig.GetProperty("title").GetString());
        Assert.Equal("2026-07-11", savedGig.GetProperty("date").GetString());
        Assert.Equal("Botanical Gardens", savedGig.GetProperty("venue").GetString());
        Assert.Equal(425.00m, savedGig.GetProperty("fee").GetDecimal());
        Assert.Equal("Confirmed", savedGig.GetProperty("status").GetString());
    }
}
