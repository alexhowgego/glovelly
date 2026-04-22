using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AuthEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public AuthEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenAuthenticatedClaimUserIsUnknown()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("X-Test-UserId", TestAuthContext.AlternateUserId.ToString());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUserInvoiceFilenamePattern()
    {
        var updateResponse = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "{MonthName} {Year} {InvoiceNumber}",
        });
        updateResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "{MonthName} {Year} {InvoiceNumber}",
            payload.GetProperty("invoiceFilenamePattern").GetString());
    }

    [Fact]
    public async Task UpdateSettings_PersistsInvoiceFilenamePattern()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "  {ClientName} {InvoiceNumber}  ",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "{ClientName} {InvoiceNumber}",
            payload.GetProperty("invoiceFilenamePattern").GetString());
    }

    [Fact]
    public async Task UpdateSettings_WithWhitespaceOnlyInvoiceFilenamePattern_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "   ",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice filename pattern cannot be empty or whitespace.",
            problem.GetProperty("errors").GetProperty("invoiceFilenamePattern")[0].GetString());
    }
}
