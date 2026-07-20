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

    [Fact]
    public Task InvoiceLineNavigationDoesNotReusePreviouslySavedGigEditorState() => RunWithDiagnosticsAsync(
        nameof(InvoiceLineNavigationDoesNotReusePreviouslySavedGigEditorState),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} State Isolation Client";
            var firstGigTitle = $"{runId} State Isolation Gig A";
            var secondGigTitle = $"{runId} State Isolation Gig B";
            var firstGigVenue = "First gig venue";
            var firstGigUpdatedExpense = $"{runId} Updated first expense";
            var secondGigVenue = "Second gig venue";
            var secondGigUpdatedVenue = "Second gig updated venue";
            var firstGigExpense = $"{runId} First expense";
            var secondGigExpense = $"{runId} Second expense";
            var gigDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(
                clientName,
                firstGigTitle,
                gigDate,
                venue: firstGigVenue,
                expenses: [new GigExpense(firstGigExpense, "11.00")]);
            await CreateGigAsync(
                clientName,
                secondGigTitle,
                DateTime.UtcNow.AddDays(31).ToString("yyyy-MM-dd"),
                venue: secondGigVenue,
                expenses: [new GigExpense(secondGigExpense, "22.00")]);

            await SelectGigForBatchInvoiceAsync(firstGigTitle);
            await SelectGigForBatchInvoiceAsync(secondGigTitle);
            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();

            await OpenGigFromInvoiceLineAsync(firstGigTitle);
            await EnsureGigEditorOpenAsync();
            await UpdateExpenseAndAcceptLinkedRedraftAsync(firstGigExpense, firstGigUpdatedExpense);

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            await OpenGigFromInvoiceLineAsync(secondGigTitle);

            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = secondGigTitle })).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByTestId("gig-title-input")).ToHaveValueAsync(secondGigTitle);
            await Assertions.Expect(Page.GetByTestId("gig-venue-input")).ToHaveValueAsync(secondGigVenue);
            await Assertions.Expect(ExpenseRow(secondGigExpense)).ToBeVisibleAsync();
            await Assertions.Expect(ExpenseRow(firstGigUpdatedExpense)).ToHaveCountAsync(0);

            await Page.GetByTestId("gig-venue-input").FillAsync(secondGigUpdatedVenue);
            await SaveGigAndAcceptLinkedRedraftAsync();

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            await OpenGigFromInvoiceLineAsync(firstGigTitle);
            await EnsureGigEditorOpenAsync();
            await Assertions.Expect(Page.GetByTestId("gig-venue-input")).ToHaveValueAsync(firstGigVenue);
            await Assertions.Expect(ExpenseRow(firstGigUpdatedExpense)).ToBeVisibleAsync();
            await Assertions.Expect(ExpenseRow(secondGigExpense)).ToHaveCountAsync(0);

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            await OpenGigFromInvoiceLineAsync(secondGigTitle);
            await EnsureGigEditorOpenAsync();
            await Assertions.Expect(Page.GetByTestId("gig-venue-input")).ToHaveValueAsync(secondGigUpdatedVenue);
            await Assertions.Expect(ExpenseRow(secondGigExpense)).ToBeVisibleAsync();
        });

    private async Task SelectGigForBatchInvoiceAsync(string gigTitle)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("gig-search-input").FillAsync(string.Empty);
        await GigCard(gigTitle).Locator("input[type=checkbox]").CheckAsync();
    }

    private async Task UpdateExpenseAndAcceptLinkedRedraftAsync(string currentDescription, string nextDescription)
    {
        var expense = ExpenseRow(currentDescription);
        await expense.Locator(".associated-item-summary").ClickAsync();
        await expense.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await Page.GetByTestId("gig-expense-description-input").FillAsync(nextDescription);

        Page.Dialog += AcceptLinkedRedraftDialog;
        try
        {
            var redraftResponse = await Page.RunAndWaitForResponseAsync(
                async () => await Page.GetByTestId("add-gig-expense-button").ClickAsync(),
                IsInvoiceRedraftResponse,
                new PageRunAndWaitForResponseOptions { Timeout = 30_000 });
            Assert.True(redraftResponse.Ok, $"Expected invoice redraft to succeed, got HTTP {redraftResponse.Status} for {redraftResponse.Url}.");
        }
        finally
        {
            Page.Dialog -= AcceptLinkedRedraftDialog;
        }

        await Assertions.Expect(ExpenseRow(nextDescription)).ToBeVisibleAsync();
    }

    private async Task SaveGigAndAcceptLinkedRedraftAsync()
    {
        Page.Dialog += AcceptLinkedRedraftDialog;
        try
        {
            var redraftResponse = await Page.RunAndWaitForResponseAsync(
                async () => await Page.GetByTestId("gig-save-close-button").ClickAsync(),
                IsInvoiceRedraftResponse,
                new PageRunAndWaitForResponseOptions { Timeout = 30_000 });
            Assert.True(redraftResponse.Ok, $"Expected invoice redraft to succeed, got HTTP {redraftResponse.Status} for {redraftResponse.Url}.");
        }
        finally
        {
            Page.Dialog -= AcceptLinkedRedraftDialog;
        }
    }

    private ILocator ExpenseRow(string description) => Page.GetByTestId("gig-expense-item").Filter(new LocatorFilterOptions
    {
        HasText = description,
    });

    private static bool IsInvoiceRedraftResponse(IResponse response)
    {
        var path = new Uri(response.Url).AbsolutePath;
        return response.Request.Method == "POST" &&
            path.StartsWith("/invoices/", StringComparison.Ordinal) &&
            path.EndsWith("/redraft", StringComparison.Ordinal);
    }

    private static void AcceptLinkedRedraftDialog(object? _, IDialog dialog)
    {
        _ = dialog.Type == "confirm" ? dialog.AcceptAsync() : dialog.DismissAsync();
    }
}
