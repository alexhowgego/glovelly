using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class ExpenseStatementTests : InvoiceUatTestBase
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

    [Fact]
    public Task ExpenseStatementVariantsRespectReimbursementSelectionAndInvoiceLinks() => RunWithDiagnosticsAsync(
        nameof(ExpenseStatementVariantsRespectReimbursementSelectionAndInvoiceLinks),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Statement Client";
            var otherClientName = $"{runId} Other Statement Client";
            var invoicedGig = $"{runId} Statement Invoiced Gig";
            var openGig = $"{runId} Statement Open Gig";
            var otherClientGig = $"{runId} Statement Other Client Gig";
            var claimableExpense = $"{runId} Claimable Parking";
            var reimbursedExpense = $"{runId} Reimbursed Train";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateClientAsync(otherClientName);
            await CreateGigAsync(
                clientName,
                invoicedGig,
                DateTime.UtcNow.AddDays(16).ToString("yyyy-MM-dd"),
                expenses:
                [
                    new GigExpense(claimableExpense, "20.00"),
                    new GigExpense(reimbursedExpense, "40.00"),
                ]);
            await MarkExpenseReimbursedAsync(reimbursedExpense);
            await GenerateInvoiceAndWaitForPreviewAsync();
            await Page.GetByTestId("invoice-preview-modal").GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();

            await CreateGigAsync(
                clientName,
                openGig,
                DateTime.UtcNow.AddDays(17).ToString("yyyy-MM-dd"),
                expenses: [new GigExpense($"{runId} Open Expense", "15.00")]);
            await CreateGigAsync(
                otherClientName,
                otherClientGig,
                DateTime.UtcNow.AddDays(18).ToString("yyyy-MM-dd"),
                expenses: [new GigExpense($"{runId} Other Expense", "10.00")]);

            var invoiceCountBefore = await CurrentInvoiceCountAsync();

            await SelectGigForStatementAsync(invoicedGig);
            await SelectGigForStatementAsync(openGig);
            await Assertions.Expect(GigCard(otherClientGig).Locator("input[type=checkbox]")).ToBeDisabledAsync();

            await Page.GetByTestId("expense-statement-button").ClickAsync();
            await Page.GetByTestId("expense-statement-modal").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000,
            });
            await Assertions.Expect(Page.GetByTestId("expense-statement-modal")).ToContainTextAsync("Invoiced");
            await Assertions.Expect(ExpenseStatementRow(reimbursedExpense)).ToContainTextAsync("Reimbursed");
            await Assertions.Expect(Page.GetByTestId("expense-statement-total")).ToContainTextAsync("35.00");

            await ExpenseStatementRow(reimbursedExpense).Locator("input[type=checkbox]").CheckAsync();
            await Assertions.Expect(Page.GetByTestId("expense-statement-total")).ToContainTextAsync("75.00");
            await Page.GetByTestId("expense-statement-preview-button").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("expense-statement-status")).ToContainTextAsync("PDF preview ready", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Page.GetByTestId("expense-statement-preview-frame").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000,
            });

            Assert.Equal(invoiceCountBefore, await CurrentInvoiceCountAsync());
            await Page.GetByTestId("expense-statement-modal").GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
            await OpenGigAsync(invoicedGig);
            await Page.GetByTestId("gig-open-linked-invoice-button").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000,
            });
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
        await Page.GetByTestId("gig-save-close-button").ClickAsync();

        await GigCard(gigTitle).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });

        await Page.GetByTestId("open-gig-expense-dialog-button").ClickAsync();
        await Page.GetByTestId("gig-expense-amount-input").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await Page.GetByTestId("gig-expense-amount-input").FillAsync("62.50");
        await Page.GetByTestId("gig-expense-description-input").FillAsync(expenseDescription);
        await Page.GetByTestId("add-gig-expense-button").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("gig-expense-item").Locator("strong").First).ToContainTextAsync(
            expenseDescription,
            new LocatorAssertionsToContainTextOptions
        {
            Timeout = 30_000,
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

    private async Task MarkExpenseReimbursedAsync(string expenseDescription)
    {
        Page.Dialog += AcceptReimbursementDialog;
        try
        {
            var row = Page.GetByTestId("gig-expense-item").Filter(new LocatorFilterOptions
            {
                HasText = expenseDescription,
            });
            await row.Locator(".associated-item-summary").ClickAsync();
            var responseTask = Page.WaitForResponseAsync(response =>
            {
                var path = new Uri(response.Url).AbsolutePath;
                return response.Request.Method == "PATCH" && path.EndsWith("/expenses/reimbursement", StringComparison.Ordinal);
            }, new PageWaitForResponseOptions { Timeout = 30_000 });
            await row.GetByTestId("gig-expense-reimbursement-select").SelectOptionAsync("Reimbursed");
            Assert.True((await responseTask).Ok);
        }
        finally
        {
            Page.Dialog -= AcceptReimbursementDialog;
        }

        static void AcceptReimbursementDialog(object? _, IDialog dialog)
        {
            _ = dialog.Message.Contains("date", StringComparison.OrdinalIgnoreCase)
                ? dialog.AcceptAsync(DateTime.UtcNow.ToString("yyyy-MM-dd"))
                : dialog.AcceptAsync("UAT reimbursement");
        }
    }

    private async Task SelectGigForStatementAsync(string gigTitle)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("gig-search-input").FillAsync(string.Empty);
        var card = GigCard(gigTitle);
        await card.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000,
        });
        await card.Locator("input[type=checkbox]").CheckAsync();
    }

    private ILocator ExpenseStatementRow(string expenseDescription) => Page.GetByTestId("expense-statement-expense-row").Filter(new LocatorFilterOptions
    {
        HasText = expenseDescription,
    });

    private async Task<int> CurrentInvoiceCountAsync()
    {
        var invoices = await FetchJsonWithSessionAsync("/invoices");
        return invoices.GetArrayLength();
    }

}
