using Microsoft.Playwright;
using System.Net.Http.Json;
using Xunit;

namespace Glovelly.Uat.Tests;

[Trait("Suite", "Smoke")]
public sealed class SmokeTests : UatTestBase
{
    [Fact]
    public Task HomePageLoadsSuccessfully() => RunWithDiagnosticsAsync(nameof(HomePageLoadsSuccessfully), async () =>
    {
        var response = await Page.GotoAsync("/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
        });

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected the home page to return a successful response, but received HTTP {response.Status}.");
        await Page.GetByRole(AriaRole.Heading, new() { NameRegex = GlovellyHeadingRegex() }).WaitForAsync();
    });

    [Fact]
    public Task SignInEntryPointIsVisible() => RunWithDiagnosticsAsync(nameof(SignInEntryPointIsVisible), async () =>
    {
        await Page.GotoAsync("/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
        });

        var signInButton = Page.GetByRole(AriaRole.Button, new() { Name = "Continue with Google" });

        await signInButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        Assert.True(await signInButton.IsEnabledAsync(), "Expected the Google sign-in entry point to be enabled.");
    });

    [Fact]
    public async Task HealthEndpointRespondsSuccessfully()
    {
        using var httpClient = CreateHttpClient();
        using var response = await httpClient.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode, $"Expected /health to return a successful response, but received HTTP {(int)response.StatusCode}.");

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.Equal("ok", payload?.Status);
    }

    [Fact]
    public async Task AppMetadataEndpointRespondsSuccessfully()
    {
        using var httpClient = CreateHttpClient();
        using var response = await httpClient.GetAsync("/app/metadata");

        Assert.True(response.IsSuccessStatusCode, $"Expected /app/metadata to return a successful response, but received HTTP {(int)response.StatusCode}.");

        var payload = await response.Content.ReadFromJsonAsync<AppMetadataPayload>();
        Assert.Contains("Glovelly", payload?.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/gordon-192.png")]
    public async Task KeyStaticAssetLoadsSuccessfully(string path)
    {
        using var httpClient = CreateHttpClient();
        using var response = await httpClient.GetAsync(path);

        Assert.True(response.IsSuccessStatusCode, $"Expected {path} to return a successful response, but received HTTP {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(content);
    }

    private static System.Text.RegularExpressions.Regex GlovellyHeadingRegex()
    {
        return new System.Text.RegularExpressions.Regex("Glovelly", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private sealed record HealthPayload(string Status);

    private sealed record AppMetadataPayload(string Title);
}
