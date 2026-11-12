using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;

namespace Glovelly.Uat.Tests;

[Trait("Suite", "DocumentationCapture")]
public sealed class DocumentationCaptureTests : UatTestBase
{
    private readonly ConcurrentQueue<string> mapEvents = new();
    private readonly Stopwatch captureClock = Stopwatch.StartNew();
    private int descriptorRequests;

    protected override BrowserNewContextOptions CreateContextOptions() => new()
    {
        BaseURL = BaseUrl(),
        Locale = "en-GB",
        TimezoneId = "Europe/London",
        ColorScheme = ColorScheme.Light,
        DeviceScaleFactor = 1,
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
        Page.Request += OnMapRequest;
        Page.Response += OnMapResponse;
        Page.RequestFailed += OnMapRequestFailed;
        try
        {
            await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce, ColorScheme = ColorScheme.Light });
            await AuthenticateDocumentationFixtureAsync();
            await CaptureWorkflowViewsAsync("light");
            await CaptureWorkflowViewsAsync("dark");
        }
        finally
        {
            Page.Request -= OnMapRequest;
            Page.Response -= OnMapResponse;
            Page.RequestFailed -= OnMapRequestFailed;
            try
            {
                var directory = Environment.GetEnvironmentVariable("GLOVELLY_DOCUMENTATION_CAPTURE_DIR") ?? Path.Combine("TestResults", "documentation-captures");
                Directory.CreateDirectory(directory);
                await File.WriteAllLinesAsync(Path.Combine(directory, "map-diagnostics.txt"), mapEvents);
            }
            finally
            {
                await ResetDocumentationFixtureAsync();
            }
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
        await CaptureWorkspaceAsync("client-workspace", theme);

        await Page.GetByTestId("nav-gigs").ClickAsync();
        var gigCard = Page.GetByTestId("gig-card").Filter(new LocatorFilterOptions { HasText = "Spring concert" });
        await gigCard.ClickAsync();
        await CaptureWorkspaceAsync("gig-workspace", theme);

        await Page.GetByTestId("gig-expenses-button").ClickAsync();
        var expenseDialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Gig expenses", Exact = true });
        var expense = Page.GetByTestId("gig-expense-item");
        await expense.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await expense.Locator(".associated-item-summary").ClickAsync();
        await expenseDialog.ScreenshotAsync(new LocatorScreenshotOptions { Path = CandidatePath("gig-expenses-mileage", theme) });
        await expenseDialog.Locator(":scope > .panel-heading").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        await Page.GetByTestId("nav-invoices").ClickAsync();
        var invoiceCard = Page.GetByTestId("invoice-card").Filter(new LocatorFilterOptions { HasText = "GLV-202604-001" });
        await invoiceCard.ClickAsync();
        await WaitForSupplementaryPreviewAsync();
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

    private async Task WaitForSupplementaryPreviewAsync()
    {
        // Let actual pane observers settle, then capture ready/unavailable product UI,
        // never an in-progress preview or a synthetic replacement image.
        await Page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
        await Page.WaitForFunctionAsync(
            "() => !Array.from(document.querySelectorAll('.record-visual-rail [role=status]')).some(el => el.textContent.startsWith('Loading'))",
            null, new() { Timeout = 30_000 });
    }

    private async Task CaptureWorkspaceAsync(string name, string theme)
    {
        var workspace = Page.Locator(".section-layout");
        var image = workspace.Locator(".map-search-preview img:visible");
        var path = CandidatePath(name, theme);
        File.Delete(path + ".mask");
        RecordMapEvent($"{name}-{theme}: waiting for loaded map");

        try
        {
            await WaitForLoadedMapAsync(image, 20_000);
            await WaitForStableMapAsync();

            var bounds = await workspace.BoundingBoxAsync();
            var mapElement = await image.ElementHandleAsync();
            var map = mapElement is null ? null : await mapElement.BoundingBoxAsync();
            if (bounds is null || mapElement is null || map is null || map.Width <= 0 || map.Height <= 0)
                throw new InvalidOperationException("Map or workspace has no visible bounds.");

            var left = (int)Math.Floor(map.X - bounds.X);
            var top = (int)Math.Floor(map.Y - bounds.Y);
            var right = (int)Math.Ceiling(map.X + map.Width - bounds.X);
            var bottom = (int)Math.Ceiling(map.Y + map.Height - bounds.Y);
            if (left < 0 || top < 0 || right > Math.Ceiling(bounds.Width) || bottom > Math.Ceiling(bounds.Height))
                throw new InvalidOperationException("Map extends beyond the captured workspace.");

            var screenshot = await workspace.ScreenshotAsync();
            var after = await mapElement.BoundingBoxAsync();
            if (after is null || Math.Abs(after.X - map.X) > 0.1 || Math.Abs(after.Y - map.Y) > 0.1 ||
                Math.Abs(after.Width - map.Width) > 0.1 || Math.Abs(after.Height - map.Height) > 0.1 ||
                !await mapElement.EvaluateAsync<bool>("img => img.isConnected && !img.hidden && img.complete && img.naturalWidth > 0 && img.naturalHeight > 0 && !img.closest('.record-visual-rail')?.querySelector('[role=status]')"))
                throw new InvalidOperationException("Map changed while the workspace was captured.");

            // Record only geometry, never the provider URL. The candidate remains real product UI.
            await File.WriteAllBytesAsync(path, screenshot);
            await File.WriteAllTextAsync(path + ".mask", $"{left} {top} {right} {bottom}\n");
            RecordMapEvent($"{name}-{theme}: captured map {right - left}x{bottom - top}");
        }
        catch (Exception)
        {
            RecordMapEvent($"{name}-{theme}: map unavailable or unstable; candidate rejected");
            // Do not include Playwright exception text, which could contain provider URLs.
            throw new InvalidOperationException($"{name}-{theme} requires a loaded, stable map. See map-diagnostics.txt for safe request statuses.");
        }
    }

    internal static async Task WaitForLoadedMapAsync(ILocator image, float timeout)
    {
        await Assertions.Expect(image).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = timeout });
        await Assertions.Expect(image).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = timeout });
        if (!await image.EvaluateAsync<bool>("img => img.complete && img.naturalWidth > 0 && img.naturalHeight > 0"))
            throw new InvalidOperationException("Map image did not finish loading.");
    }

    private async Task WaitForStableMapAsync()
    {
        // The map frame debounces resize-driven replacements for 300ms. Require
        // a continuously loaded image and unchanged bounds beyond that interval.
        await Page.EvaluateAsync("""
            () => new Promise((resolve, reject) => {
              const started = performance.now();
              let previous = null;
              let stableSince = 0;
              function check(now) {
                if (now - started > 20000) { reject(new Error('Map did not settle')); return; }
                const rail = document.querySelector('.section-layout .record-visual-rail');
                const image = rail?.querySelector('.map-search-preview img:not([hidden])');
                const rect = image?.getBoundingClientRect();
                const ready = image && image.complete && image.naturalWidth > 0 && image.naturalHeight > 0 &&
                  !rail.querySelector('[role=status]') && rect.width > 0 && rect.height > 0;
                const dimensions = ready ? [rect.x, rect.y, rect.width, rect.height] : null;
                if (!ready || !previous || previous.image !== image ||
                    dimensions.some((value, index) => Math.abs(value - previous.dimensions[index]) > 0.1)) {
                  previous = ready ? { image, dimensions } : null;
                  stableSince = now;
                } else if (now - stableSince >= 500) { resolve(); return; }
                requestAnimationFrame(check);
              }
              requestAnimationFrame(check);
            })
            """);
    }

    private void OnMapRequest(object? sender, IRequest request)
    {
        var kind = MapRequestKind(request.Url);
        if (kind is null) return;
        if (kind != "provider-image") Interlocked.Increment(ref descriptorRequests);
        RecordMapEvent($"{kind}: requested; descriptor requests this run={Volatile.Read(ref descriptorRequests)}");
    }

    private void OnMapResponse(object? sender, IResponse response)
    {
        var kind = MapRequestKind(response.Url);
        if (kind is not null) RecordMapEvent($"{kind}: HTTP {response.Status}");
    }

    private void OnMapRequestFailed(object? sender, IRequest request)
    {
        var kind = MapRequestKind(request.Url);
        if (kind is not null) RecordMapEvent($"{kind}: network request failed or was cancelled");
    }

    private void RecordMapEvent(string message) => mapEvents.Enqueue($"{captureClock.Elapsed.TotalSeconds:F1}s {message}");

    internal static string? MapRequestKind(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Host.Equals("maps.googleapis.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath == "/maps/api/staticmap") return "provider-image";
        if (!uri.AbsolutePath.EndsWith("/map-preview", StringComparison.Ordinal)) return null;
        if (uri.AbsolutePath.StartsWith("/clients/", StringComparison.Ordinal)) return "client-descriptor";
        if (uri.AbsolutePath.StartsWith("/gigs/", StringComparison.Ordinal)) return "gig-descriptor";
        return null;
    }

    private static string CandidatePath(string name, string theme)
    {
        var directory = Environment.GetEnvironmentVariable("GLOVELLY_DOCUMENTATION_CAPTURE_DIR") ?? Path.Combine("TestResults", "documentation-captures");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{name}-{theme}.png");
    }
}
