using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AuthEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
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
            invoiceReplyToEmail = "billing@example.com",
        });
        updateResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "{MonthName} {Year} {InvoiceNumber}",
            payload.GetProperty("invoiceFilenamePattern").GetString());
        Assert.Equal(
            "billing@example.com",
            payload.GetProperty("invoiceReplyToEmail").GetString());
    }

    [Fact]
    public async Task UpdateSettings_PersistsInvoiceFilenamePatternAndReplyToEmail()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "  {ClientName} {InvoiceNumber}  ",
            invoiceReplyToEmail = "  accounts@example.com  ",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "{ClientName} {InvoiceNumber}",
            payload.GetProperty("invoiceFilenamePattern").GetString());
        Assert.Equal(
            "accounts@example.com",
            payload.GetProperty("invoiceReplyToEmail").GetString());
    }

    [Fact]
    public async Task UpdateSettings_WithWhitespaceOnlyInvoiceFilenamePattern_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "   ",
            invoiceReplyToEmail = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Invoice filename pattern cannot be empty or whitespace.",
            problem.GetProperty("errors").GetProperty("invoiceFilenamePattern")[0].GetString());
    }

    [Fact]
    public async Task UpdateSettings_WithInvalidReplyToEmail_ReturnsValidationProblem()
    {
        var response = await _client.PutAsJsonAsync("/auth/me/settings", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            invoiceFilenamePattern = "{InvoiceNumber}",
            invoiceReplyToEmail = "not-an-email",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Reply-to email must be a valid email address.",
            problem.GetProperty("errors").GetProperty("invoiceReplyToEmail")[0].GetString());
    }

    [Fact]
    public async Task DeniedPage_ShowsRequestAccessUi_WhenSignedRequestTokenProvided()
    {
        var token = CreateAccessRequestToken("new-user@glovelly.local", "New User", "google-sub-new-user");

        var response = await _client.GetAsync($"/auth/denied?code=not_authorized&request={Uri.EscapeDataString(token)}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Request access", html);
        Assert.Contains("data-access-request-button", html);
    }

    private string CreateAccessRequestToken(string email, string displayName, string subject)
    {
        using var scope = _factory.Services.CreateScope();
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider
            .CreateProtector("Glovelly.AccessRequest")
            .ToTimeLimitedDataProtector();

        return protector.Protect(
            JsonSerializer.Serialize(new
            {
                email,
                displayName,
                subject,
            }),
            lifetime: TimeSpan.FromMinutes(15));
    }
}
