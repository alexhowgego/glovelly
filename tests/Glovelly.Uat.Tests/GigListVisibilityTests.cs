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
            await Page.GetByTestId("show-past-gigs-button").ClickAsync();
            await Assertions.Expect(GigCard(gigTitle)).ToHaveCountAsync(0);

            await Page.GetByTestId("nav-invoices").ClickAsync();
            await OpenInvoiceLinesAsync();
            await OpenGigFromInvoiceLineAsync(gigTitle);

            await Assertions.Expect(Page.GetByTestId("show-past-gigs-button")).ToHaveAttributeAsync("aria-pressed", "true");
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

            var showPastGigs = Page.GetByTestId("show-past-gigs-button");
            await Assertions.Expect(showPastGigs).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(GigCard(historicalGig)).ToBeVisibleAsync();

            await Page.GetByTestId("gig-search-input").FillAsync(runId);

            await showPastGigs.ClickAsync();
            await Assertions.Expect(GigCard(historicalGig)).ToHaveCountAsync(0);
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = activeGig })).ToBeVisibleAsync();

            await showPastGigs.ClickAsync();
            await Assertions.Expect(GigCard(historicalGig)).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = activeGig })).ToBeVisibleAsync();
        });
}
