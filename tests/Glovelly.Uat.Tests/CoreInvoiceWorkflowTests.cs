using System.Net.Mail;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class CoreInvoiceWorkflowTests : InvoiceUatTestBase
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
            var expenseDescription = $"{runId} Parking";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName, recipientEmail);
            await CreateGigAsync(
                clientName,
                gigTitle,
                DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
                expenses: [new GigExpense(expenseDescription, "32.50")]);

            await GenerateInvoiceAndWaitForPreviewAsync();
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();
            await AssertInvoiceLineContainsAsync(gigTitle);
            await AssertInvoiceLineContainsAsync(expenseDescription);
            await DownloadInvoicePdfAsync();

            await OpenGigFromInvoiceLineAsync(gigTitle);
            await ExpectContainsAsync(Page.GetByTestId("generate-invoice-button"), "Already invoiced");
            await Page.GetByTestId("gig-open-linked-invoice-button").WaitForAsync();
            await OpenLinkedInvoiceFromGigAsync();

            await SendInvoiceAndExpectSuccessAsync(runId);
        });

    private async Task SendInvoiceAndExpectSuccessAsync(string runId)
    {
        Page.Dialog += AcceptInvoiceMessageDialog;
        try
        {
            await Page.GetByTestId("invoice-send-button").ClickAsync();
            await ExpectContainsAsync(Page.GetByTestId("invoice-status"), "delivered and left as Draft");
        }
        finally
        {
            Page.Dialog -= AcceptInvoiceMessageDialog;
        }

        void AcceptInvoiceMessageDialog(object? _, Microsoft.Playwright.IDialog dialog)
        {
            _ = dialog.Type switch
            {
                "prompt" => dialog.AcceptAsync($"Automated UAT invoice delivery for {runId}."),
                _ => dialog.DismissAsync(),
            };
        }
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
