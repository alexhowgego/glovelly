using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

[Trait("Suite", "DocumentationCapture")]
public sealed class DocumentationCaptureTests : UatTestBase
{
    [Fact]
    public Task CapturesCoreWorkflowViews() => RunWithDiagnosticsAsync(nameof(CapturesCoreWorkflowViews), async () =>
    {
        try
        {
            await Page.SetViewportSizeAsync(1440, 960);
            await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce, ColorScheme = ColorScheme.Light });
            await AuthenticateDocumentationFixtureAsync();
            await Page.GetByTestId("nav-gigs").ClickAsync();
            await Page.GetByTestId("gig-card").Filter(new LocatorFilterOptions { HasText = "Spring concert" }).ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("gig-card") });
            await Page.GetByTestId("nav-invoices").ClickAsync();
            await Page.GetByTestId("invoice-card").Filter(new LocatorFilterOptions { HasText = "GLV-202604-001" }).ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("invoice-card") });
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
