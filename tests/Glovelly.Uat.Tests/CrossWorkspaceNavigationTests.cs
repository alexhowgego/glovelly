using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class CrossWorkspaceNavigationTests : InvoiceUatTestBase
{
    [Fact]
    public Task ClientAndInvoiceLineShortcutsOpenTargetsDespiteStaleFilters() => RunWithDiagnosticsAsync(
        nameof(ClientAndInvoiceLineShortcutsOpenTargetsDespiteStaleFilters),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Shortcut Client";
            var gigTitle = $"{runId} Shortcut Gig";
            var adjustmentReason = $"{runId} Manual adjustment";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.AddDays(28).ToString("yyyy-MM-dd"));
            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();
            await AddManualAdjustmentAsync(adjustmentReason);

            await Page.GetByTestId("nav-clients").ClickAsync();
            await Page.GetByTestId("client-search-input").FillAsync("stale-filter-that-hides-target");
            await OpenGigAsync(gigTitle);
            await Page.GetByTestId("gig-client-link").ClickAsync();
            await Assertions.Expect(ClientCard(clientName)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = clientName })).ToBeVisibleAsync();

            await Page.GetByTestId("client-search-input").FillAsync("another-stale-filter");
            await Page.GetByTestId("nav-invoices").ClickAsync();
            await Page.GetByTestId("invoice-client-link").ClickAsync();
            await Assertions.Expect(ClientCard(clientName)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = clientName })).ToBeVisibleAsync();

            await Page.GetByTestId("nav-gigs").ClickAsync();
            await Page.GetByTestId("gig-search-input").FillAsync("stale-gig-filter");
            await Page.GetByTestId("nav-invoices").ClickAsync();
            await OpenInvoiceLinesAsync();
            await Page.GetByTestId("invoice-line-item").Filter(new LocatorFilterOptions
            {
                HasText = gigTitle,
            }).GetByTestId("invoice-line-link").ClickAsync();
            await Assertions.Expect(GigCard(gigTitle)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = gigTitle })).ToBeVisibleAsync();

            await Page.GetByTestId("nav-invoices").ClickAsync();
            await OpenInvoiceLinesAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-line-item").Filter(new LocatorFilterOptions
            {
                HasText = adjustmentReason,
            }).GetByTestId("invoice-line-link")).ToHaveCountAsync(0);
        });
}
