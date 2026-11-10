using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

// Browser-only readiness tests; these do not create documentation screenshots or call Google.
public sealed class DocumentationMapReadinessTests : UatTestBase
{
    private const string Pixel = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==";

    [Fact]
    public async Task LoadedVisibleImageIsAccepted()
    {
        await Page.SetContentAsync($"<img alt='map' src='{Pixel}'>");
        await DocumentationCaptureTests.WaitForLoadedMapAsync(Page.GetByAltText("map"), 1_000);
    }

    [Fact]
    public async Task MissingAndHiddenImagesAreRejected()
    {
        await Page.SetContentAsync($"<img alt='map' hidden src='{Pixel}'>");
        await Assert.ThrowsAnyAsync<Exception>(() => DocumentationCaptureTests.WaitForLoadedMapAsync(Page.GetByAltText("map"), 250));
        await Page.SetContentAsync("<div>Map unavailable</div>");
        await Assert.ThrowsAnyAsync<Exception>(() => DocumentationCaptureTests.WaitForLoadedMapAsync(Page.GetByAltText("map"), 250));
    }

    [Fact]
    public async Task BrokenVisibleImageIsRejected()
    {
        await Page.SetContentAsync("<img alt='map' src='data:image/png;base64,broken' style='width:20px;height:20px'>");
        await Assert.ThrowsAnyAsync<Exception>(() => DocumentationCaptureTests.WaitForLoadedMapAsync(Page.GetByAltText("map"), 1_000));
    }
}
