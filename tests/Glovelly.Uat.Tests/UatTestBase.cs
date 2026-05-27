using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public abstract class UatTestBase : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? context;
    private IPage? page;
    private bool tracingActive;

    protected IPage Page => page ?? throw new InvalidOperationException("The Playwright page has not been initialized.");

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
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });
        tracingActive = true;
        page = await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (context is not null)
        {
            if (tracingActive)
            {
                await context.Tracing.StopAsync();
                tracingActive = false;
            }

            await context.DisposeAsync();
        }

        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
    }

    protected async Task RunWithDiagnosticsAsync(string testName, Func<Task> testBody)
    {
        try
        {
            await testBody();
        }
        catch
        {
            await CaptureFailureDiagnosticsAsync(testName);
            throw;
        }
    }

    protected async Task AuthenticateWithUatSecretAsync()
    {
        var secret = RequiredEnvironmentVariable(
            "GLOVELLY_UAT_SECRET",
            "Set GLOVELLY_UAT_SECRET to authenticate with the staging-only UAT endpoint.");

        await Page.GotoAsync("/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
        });

        var status = await Page.EvaluateAsync<int>(
            """
            async (secret) => {
              const response = await fetch('/test-auth/login', {
                method: 'POST',
                headers: { 'X-Glovelly-Uat-Secret': secret },
                credentials: 'include'
              });
              return response.status;
            }
            """,
            secret);

        Assert.Equal(200, status);

        await Page.GotoAsync("/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
        });
        await Page.GetByTestId("nav-clients").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    protected static string RequiredEnvironmentVariable(string name, string message)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value;
    }

    protected static string CreateRunId()
    {
        var shortSha = Environment.GetEnvironmentVariable("GITHUB_SHA")?.Trim();
        shortSha = string.IsNullOrWhiteSpace(shortSha) ? Guid.NewGuid().ToString("N")[..8] : shortSha[..Math.Min(shortSha.Length, 8)];

        return $"UAT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{shortSha}";
    }

    protected static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(BaseUrl(), UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    private async Task CaptureFailureDiagnosticsAsync(string testName)
    {
        var artifactDirectory = ArtifactDirectory();
        var safeTestName = SafeArtifactName(testName);

        if (page is not null)
        {
            var screenshotDirectory = Path.Combine(artifactDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotDirectory, $"{safeTestName}.png"),
                FullPage = true,
            });
        }

        if (context is not null && tracingActive)
        {
            var traceDirectory = Path.Combine(artifactDirectory, "playwright-traces");
            Directory.CreateDirectory(traceDirectory);
            await context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = Path.Combine(traceDirectory, $"{safeTestName}.zip"),
            });
            tracingActive = false;
        }
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

    private static string ArtifactDirectory()
    {
        var directory = Environment.GetEnvironmentVariable("GLOVELLY_UAT_ARTIFACT_DIR")?.Trim();

        return string.IsNullOrWhiteSpace(directory)
            ? Path.Combine("TestResults", "uat")
            : directory;
    }

    private static string SafeArtifactName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(valueChar => invalidChars.Contains(valueChar) ? '-' : valueChar).ToArray();

        return new string(chars);
    }
}
