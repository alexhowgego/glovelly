using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

[Trait("Suite", "DocumentationCapture")]
public sealed class DocumentationCaptureTests : UatTestBase
{
    protected override BrowserNewContextOptions CreateContextOptions() => new()
    {
        BaseURL = BaseUrl(),
        Locale = "en-GB",
        TimezoneId = "Europe/London",
        ColorScheme = ColorScheme.Light,
        ViewportSize = new ViewportSize { Width = 1440, Height = 960 },
    };

    protected override Task ConfigureContextAsync(IBrowserContext browserContext) => browserContext.AddInitScriptAsync(
        """
        (() => {
          const fixedNow = Date.parse('2026-04-06T09:00:00.000Z');
          const NativeDate = Date;
          class FixedDate extends NativeDate {
            constructor(...args) { super(...(args.length ? args : [fixedNow])); }
            static now() { return fixedNow; }
          }
          Object.setPrototypeOf(FixedDate, NativeDate);
          window.Date = FixedDate;
        })();
        """);

    [Fact]
    public Task CapturesCoreWorkflowViews() => RunWithDiagnosticsAsync(nameof(CapturesCoreWorkflowViews), async () =>
    {
        try
        {
            await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce, ColorScheme = ColorScheme.Light });
            await AuthenticateDocumentationFixtureAsync();
            await CaptureWorkflowViewsAsync("light");
            await CaptureWorkflowViewsAsync("dark");
        }
        finally
        {
            await ResetDocumentationFixtureAsync();
        }
    });

    private async Task CaptureWorkflowViewsAsync(string theme)
    {
        await Page.EvaluateAsync(
            "theme => window.localStorage.setItem('glovelly.theme-preference', theme)",
            theme);
        await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
        await Assertions.Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", theme);
        await Page.EvaluateAsync("async () => { await document.fonts.ready; }");

        await Page.GetByTestId("nav-clients").ClickAsync();
        await Page.GetByTestId("client-card").Filter(new LocatorFilterOptions { HasText = "The Lantern Quartet" }).ClickAsync();
        await Page.Locator(".section-layout").ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("client-workspace", theme) });

        await Page.GetByTestId("nav-gigs").ClickAsync();
        var gigCard = Page.GetByTestId("gig-card").Filter(new LocatorFilterOptions { HasText = "Spring concert" });
        await gigCard.ClickAsync();
        await Page.Locator(".section-layout").ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("gig-workspace", theme) });

        var expense = Page.GetByTestId("gig-expense-item");
        await expense.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await expense.Locator(".associated-item-summary").ClickAsync();
        await Page.Locator(".section-layout").ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("gig-expenses-mileage", theme) });

        await Page.GetByTestId("nav-invoices").ClickAsync();
        var invoiceCard = Page.GetByTestId("invoice-card").Filter(new LocatorFilterOptions { HasText = "GLV-202604-001" });
        await invoiceCard.ClickAsync();
        await Page.Locator(".section-layout").ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("invoice-status", theme) });

        await Page.GetByTestId("invoice-send-button").ClickAsync();
        var review = Page.GetByTestId("invoice-email-review-modal");
        await review.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await review.Locator(".invoice-email-review-content").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await Assertions.Expect(Page.GetByTestId("invoice-email-review-send-button")).ToBeEnabledAsync();
        await review.ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("invoice-email-review", theme) });

        await review.Locator(".panel-heading").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancel" }).ClickAsync();
        await Page.GetByTestId("profile-menu-button").ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Seller profile" }).ClickAsync();
        var sellerProfile = Page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Seller profile" });
        await sellerProfile.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await sellerProfile.ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("seller-profile", theme) });

        await sellerProfile.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Close" }).ClickAsync();
        await Page.GetByTestId("profile-menu-button").ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Settings" }).ClickAsync();
        var userSettings = Page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Your settings" });
        await userSettings.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await userSettings.ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("user-settings", theme) });
    }

    private static string CandidatePath(string name, string theme)
    {
        var directory = Environment.GetEnvironmentVariable("GLOVELLY_DOCUMENTATION_CAPTURE_DIR") ?? Path.Combine("TestResults", "documentation-captures");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{name}-{theme}.png");
    }
}
