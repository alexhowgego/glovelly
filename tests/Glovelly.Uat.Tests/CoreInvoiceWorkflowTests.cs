using Microsoft.Playwright;
using System.Net.Mail;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class CoreInvoiceWorkflowTests : UatTestBase
{
    [Fact]
    public Task AuthenticatedUserCanCreatePreviewAndSendInvoiceToConfiguredRecipient() => RunWithDiagnosticsAsync(
        nameof(AuthenticatedUserCanCreatePreviewAndSendInvoiceToConfiguredRecipient),
        async () =>
        {
            var recipientEmail = ConfiguredInvoiceRecipientEmail();
            var runId = CreateRunId();
            var clientName = $"{runId} Invoice Client";
            var gigTitle = $"{runId} Invoice Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName, recipientEmail);
            await CreateGigAsync(clientName, gigTitle);
            await GenerateInvoiceAndOpenPreviewAsync();
            await OpenPreviewedInvoiceAsync();
            await SendInvoiceAndExpectSuccessAsync(runId);
        });

    private async Task CreateClientAsync(string clientName, string recipientEmail)
    {
        await Page.GetByTestId("nav-clients").ClickAsync();
        await Page.GetByTestId("new-client-button").ClickAsync();
        await Page.GetByTestId("client-form").WaitForAsync();

        await Page.GetByTestId("client-name-input").FillAsync(clientName);
        await Page.GetByTestId("client-email-input").FillAsync(recipientEmail);
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

    private async Task CreateGigAsync(string clientName, string gigTitle)
    {
        await Page.GetByTestId("nav-gigs").ClickAsync();
        await Page.GetByTestId("new-gig-button").ClickAsync();
        await Page.GetByTestId("gig-form").WaitForAsync();

        await Page.GetByTestId("gig-client-select").SelectOptionAsync(new[]
        {
            new SelectOptionValue { Label = clientName },
        });
        await Page.GetByTestId("gig-date-input").FillAsync(DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"));
        await Page.GetByTestId("gig-title-input").FillAsync(gigTitle);
        await Page.GetByTestId("gig-venue-input").FillAsync("UAT Regression Hall");
        await Page.GetByTestId("gig-fee-input").FillAsync("125.00");
        await Page.GetByTestId("gig-save-close-button").ClickAsync();

        await GigCard(gigTitle).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    private async Task GenerateInvoiceAndOpenPreviewAsync()
    {
        await Page.GetByTestId("generate-invoice-button").ClickAsync();
        await Page.GetByTestId("invoice-preview-modal").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await Page.GetByTestId("invoice-preview-frame").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await ExpectContainsAsync(Page.GetByTestId("invoice-preview-status"), "ready to review");
    }

    private async Task OpenPreviewedInvoiceAsync()
    {
        await Page.GetByTestId("open-invoice-button").ClickAsync();
        await Page.GetByTestId("nav-invoices").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await Page.GetByTestId("invoice-send-button").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
    }

    private async Task SendInvoiceAndExpectSuccessAsync(string runId)
    {
        Page.Dialog += (_, dialog) =>
        {
            _ = dialog.Type switch
            {
                "prompt" => dialog.AcceptAsync($"Automated UAT invoice delivery for {runId}."),
                _ => dialog.DismissAsync(),
            };
        };

        await Page.GetByTestId("invoice-send-button").ClickAsync();
        await ExpectContainsAsync(Page.GetByTestId("invoice-status"), "delivered and left as Draft");
    }

    private ILocator ClientCard(string clientName) => Page.GetByTestId("client-card").Filter(new LocatorFilterOptions
    {
        HasText = clientName,
    });

    private ILocator GigCard(string gigTitle) => Page.GetByTestId("gig-card").Filter(new LocatorFilterOptions
    {
        HasText = gigTitle,
    });

    private static async Task ExpectContainsAsync(ILocator locator, string expectedText)
    {
        await Assertions.Expect(locator).ToContainTextAsync(expectedText, new LocatorAssertionsToContainTextOptions
        {
            IgnoreCase = true,
            Timeout = 30_000,
        });
    }

    private static string ConfiguredInvoiceRecipientEmail()
    {
        var recipientEmail = RequiredEnvironmentVariable(
            "GLOVELLY_UAT_INVOICE_RECIPIENT_EMAIL",
            "Set GLOVELLY_UAT_INVOICE_RECIPIENT_EMAIL to the controlled inbox used by the real invoice delivery UAT.");

        try
        {
            _ = new MailAddress(recipientEmail);
            return recipientEmail;
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("GLOVELLY_UAT_INVOICE_RECIPIENT_EMAIL must be a valid email address.", exception);
        }
    }
}
