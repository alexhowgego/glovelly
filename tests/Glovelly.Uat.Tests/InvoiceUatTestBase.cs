using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public abstract class InvoiceUatTestBase : UatTestBase
{
    protected sealed record GigExpense(string Description, string Amount);

    protected async Task CreateClientAsync(string clientName, string email = "invoices-uat@example.com")
    {
        await Page.GetByTestId("nav-clients").ClickAsync();
        await Page.GetByTestId("client-search-input").FillAsync(string.Empty);
        await Page.GetByTestId("new-client-button").ClickAsync();
        await Page.GetByTestId("client-form").WaitForAsync();

        await Page.GetByTestId("client-name-input").FillAsync(clientName);
        await Page.GetByTestId("client-email-input").FillAsync(email);
        await Page.GetByTestId("client-address-line1-input").FillAsync("1 UAT Invoice Street");
        await Page.GetByTestId("client-city-input").FillAsync("Bristol");
        await Page.GetByTestId("client-postal-code-input").FillAsync("BS1 5AA");
        await Page.GetByTestId("client-country-input").FillAsync("United Kingdom");
        await Page.GetByTestId("client-save-close-button").ClickAsync();

        await ClientCard(clientName).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    protected async Task CreateGigAsync(
        string clientName,
        string gigTitle,
        string gigDate,
        string fee = "125.00",
        string venue = "UAT Invoice Hall",
        bool wasDriving = false,
        string travelMiles = "0",
        string passengerCount = "0",
        params GigExpense[] expenses)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("gig-search-input").FillAsync(string.Empty);
        await Page.GetByTestId("new-gig-button").ClickAsync();
        await Page.GetByTestId("gig-form").WaitForAsync();

        await Page.GetByTestId("gig-client-select").SelectOptionAsync(new[]
        {
            new SelectOptionValue { Label = clientName },
        });
        await Page.GetByTestId("gig-date-input").FillAsync(gigDate);
        await Page.GetByTestId("gig-title-input").FillAsync(gigTitle);
        await Page.GetByTestId("gig-venue-input").FillAsync(venue);
        await Page.GetByTestId("gig-fee-input").FillAsync(fee);

        if (wasDriving)
        {
            await Page.GetByTestId("gig-driving-checkbox").CheckAsync();
            await Page.GetByTestId("gig-travel-miles-input").FillAsync(travelMiles);
            await Page.GetByTestId("gig-passenger-count-input").FillAsync(passengerCount);
        }

        foreach (var expense in expenses)
        {
            await Page.GetByTestId("gig-expense-amount-input").FillAsync(expense.Amount);
            await Page.GetByTestId("gig-expense-description-input").FillAsync(expense.Description);
            await Page.GetByTestId("add-gig-expense-button").ClickAsync();
            await ExpenseRow(expense.Description).WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
            });
        }

        await Page.GetByTestId("gig-save-close-button").ClickAsync();
        await GigCard(gigTitle).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    protected async Task GenerateInvoiceAndWaitForPreviewAsync()
    {
        await Page.GetByTestId("generate-invoice-button").ClickAsync();
        await WaitForInvoicePreviewAsync();
    }

    protected async Task WaitForInvoicePreviewAsync()
    {
        await Page.GetByTestId("invoice-preview-modal").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await Page.GetByTestId("invoice-preview-frame").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await ExpectContainsAsync(Page.GetByTestId("invoice-preview-status"), "ready");
    }

    protected async Task OpenPreviewedInvoiceAsync()
    {
        await Page.GetByTestId("open-invoice-button").ClickAsync();
        await Page.GetByTestId("invoice-line-items-button").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    protected async Task OpenInvoiceLinesAsync()
    {
        await Page.GetByTestId("invoice-line-items-button").ClickAsync();
        await Page.GetByTestId("invoice-line-item").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000,
        });
    }

    protected async Task AssertInvoiceLineContainsAsync(string expectedText)
    {
        await InvoiceLine(expectedText).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000,
        });
    }

    protected async Task AssertInvoiceLineCountAsync(string expectedText, int expectedCount)
    {
        await Assertions.Expect(InvoiceLine(expectedText)).ToHaveCountAsync(expectedCount, new LocatorAssertionsToHaveCountOptions
        {
            Timeout = 30_000,
        });
    }

    protected async Task AssertInvoiceLineAbsentAsync(string expectedText)
    {
        await Assertions.Expect(InvoiceLine(expectedText)).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions
        {
            Timeout = 30_000,
        });
    }

    protected async Task AssertInvoiceLineTypeCountAsync(string expectedType, int expectedCount)
    {
        await Assertions.Expect(Page.GetByTestId("invoice-line-type").Filter(new LocatorFilterOptions
        {
            HasTextRegex = new System.Text.RegularExpressions.Regex($"^{System.Text.RegularExpressions.Regex.Escape(expectedType)}$")
        })).ToHaveCountAsync(expectedCount, new LocatorAssertionsToHaveCountOptions
        {
            Timeout = 30_000,
        });
    }

    protected async Task DownloadPreviewPdfAsync()
    {
        var download = await Page.RunAndWaitForDownloadAsync(
            async () => await Page.GetByTestId("invoice-preview-download-button").ClickAsync());

        Assert.EndsWith(".pdf", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
        var path = await download.PathAsync();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(new FileInfo(path).Length > 0, "Expected the invoice preview PDF download to be non-empty.");
    }

    protected async Task DownloadInvoicePdfAsync()
    {
        var download = await Page.RunAndWaitForDownloadAsync(
            async () => await Page.GetByTestId("invoice-download-pdf-button").ClickAsync());

        Assert.EndsWith(".pdf", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
        var path = await download.PathAsync();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(new FileInfo(path).Length > 0, "Expected the invoice PDF download to be non-empty.");
    }

    protected async Task OpenGigFromInvoiceLineAsync(string lineText)
    {
        await InvoiceLine(lineText).GetByTestId("invoice-line-link").ClickAsync();
        await Page.GetByTestId("generate-invoice-button").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    protected async Task OpenGigAsync(string gigTitle)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("gig-search-input").FillAsync(gigTitle);
        await GigCard(gigTitle).ClickAsync();
    }

    protected async Task OpenLinkedInvoiceFromGigAsync()
    {
        await Page.GetByTestId("gig-open-linked-invoice-button").ClickAsync();
        await Page.GetByTestId("invoice-line-items-button").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    protected async Task RedraftSelectedInvoiceAndOpenPreviewAsync()
    {
        await RunAndAcceptConfirmAsync(
            async () => await Page.GetByTestId("invoice-redraft-reissue-button").ClickAsync());
        await WaitForInvoicePreviewAsync();
    }

    protected async Task RunAndAcceptConfirmAsync(Func<Task> action)
    {
        Page.Dialog += AcceptDialog;
        try
        {
            await action();
        }
        finally
        {
            Page.Dialog -= AcceptDialog;
        }

        static void AcceptDialog(object? _, IDialog dialog)
        {
            _ = dialog.AcceptAsync();
        }
    }

    protected async Task ExpectContainsAsync(ILocator locator, string expectedText)
    {
        await Assertions.Expect(locator).ToContainTextAsync(expectedText, new LocatorAssertionsToContainTextOptions
        {
            IgnoreCase = true,
            Timeout = 30_000,
        });
    }

    protected ILocator ClientCard(string clientName) => Page.GetByTestId("client-card").Filter(new LocatorFilterOptions
    {
        HasText = clientName,
    });

    protected ILocator GigCard(string gigTitle) => Page.GetByTestId("gig-card").Filter(new LocatorFilterOptions
    {
        HasText = gigTitle,
    });

    protected ILocator ExpenseRow(string description) => Page.GetByTestId("gig-expense-item").Filter(new LocatorFilterOptions
    {
        HasText = description,
    });

    private ILocator InvoiceLine(string expectedText) => Page.GetByTestId("invoice-line-item").Filter(new LocatorFilterOptions
    {
        HasText = expectedText,
    });
}
