using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class InvoicePreviewWorkflowTests : InvoiceUatTestBase
{
    [Fact]
    public Task InvoicePreviewDownloadRedraftAndReissueShowLatestPdf() => RunWithDiagnosticsAsync(
        nameof(InvoicePreviewDownloadRedraftAndReissueShowLatestPdf),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Preview Client";
            var gigTitle = $"{runId} Preview Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.AddDays(42).ToString("yyyy-MM-dd"));

            await GenerateInvoiceAndWaitForPreviewAsync();
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();

            await Page.GetByTestId("invoice-preview-button").ClickAsync();
            await WaitForInvoicePreviewAsync();
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();

            await RedraftSelectedInvoiceAndOpenPreviewAsync();
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();

            await IssueSelectedInvoiceAsync();
            await RedraftSelectedInvoiceAndOpenPreviewAsync();
            await DownloadPreviewPdfAsync();
        });

    private async Task IssueSelectedInvoiceAsync()
    {
        Page.Dialog += AcceptIssueDialogs;
        try
        {
            await Page.GetByTestId("invoice-status-select").SelectOptionAsync(new[] { "Issued" });
            await Assertions.Expect(Page.GetByTestId("invoice-status")).ToContainTextAsync("issued", new LocatorAssertionsToContainTextOptions
            {
                IgnoreCase = true,
                Timeout = 30_000,
            });
        }
        finally
        {
            Page.Dialog -= AcceptIssueDialogs;
        }

        static void AcceptIssueDialogs(object? _, IDialog dialog)
        {
            _ = dialog.Type switch
            {
                "confirm" => dialog.AcceptAsync(),
                _ => dialog.DismissAsync(),
            };
        }
    }
}
