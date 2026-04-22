using System.Net;
using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class AuthEndpointsTests : IClassFixture<GlovellyApiFactory>
{
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
}
