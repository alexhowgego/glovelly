using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class InvoiceAggregationWorkflowTests : UatTestBase
{
    [Fact]
    public Task CanGenerateCombinedInvoiceForSameClientGigs() => RunWithDiagnosticsAsync(
        nameof(CanGenerateCombinedInvoiceForSameClientGigs),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Combined Client";
            var firstGigTitle = $"{runId} Combined Gig 1";
            var secondGigTitle = $"{runId} Combined Gig 2";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, firstGigTitle, DateTime.UtcNow.AddDays(21).ToString("yyyy-MM-dd"));
            await CreateGigAsync(clientName, secondGigTitle, DateTime.UtcNow.AddDays(22).ToString("yyyy-MM-dd"));

            await SelectGigForBatchInvoiceAsync(firstGigTitle);
            await SelectGigForBatchInvoiceAsync(secondGigTitle);
            await Page.GetByTestId("generate-invoice-button").ClickAsync();
            await OpenGeneratedInvoicePreviewAsync();
            await AssertInvoiceLinesContainAsync(firstGigTitle, secondGigTitle);
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
            await OpenGeneratedInvoicePreviewAsync();
            await AssertInvoiceLinesContainAsync(firstGigTitle, secondGigTitle);
        });

    private async Task CreateClientAsync(string clientName)
    {
        await Page.GetByTestId("nav-clients").ClickAsync();
        await Page.GetByTestId("new-client-button").ClickAsync();
        await Page.GetByTestId("client-form").WaitForAsync();

        await Page.GetByTestId("client-name-input").FillAsync(clientName);
        await Page.GetByTestId("client-email-input").FillAsync("invoices-uat@example.com");
        await Page.GetByTestId("client-address-line1-input").FillAsync("3 UAT Invoice Street");
        await Page.GetByTestId("client-city-input").FillAsync("Bristol");
        await Page.GetByTestId("client-postal-code-input").FillAsync("BS1 5AA");
        await Page.GetByTestId("client-country-input").FillAsync("United Kingdom");
        await Page.GetByTestId("client-save-close-button").ClickAsync();

        await ClientCard(clientName).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    private async Task CreateGigAsync(string clientName, string gigTitle, string gigDate)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("new-gig-button").ClickAsync();
        await Page.GetByTestId("gig-form").WaitForAsync();

        await Page.GetByTestId("gig-client-select").SelectOptionAsync(new[]
        {
            new SelectOptionValue { Label = clientName },
        });
        await Page.GetByTestId("gig-date-input").FillAsync(gigDate);
        await Page.GetByTestId("gig-title-input").FillAsync(gigTitle);
        await Page.GetByTestId("gig-venue-input").FillAsync("UAT Invoice Hall");
        await Page.GetByTestId("gig-fee-input").FillAsync("125.00");
        await Page.GetByTestId("gig-save-close-button").ClickAsync();

        await GigCard(gigTitle).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    private async Task SelectGigForBatchInvoiceAsync(string gigTitle)
    {
        var card = GigCard(gigTitle);
        await card.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await card.Locator("input[type=checkbox]").CheckAsync();
    }

    private async Task OpenGeneratedInvoicePreviewAsync()
    {
        await Page.GetByTestId("invoice-preview-modal").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await Page.GetByTestId("invoice-preview-frame").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await Page.GetByTestId("open-invoice-button").ClickAsync();
        await Page.GetByTestId("invoice-line-items-button").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    private async Task AssertInvoiceLinesContainAsync(params string[] expectedGigTitles)
    {
        await Page.GetByTestId("invoice-line-items-button").ClickAsync();

        foreach (var expectedGigTitle in expectedGigTitles)
        {
            await Page.GetByTestId("invoice-line-item").Filter(new LocatorFilterOptions
            {
                HasText = expectedGigTitle,
            }).WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000,
            });
        }
    }

    private ILocator ClientCard(string clientName) => Page.GetByTestId("client-card").Filter(new LocatorFilterOptions
    {
        HasText = clientName,
    });

    private ILocator GigCard(string gigTitle) => Page.GetByTestId("gig-card").Filter(new LocatorFilterOptions
    {
        HasText = gigTitle,
    });
}
