using System.Net;
using Glovelly.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class PublicSurfaceRoutingTests
{
    [Fact]
    public async Task LegacyApplicationHost_RedirectsToMenuWithPathAndQuery()
    {
        await using var factory = new GlovellyApiFactory()
            .WithConfiguration(new Dictionary<string, string?>
            {
                ["App:PublicBaseUrl"] = "https://menu.glovelly.net",
                ["App:LegacyApplicationHost"] = "glovelly.net",
            })
            .WithEnvironment("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/auth/login?returnUrl=%2Finvoices%3Fstatus%3Ddraft");
        request.Headers.Host = "glovelly.net";

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(
            "https://menu.glovelly.net/auth/login?returnUrl=%2Finvoices%3Fstatus%3Ddraft",
            response.Headers.Location?.OriginalString);
    }
}
