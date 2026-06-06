using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class InvoiceAggregationWorkflowTests : InvoiceUatTestBase
{
    [Fact]
    public Task CanGenerateCombinedInvoiceForSameClientGigs() => RunWithDiagnosticsAsync(
        nameof(CanGenerateCombinedInvoiceForSameClientGigs),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Combined Client";
            var otherClientName = $"{runId} Other Combined Client";
            var laterGigTitle = $"{runId} Combined Gig Later";
            var earlierGigTitle = $"{runId} Combined Gig Earlier";
            var otherGigTitle = $"{runId} Different Client Gig";
            var baseDate = DateTime.UtcNow.AddDays(35);

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateClientAsync(otherClientName, "other-invoices-uat@example.com");
            await CreateGigAsync(clientName, laterGigTitle, baseDate.AddDays(5).ToString("yyyy-MM-dd"));
            await CreateGigAsync(clientName, earlierGigTitle, baseDate.ToString("yyyy-MM-dd"));
            await CreateGigAsync(otherClientName, otherGigTitle, baseDate.AddDays(1).ToString("yyyy-MM-dd"));

            await SelectGigForBatchInvoiceAsync(laterGigTitle);
            await SelectGigForBatchInvoiceAsync(earlierGigTitle);

            var otherClientCheckbox = GigCard(otherGigTitle).Locator("input[type=checkbox]");
            await Assertions.Expect(otherClientCheckbox).ToBeDisabledAsync();
            await Assertions.Expect(GigCard(otherGigTitle)).ToContainTextAsync("Different client");

            await Page.GetByTestId("generate-invoice-button").ClickAsync();
            await WaitForInvoicePreviewAsync();
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();
            await AssertInvoiceLineContainsAsync(earlierGigTitle);
            await AssertInvoiceLineContainsAsync(laterGigTitle);
            await AssertLinesAreOrderedAsync(earlierGigTitle, laterGigTitle);
            await DownloadInvoicePdfAsync();
        });

    [Fact]
    public Task CanGenerateMonthlyInvoiceForEligibleClientGigs() => RunWithDiagnosticsAsync(
        nameof(CanGenerateMonthlyInvoiceForEligibleClientGigs),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Monthly Client";
            var firstGigTitle = $"{runId} Monthly Gig 1";
            var secondGigTitle = $"{runId} Monthly Gig 2";
            var invoiceMonthDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(2);
            var invoiceMonth = invoiceMonthDate.ToString("yyyy-MM");

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, firstGigTitle, invoiceMonthDate.AddDays(4).ToString("yyyy-MM-dd"));
            await CreateGigAsync(clientName, secondGigTitle, invoiceMonthDate.AddDays(11).ToString("yyyy-MM-dd"));

            await Page.GetByTestId("nav-clients").ClickAsync();
            await ClientCard(clientName).ClickAsync();
            await Page.GetByTestId("monthly-invoice-month-input").FillAsync(invoiceMonth);
            await Assertions.Expect(Page.GetByTestId("monthly-invoice-helper")).ToContainTextAsync(
                "2 eligible gig(s)",
                new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });

            await Page.GetByTestId("generate-monthly-invoice-button").ClickAsync();
            await WaitForInvoicePreviewAsync();
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();
            await AssertInvoiceLineContainsAsync(firstGigTitle);
            await AssertInvoiceLineContainsAsync(secondGigTitle);
            await DownloadInvoicePdfAsync();

            await OpenGigFromInvoiceLineAsync(firstGigTitle);
            await ExpectContainsAsync(Page.GetByTestId("generate-invoice-button"), "Already invoiced");
        });

    private async Task SelectGigForBatchInvoiceAsync(string gigTitle)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("gig-search-input").FillAsync(string.Empty);
        var card = GigCard(gigTitle);
        await card.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await card.Locator("input[type=checkbox]").CheckAsync();
    }

    private async Task AssertLinesAreOrderedAsync(string firstText, string secondText)
    {
        var lineTexts = (await Page.GetByTestId("invoice-line-item").AllInnerTextsAsync()).ToList();
        var firstIndex = lineTexts.FindIndex(text => text.Contains(firstText, StringComparison.Ordinal));
        var secondIndex = lineTexts.FindIndex(text => text.Contains(secondText, StringComparison.Ordinal));

        Assert.True(firstIndex >= 0, $"Expected to find invoice line containing '{firstText}'.");
        Assert.True(secondIndex >= 0, $"Expected to find invoice line containing '{secondText}'.");
        Assert.True(firstIndex < secondIndex, $"Expected '{firstText}' to appear before '{secondText}'.");
    }
}
