using Glovelly.Api.Tests.Infrastructure;
using System.Net.Http.Json;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AppMetadataEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private readonly HttpClient _client;

    public AppMetadataEndpointsTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOkStatus()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.Equal("ok", payload?.Status);
    }

    private sealed record HealthPayload(string Status);
}
