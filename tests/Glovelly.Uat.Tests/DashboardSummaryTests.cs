using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class DashboardSummaryTests : InvoiceUatTestBase
{
    [Fact]
    public Task DashboardHighlightsNextGigOutstandingBalanceAndInvoicePrompt() => RunWithDiagnosticsAsync(
        nameof(DashboardHighlightsNextGigOutstandingBalanceAndInvoicePrompt),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Dashboard Client";
            var gigTitle = $"000 {runId} Dashboard Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(
                clientName,
                gigTitle,
                DateTime.UtcNow.ToString("yyyy-MM-dd"),
                fee: "187.00",
                status: "Completed");

            await Assertions.Expect(Page.GetByTestId("dashboard-next-gig").Locator("strong")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("dashboard-invoice-prompt").Locator("strong")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            var dashboardNextGigTitle = (await Page.GetByTestId("dashboard-next-gig").Locator("strong").InnerTextAsync()).Trim();
            var dashboardInvoicePromptTitle = (await Page.GetByTestId("dashboard-invoice-prompt").Locator("strong").InnerTextAsync()).Trim();

            await Page.GetByTestId("dashboard-open-next-gig-button").ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = dashboardNextGigTitle })).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("selected-gig-status")).ToBeVisibleAsync();

            await Page.GetByTestId("dashboard-generate-invoice-button").ClickAsync();
            await WaitForInvoicePreviewAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();

            var expectedOutstandingBalance = await FormatCurrentOutstandingBalanceAsync();
            await Assertions.Expect(Page.GetByTestId("dashboard-outstanding-balance")).ToContainTextAsync(
                expectedOutstandingBalance,
                new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("dashboard-invoice-prompt")).Not.ToContainTextAsync(
                dashboardInvoicePromptTitle,
                new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });

            await OpenGigAsync(dashboardInvoicePromptTitle);
            await ExpectContainsAsync(Page.GetByTestId("generate-invoice-button"), "Already invoiced");
            await Page.GetByTestId("gig-open-linked-invoice-button").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000,
            });
        });

    private async Task<string> FormatCurrentOutstandingBalanceAsync()
    {
        return await Page.EvaluateAsync<string>(
            """
            async () => {
              const response = await fetch('/invoices', { credentials: 'include' });
              if (!response.ok) {
                throw new Error(`Unable to load invoices: ${response.status}`);
              }

              const invoices = await response.json();
              const total = invoices
                .filter((invoice) => invoice.status !== 'Paid' && invoice.status !== 'Cancelled')
                .reduce((sum, invoice) => sum + invoice.total, 0);

              return new Intl.NumberFormat(undefined, {
                style: 'currency',
                currency: 'GBP',
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
              }).format(total);
            }
            """);
    }
}
