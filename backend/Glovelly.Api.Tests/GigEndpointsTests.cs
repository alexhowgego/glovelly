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
}
