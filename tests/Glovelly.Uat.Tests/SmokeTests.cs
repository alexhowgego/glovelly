using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

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

    private static System.Text.RegularExpressions.Regex GlovellyHeadingRegex()
    {
        return new System.Text.RegularExpressions.Regex("Glovelly", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
