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
            await Page.SetViewportSizeAsync(1920, 1080);
            var runId = CreateRunId();
            var clientName = $"{runId} Preview Client";
            var gigTitle = $"{runId} Preview Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.AddDays(42).ToString("yyyy-MM-dd"));

            await GenerateInvoiceAndWaitForPreviewAsync();
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();

            var passiveMutations = new List<string>();
            void WatchPassiveReads(object? _, IRequest request)
            {
                if (request.Url.Contains("/invoices/", StringComparison.Ordinal) && request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
                    passiveMutations.Add(request.Method);
            }
            Page.Request += WatchPassiveReads;
            try
            {
                var canvas = Page.GetByTestId("invoice-thumbnail-canvas");
                await Assertions.Expect(canvas).ToBeVisibleAsync(new() { Timeout = 30_000 });
                Assert.True(await canvas.EvaluateAsync<bool>("canvas => canvas.width > 0 && canvas.height > canvas.width"));
                Assert.True(await Page.Locator(".record-visual-layout").EvaluateAsync<bool>(
                    "el => el.querySelector('.record-visual-facts').getBoundingClientRect().right <= el.querySelector('.record-visual-rail').getBoundingClientRect().left"));
                Assert.True(await Page.Locator(".invoice-visual-layout").EvaluateAsync<bool>(
                    "el => { const tracks = getComputedStyle(el).gridTemplateColumns.split(' ').map(parseFloat); return tracks.length === 3 && Math.max(...tracks) - Math.min(...tracks) < 1; }"));
                Assert.Empty(passiveMutations);
                var thumbnail = Page.GetByRole(AriaRole.Button, new() { Name = "Open PDF preview for invoice", Exact = false });
                await thumbnail.FocusAsync();
                await thumbnail.PressAsync("Enter");
                await WaitForInvoicePreviewAsync();
                Assert.Empty(passiveMutations);
            }
            finally
            {
                Page.Request -= WatchPassiveReads;
            }
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
            await ExpectNotificationAsync("issued", "success");
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
