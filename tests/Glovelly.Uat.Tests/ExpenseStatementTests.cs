using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class ExpenseStatementTests : UatTestBase
{
    [Fact]
    public Task CanGenerateExpenseStatementPreviewAndDownload() => RunWithDiagnosticsAsync(
        nameof(CanGenerateExpenseStatementPreviewAndDownload),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Expense Client";
            var gigTitle = $"{runId} Expense Gig";
            var expenseDescription = $"{runId} Parking";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigWithExpenseAsync(clientName, gigTitle, expenseDescription);
            await GeneratePreviewAndDownloadAsync(gigTitle, expenseDescription);
        });

    private async Task CreateClientAsync(string clientName)
    {
        await Page.GetByTestId("nav-clients").ClickAsync();
        await Page.GetByTestId("new-client-button").ClickAsync();
        await Page.GetByTestId("client-form").WaitForAsync();

        await Page.GetByTestId("client-name-input").FillAsync(clientName);
        await Page.GetByTestId("client-email-input").FillAsync("expenses-uat@example.com");
        await Page.GetByTestId("client-address-line1-input").FillAsync("2 UAT Expense Street");
        await Page.GetByTestId("client-city-input").FillAsync("Bristol");
        await Page.GetByTestId("client-postal-code-input").FillAsync("BS1 5AA");
        await Page.GetByTestId("client-country-input").FillAsync("United Kingdom");
        await Page.GetByTestId("client-save-close-button").ClickAsync();

        await ClientCard(clientName).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    private async Task CreateGigWithExpenseAsync(string clientName, string gigTitle, string expenseDescription)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("new-gig-button").ClickAsync();
        await Page.GetByTestId("gig-form").WaitForAsync();

        await Page.GetByTestId("gig-client-select").SelectOptionAsync(new[]
        {
            new SelectOptionValue { Label = clientName },
        });
        await Page.GetByTestId("gig-date-input").FillAsync(DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd"));
        await Page.GetByTestId("gig-title-input").FillAsync(gigTitle);
        await Page.GetByTestId("gig-venue-input").FillAsync("UAT Expense Hall");
        await Page.GetByTestId("gig-fee-input").FillAsync("100.00");
        await Page.GetByTestId("gig-expense-amount-input").FillAsync("62.50");
        await Page.GetByTestId("gig-expense-description-input").FillAsync(expenseDescription);
        await Page.GetByTestId("add-gig-expense-button").ClickAsync();
        await Page.GetByTestId("gig-expense-item").Filter(new LocatorFilterOptions
        {
            HasText = expenseDescription,
        }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });

        await Page.GetByTestId("gig-save-close-button").ClickAsync();

        await GigCard(gigTitle).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    private async Task GeneratePreviewAndDownloadAsync(string gigTitle, string expenseDescription)
    {
        await GigCard(gigTitle).ClickAsync();
        await Page.GetByTestId("expense-statement-button").ClickAsync();
        await Page.GetByTestId("expense-statement-modal").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });

        await Assertions.Expect(Page.GetByTestId("expense-statement-modal")).ToContainTextAsync(gigTitle);
        await Assertions.Expect(Page.GetByTestId("expense-statement-expense-row")).ToContainTextAsync(expenseDescription);
        await Assertions.Expect(Page.GetByTestId("expense-statement-total")).ToContainTextAsync("62.50");

        await Page.GetByTestId("expense-statement-preview-button").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("expense-statement-status")).ToContainTextAsync(
            "PDF preview ready",
            new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });
        await Page.GetByTestId("expense-statement-preview-frame").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });

        var download = await Page.RunAndWaitForDownloadAsync(
            async () => await Page.GetByTestId("expense-statement-download-button").ClickAsync());
        var downloadedPath = await download.PathAsync();

        Assert.EndsWith(".pdf", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Expense-Statement-", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(downloadedPath));
        Assert.True(new FileInfo(downloadedPath).Length > 0, "Expected the downloaded expense statement PDF to be non-empty.");
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
