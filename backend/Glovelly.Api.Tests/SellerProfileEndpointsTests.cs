using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class SellerProfileEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public SellerProfileEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_WhenNoProfileExists_ReturnsEmptyEditableState()
    {
        var response = await _client.GetAsync("/seller-profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("id").ValueKind);
        Assert.False(profile.GetProperty("isConfigured").GetBoolean());
        Assert.False(profile.GetProperty("isInvoiceReady").GetBoolean());
        Assert.Contains(
            profile.GetProperty("missingFields").EnumerateArray().Select(value => value.GetString()),
            value => value == "sellerName");
    }

    [Fact]
    public async Task Put_WhenPaymentDetailsIncomplete_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/seller-profile", new
        {
            sellerName = "Glovelly Music Ltd",
            addressLine1 = "1 Chapel Street",
            city = "Manchester",
            country = "United Kingdom",
            sortCode = "12-34-56",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Account name is required when payment details are provided.",
            problem.GetProperty("errors").GetProperty("accountName")[0].GetString());
        Assert.Equal(
            "Account number is required when payment details are provided.",
            problem.GetProperty("errors").GetProperty("accountNumber")[0].GetString());
    }

    [Fact]
    public async Task Put_WhenProfileValid_PersistsAndReloads()
    {
        var saveResponse = await _client.PutAsJsonAsync("/seller-profile", new
        {
            sellerName = "Glovelly Music Ltd",
            addressLine1 = "1 Chapel Street",
            addressLine2 = "Suite 4",
            city = "Manchester",
            region = "Greater Manchester",
            postcode = "M1 1AA",
            country = "United Kingdom",
            email = "hello@glovelly.test",
            phone = "+44 7700 900123",
            accountName = "Glovelly Music Ltd",
            sortCode = "12-34-56",
            accountNumber = "12345678",
            paymentReferenceNote = "Use invoice number as the payment reference.",
        });

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var savedProfile = await saveResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(savedProfile.GetProperty("isConfigured").GetBoolean());
        Assert.True(savedProfile.GetProperty("isInvoiceReady").GetBoolean());
        Assert.Empty(savedProfile.GetProperty("missingFields").EnumerateArray());

        var getResponse = await _client.GetAsync("/seller-profile");
        getResponse.EnsureSuccessStatusCode();

        var fetchedProfile = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Glovelly Music Ltd", fetchedProfile.GetProperty("sellerName").GetString());
        Assert.Equal("1 Chapel Street", fetchedProfile.GetProperty("addressLine1").GetString());
        Assert.Equal("Suite 4", fetchedProfile.GetProperty("addressLine2").GetString());
        Assert.Equal("Manchester", fetchedProfile.GetProperty("city").GetString());
        Assert.Equal("Greater Manchester", fetchedProfile.GetProperty("region").GetString());
        Assert.Equal("M1 1AA", fetchedProfile.GetProperty("postcode").GetString());
        Assert.Equal("United Kingdom", fetchedProfile.GetProperty("country").GetString());
        Assert.Equal("Glovelly Music Ltd", fetchedProfile.GetProperty("accountName").GetString());
        Assert.Equal("12-34-56", fetchedProfile.GetProperty("sortCode").GetString());
        Assert.Equal("12345678", fetchedProfile.GetProperty("accountNumber").GetString());
    }
}
