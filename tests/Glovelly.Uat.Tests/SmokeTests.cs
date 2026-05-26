using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class SmokeTests : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? context;
    private IPage? page;

    private IPage Page => page ?? throw new InvalidOperationException("The Playwright page has not been initialized.");

    [Fact]
    public async Task HomePageLoadsSuccessfully()
    {
        var response = await Page.GotoAsync("/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
        });

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected the home page to return a successful response, but received HTTP {response.Status}.");
        await Page.GetByRole(AriaRole.Heading, new() { NameRegex = GlovellyHeadingRegex() }).WaitForAsync();
    }

    [Fact]
    public async Task SignInEntryPointIsVisible()
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
    }

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Headless(),
        });
        context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl(),
        });
        page = await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (context is not null)
        {
            await context.DisposeAsync();
        }

        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
    }

    private static string BaseUrl()
    {
        var baseUrl = Environment.GetEnvironmentVariable("GLOVELLY_UAT_BASE_URL")?.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Set GLOVELLY_UAT_BASE_URL to the Glovelly deployment under test, for example https://staging.glovelly.net.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("GLOVELLY_UAT_BASE_URL must be an absolute HTTP or HTTPS URL.");
        }

        return uri.ToString().TrimEnd('/');
    }

    private static bool Headless()
    {
        var value = Environment.GetEnvironmentVariable("GLOVELLY_UAT_HEADLESS");

        return !bool.TryParse(value, out var headless) || headless;
    }

    private static System.Text.RegularExpressions.Regex GlovellyHeadingRegex()
    {
        return new System.Text.RegularExpressions.Regex("Glovelly", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
