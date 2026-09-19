using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class GigListVisibilityTests : InvoiceUatTestBase
{
    [Fact]
    public Task InvoiceLineNavigationRevealsHistoricalGig() => RunWithDiagnosticsAsync(
        nameof(InvoiceLineNavigationRevealsHistoricalGig),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Historical Link Client";
            var gigTitle = $"{runId} Historical Link Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(
                clientName,
                gigTitle,
                DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd"),
                status: "Completed");
            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();

            await Page.GetByTestId("nav-gigs").ClickAsync();
            await Assertions.Expect(GigCard(gigTitle)).ToHaveCountAsync(0);

            await Page.GetByTestId("nav-invoices").ClickAsync();
            await OpenInvoiceLinesAsync();
            await OpenGigFromInvoiceLineAsync(gigTitle);

            await Assertions.Expect(Page.GetByLabel("Gig filters").GetByRole(AriaRole.Button, new() { Name = "All", Exact = true }))
                .ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(GigCard(gigTitle)).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = gigTitle })).ToBeVisibleAsync();
        });

    [Fact]
    public Task HistoricalVisibilityReconcilesSelection() => RunWithDiagnosticsAsync(
        nameof(HistoricalVisibilityReconcilesSelection),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Visibility Client";
            var activeGig = $"{runId} Active Gig";
            var historicalGig = $"{runId} Historical Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, activeGig, DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd"));
            await CreateGigAsync(
                clientName,
                historicalGig,
                DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd"),
                status: "Draft");

            await EnsureGigEditorOpenAsync();
            await Page.GetByTestId("gig-status-select").SelectOptionAsync("Completed");
            await SaveGigAndWaitForResponseAsync();

            var gigFilters = Page.GetByLabel("Gig filters");
            var upcoming = gigFilters.GetByRole(AriaRole.Button, new() { Name = "Upcoming", Exact = true });
            var all = gigFilters.GetByRole(AriaRole.Button, new() { Name = "All", Exact = true });
            await upcoming.ClickAsync();
            await Assertions.Expect(GigCard(historicalGig)).ToHaveCountAsync(0);
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = activeGig })).ToBeVisibleAsync();

            await Page.GetByTestId("gig-search-input").FillAsync(runId);
            await all.ClickAsync();
            await Assertions.Expect(all).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(GigCard(historicalGig)).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = activeGig })).ToBeVisibleAsync();
        });
}
