using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Data;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class TestAuthEndpointsTests
{
    private const string UatSecret = "shared-uat-secret";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_IsUnavailableOutsideStaging()
    {
        await using var factory = CreateFactory("Production");
        var client = factory.CreateClient();

        var response = await client.PostAsync("/test-auth/login", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutSecret_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient();

        var response = await client.PostAsync("/test-auth/login", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidSecret_ReturnsForbidden()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/test-auth/login");
        request.Headers.Add("X-Glovelly-Uat-Secret", "wrong-secret");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidSecret_SignsInRegressionUserAndSeedsBaselineData()
    {
        await using var factory = CreateFactory("Staging");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/test-auth/login");
        request.Headers.Add("X-Glovelly-Uat-Secret", UatSecret);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(AppDbSeeder.UatRegressionEmail, payload.GetProperty("email").GetString());

        var meResponse = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var mePayload = await meResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(AppDbSeeder.UatRegressionEmail, mePayload.GetProperty("email").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotNull(await dbContext.Users.FindAsync(AppDbSeeder.UatRegressionUserId));
        Assert.NotNull(await dbContext.Clients.FindAsync(AppDbSeeder.UatRegressionClientId));
        Assert.NotNull(await dbContext.SellerProfiles.FindAsync(AppDbSeeder.UatRegressionSellerProfileId));
    }

    private static GlovellyApiFactory CreateFactory(string environmentName)
    {
        return new GlovellyApiFactory()
            .WithConfiguration(new Dictionary<string, string?>
            {
                ["Uat:Secret"] = UatSecret,
            })
            .WithEnvironment(environmentName);
    }
}
