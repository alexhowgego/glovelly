using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class RecordMapPreviewTests : InvoiceUatTestBase
{
    [Fact]
    public Task AutomaticMapsUseDirectImagesAndOmitNarrowRails() => RunWithDiagnosticsAsync(
        nameof(AutomaticMapsUseDirectImagesAndOmitNarrowRails), async () =>
        {
            await MockMapDeliveryAsync();
            await Page.SetViewportSizeAsync(1920, 1080);
            await AuthenticateWithUatSecretAsync();
            var name = $"{CreateRunId()} Map Client";
            await CreateClientAsync(name);
            await AssertSideMapAsync();
            await AssertReadableClientAddressAsync();
            await Assertions.Expect(Page.Locator(".overview-address a")).ToHaveAttributeAsync("target", "_blank");
            await Page.GetByTestId("client-edit-button").ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Checkbox, new() { Name = "Show an automatic map preview", Exact = false })).ToHaveCountAsync(0);
            await Page.GetByTestId("client-address-line1-input").FillAsync("New saved map location");
            await Page.GetByTestId("client-save-close-button").ClickAsync();
            await Assertions.Expect(Page.Locator(".overview-facts")).ToContainTextAsync("New saved map location");
            await AssertSideMapAsync();
            await AssertReadableClientAddressAsync();
            await Page.Context.RouteAsync("https://www.google.com/maps/**", route => route.FulfillAsync(new() { ContentType = "text/html", Body = "Maps navigation fixture" }));
            var mapsLink = Page.Locator(".overview-address a");
            Assert.Equal(await mapsLink.GetAttributeAsync("href"), await Page.Locator(".map-preview-link").GetAttributeAsync("href"));
            await mapsLink.FocusAsync();
            var popup = await Page.Context.RunAndWaitForPageAsync(() => mapsLink.PressAsync("Enter"));
            await popup.WaitForURLAsync("https://www.google.com/maps/**");
            Assert.Contains("maps/search", popup.Url);
            await popup.CloseAsync();
            await CreateGigAsync(name, $"{CreateRunId()} Map Gig", DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd"));
            await AssertSideMapAsync();
            await Page.SetViewportSizeAsync(390, 844);
            await Assertions.Expect(Page.Locator(".record-visual-rail")).ToHaveCountAsync(0);
            await Assertions.Expect(Page.Locator(".overview-facts a[href^='https://www.google.com/maps/search/']")).ToBeVisibleAsync();
            Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
        });

    [Fact]
    public Task ClientAddressRemainsReadableWithoutMapDelivery() => RunWithDiagnosticsAsync(
        nameof(ClientAddressRemainsReadableWithoutMapDelivery), async () =>
        {
            await Page.SetViewportSizeAsync(1920, 1080);
            await Page.RouteAsync("**/map-preview?*", route => route.FulfillAsync(new() { Status = 204 }));
            await AuthenticateWithUatSecretAsync();
            await Page.RunAndWaitForResponseAsync(
                () => CreateClientAsync($"{CreateRunId()} No Map Client"),
                response => response.Url.Contains("/map-preview?", StringComparison.Ordinal));
            await Assertions.Expect(Page.Locator(".record-visual-rail")).ToHaveCountAsync(0);
            await AssertReadableClientAddressAsync();
            await Assertions.Expect(Page.Locator(".overview-address a")).ToHaveAttributeAsync("target", "_blank");
            await Page.SetViewportSizeAsync(390, 844);
            await AssertReadableClientAddressAsync();
            Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
        });

    [Fact]
    public Task NarrowPanesNeverRequestMapDescriptors() => RunWithDiagnosticsAsync(
        nameof(NarrowPanesNeverRequestMapDescriptors), async () =>
        {
            var descriptors = 0;
            await MockMapDeliveryAsync();
            Page.Request += (_, request) =>
            {
                if (request.Url.Contains("/map-preview?", StringComparison.Ordinal)) Interlocked.Increment(ref descriptors);
            };
            await Page.SetViewportSizeAsync(390, 844);
            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync($"{CreateRunId()} Narrow Map Client");
            await Assertions.Expect(Page.Locator(".overview-facts")).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator(".record-visual-rail")).ToHaveCountAsync(0);
            Assert.Equal(0, descriptors);
        });

    private async Task AssertSideMapAsync()
    {
        var image = Page.GetByRole(AriaRole.Img, new() { Name = "Unverified map search preview", Exact = true });
        await Assertions.Expect(image).ToBeVisibleAsync();
        await Assertions.Expect(image).ToHaveAttributeAsync("referrerpolicy", "origin");
        Assert.Equal("0% 50%", await image.EvaluateAsync<string>("el => getComputedStyle(el).objectPosition"));
        Assert.True(await Page.Locator(".record-visual-layout").EvaluateAsync<bool>(
            "el => { const facts = el.querySelector('.overview-facts'); const columns = getComputedStyle(facts).gridTemplateColumns; return getComputedStyle(el).columnGap === '28px' && getComputedStyle(facts).columnGap === '24px' && (el.classList.contains('single-column-facts') ? columns.split(' ').length === 1 : columns.startsWith('subgrid')) && [...el.querySelectorAll('.overview-facts > .full-width:not(.overview-address)')].every(field => getComputedStyle(field).contain.includes('inline-size')); }"));
        Assert.True(await image.EvaluateAsync<bool>("el => { const frame = el.closest('.map-preview-frame').getBoundingClientRect(); return Math.abs(el.naturalWidth / el.naturalHeight - frame.width / frame.height) < .01 && getComputedStyle(el.closest('a')).borderRadius === '12px'; }"));
        Assert.StartsWith("https://maps.googleapis.com/maps/api/staticmap?", await image.GetAttributeAsync("src"));
        await Page.Locator(".map-preview-link").FocusAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Tooltip)).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".map-search-preview figcaption")).ToHaveCountAsync(0);
        Assert.True(await image.EvaluateAsync<bool>("el => { const image = el.getBoundingClientRect(); const body = el.closest('.overview-body').getBoundingClientRect(); return image.height <= body.height && getComputedStyle(el).objectFit === 'contain'; }"));
        Assert.True(await Page.Locator(".record-visual-layout").EvaluateAsync<bool>(
            "el => el.querySelector('.record-visual-facts').getBoundingClientRect().right <= el.querySelector('.record-visual-rail').getBoundingClientRect().left"));
    }

    private async Task AssertReadableClientAddressAsync()
    {
        var address = Page.Locator(".overview-address");
        await Assertions.Expect(address).ToBeVisibleAsync();
        Assert.True(await address.EvaluateAsync<bool>(
            "el => { const dd = el.querySelector('dd'); const lineHeight = parseFloat(getComputedStyle(dd).lineHeight); return getComputedStyle(el).contain === 'none' && dd.clientWidth >= 120 && dd.getBoundingClientRect().height <= lineHeight * 10; }"));
        Assert.True(await Page.Locator(".overview-facts").EvaluateAsync<bool>(
            "el => { const fields = [...el.children].map(field => field.getBoundingClientRect()); return fields.every((field, index) => Math.abs(field.left - fields[0].left) < 1 && (index === 0 || field.top >= fields[index - 1].bottom)); }"));
    }

    private async Task MockMapDeliveryAsync()
    {
        // Synthetic geometry fixture only; never used for documentation screenshots.
        await Page.RouteAsync("https://maps.googleapis.com/**", route =>
        {
            var size = new Uri(route.Request.Url).Query.Split("size=").Last().Split('&')[0].Split('x');
            var width = int.Parse(size[0]);
            var height = int.Parse(size[1]);
            return route.FulfillAsync(new()
            {
                ContentType = "image/svg+xml",
                Body = $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'><rect width='{width}' height='{height}' fill='#eee'/><text x='10' y='{height - 15}'>Map test fixture</text></svg>"
            });
        });
        await Page.RouteAsync("**/map-preview?*", route =>
        {
            var query = new Uri(route.Request.Url).Query.TrimStart('?').Split('&')
                .Select(pair => pair.Split('=', 2)).ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
            return route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, string> { ["Cache-Control"] = "private, no-store" },
                Body = JsonSerializer.Serialize(new
                {
                    url = $"https://maps.googleapis.com/maps/api/staticmap?center=Fixture&key=fixture&signature=fixture&size={query["size"]}",
                    sourceFingerprint = query["source"], size = query["size"]
                })
            });
        });
    }
}
