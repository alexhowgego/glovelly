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
            await Page.EvaluateAsync("async () => { await document.fonts.ready; }");
            await Page.GetByTestId("nav-gigs").ClickAsync();
            await Page.Locator(".section-layout").ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("gig-workspace") });
            await Page.GetByTestId("nav-invoices").ClickAsync();
            var invoiceCard = Page.GetByTestId("invoice-card").Filter(new LocatorFilterOptions { HasText = "GLV-202604-001" });
            await invoiceCard.ClickAsync();
            await Page.Locator(".section-layout").ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("invoice-status") });
            await Page.GetByTestId("invoice-send-button").ClickAsync();
            var review = Page.GetByTestId("invoice-email-review-modal");
            await review.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await review.Locator(".invoice-email-review-content").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await Assertions.Expect(Page.GetByTestId("invoice-email-review-send-button")).ToBeEnabledAsync();
            await review.ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("invoice-email-review") });
        }
        finally
        {
            await ResetDocumentationFixtureAsync();
        }
    });

    private static string CandidatePath(string name)
    {
        var directory = Environment.GetEnvironmentVariable("GLOVELLY_DOCUMENTATION_CAPTURE_DIR") ?? Path.Combine("TestResults", "documentation-captures");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{name}.png");
    }
}
