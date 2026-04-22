using Glovelly.Api.Tests.Infrastructure;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class ApiCacheHeadersTests : IClassFixture<GlovellyApiFactory>
{
    private readonly HttpClient _client;

    public ApiCacheHeadersTests(GlovellyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/auth/me")]
    [InlineData("/clients")]
    [InlineData("/seller-profile")]
    [InlineData("/admin/users")]
    public async Task ApiEndpoints_ReturnNoStoreCacheHeaders(string path)
    {
        var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();

        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.NoStore);
        Assert.True(cacheControl.NoCache);
        Assert.Equal(TimeSpan.Zero, cacheControl.MaxAge);

        Assert.Contains(response.Headers.Pragma, value => value.Name == "no-cache");
    }
}
