using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class TypedGigEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private readonly HttpClient _client;

    public TypedGigEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGig_PersistsTypeAndGeneratesTypeAwareFeeDescription()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Weekly piano lesson",
            date = "2026-06-20",
            venue = "Studio 2",
            fee = 60m,
            travelMiles = 0m,
            wasDriving = false,
            status = "Confirmed",
            type = "Teaching",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var gig = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Teaching", gig.GetProperty("type").GetString());

        var invoiceResponse = await _client.PostAsync($"/gigs/{gig.GetProperty("id").GetGuid()}/generate-invoice", null, TestContext.Current.CancellationToken);
        invoiceResponse.EnsureSuccessStatusCode();
        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(invoice.GetProperty("lines").EnumerateArray(), line =>
            line.GetProperty("description").GetString() == "Teaching fee for Weekly piano lesson (2026-06-20)");
    }

    [Fact]
    public async Task CreateGig_WithInvalidType_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/gigs", new
        {
            clientId = TestData.FoxAndFinchId,
            title = "Invalid type",
            date = "2026-06-20",
            venue = "Studio 2",
            fee = 60m,
            travelMiles = 0m,
            wasDriving = false,
            status = "Confirmed",
            type = 99,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
