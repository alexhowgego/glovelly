using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GoogleDriveIntegrationEndpointsTests : IClassFixture<GlovellyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GlovellyApiFactory _factory;
    private readonly HttpClient _client;

    public GoogleDriveIntegrationEndpointsTests(GlovellyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Callback_WithCodeAndState_RedirectsToIntegrationStatus()
    {
        var client = _factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/integrations/google-drive/callback?code=auth-code&state=state-token");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/?integration=google-drive&status=callback-received",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Callback_WithoutCodeOrState_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync("/integrations/google-drive/callback");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(
            "Google Drive authorization code is required.",
            problem.GetProperty("errors").GetProperty("code")[0].GetString());
        Assert.Equal(
            "Google Drive OAuth state is required.",
            problem.GetProperty("errors").GetProperty("state")[0].GetString());
    }

    [Fact]
    public async Task Callback_WithGoogleError_ReturnsProblem()
    {
        var response = await _client.GetAsync(
            "/integrations/google-drive/callback?error=access_denied&error_description=User%20cancelled");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Google Drive connection was not approved.", problem.GetProperty("title").GetString());
        Assert.Equal("User cancelled", problem.GetProperty("detail").GetString());
    }
}
