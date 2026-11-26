using Microsoft.Playwright;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class InvoicePreviewWorkflowTests : InvoiceUatTestBase
{
    [Fact]
    public Task UnconfiguredSellerProfileDoesNotRemoveInvoiceThumbnail() => RunWithDiagnosticsAsync(
        nameof(UnconfiguredSellerProfileDoesNotRemoveInvoiceThumbnail), async () =>
        {
            await Page.SetViewportSizeAsync(1920, 800);
            await AuthenticateWithUatSecretAsync();
            // Keep fixture data intact: only vary the account-specific setup
            // notice that reduces the overview body's available height.
            await Page.RouteAsync("**/seller-profile", async route =>
            {
                var response = await route.FetchAsync();
                var profile = JsonNode.Parse(await response.TextAsync())!.AsObject();
                profile["isConfigured"] = false;
                profile["isInvoiceReady"] = false;
                profile["missingFields"] = new JsonArray("sellerName", "addressLine1", "city", "country");
                await route.FulfillAsync(new() { Response = response, Body = profile.ToJsonString() });
            });
            await Page.ReloadAsync();
            var runId = CreateRunId();
            var clientName = $"{runId} Setup Preview Client";
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, $"{runId} Setup Preview Gig", DateTime.UtcNow.AddDays(42).ToString("yyyy-MM-dd"));
            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();

            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Set up seller profile", Exact = true })).ToBeVisibleAsync();
            var thumbnail = Page.GetByRole(AriaRole.Button, new() { Name = "Open PDF preview for invoice", Exact = false });
            await Assertions.Expect(thumbnail).ToBeEnabledAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("invoice-thumbnail-canvas")).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-draft-watermark-hint")).ToBeVisibleAsync();
            Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
            await thumbnail.ClickAsync();
            await WaitForInvoicePreviewAsync();
        });

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

            var generatedResponse = Page.WaitForResponseAsync(response =>
                response.Url.EndsWith("/generate-invoice", StringComparison.Ordinal) && response.Request.Method == "POST");
            await GenerateInvoiceAndWaitForPreviewAsync();
            var generated = JsonNode.Parse(await (await generatedResponse).TextAsync())!;
            var invoiceId = generated["id"]!.GetValue<string>();
            await AssertStoredWatermarkAsync(invoiceId, expected: true);
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-draft-watermark-hint")).ToBeVisibleAsync();

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

            await IssueWithStablePreviewAsync(invoiceId);
            await Assertions.Expect(Page.GetByTestId("invoice-draft-watermark-hint")).ToHaveCountAsync(0);
            await AssertStoredWatermarkAsync(invoiceId, expected: false);
            await RedraftSelectedInvoiceAndOpenPreviewAsync();
            await AssertStoredWatermarkAsync(invoiceId, expected: true);
            await DownloadPreviewPdfAsync();
            await OpenPreviewedInvoiceAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-draft-watermark-hint")).ToBeVisibleAsync();
            await IssueSelectedInvoiceAsync(invoiceId);
            await AssertStoredWatermarkAsync(invoiceId, expected: false);
            await Page.GetByTestId("invoice-status-select").SelectOptionAsync("Cancelled");
            await ExpectNotificationAsync("Cancelled", "success");
            await Assertions.Expect(Page.GetByTestId("invoice-send-button")).ToBeDisabledAsync();
            await Assertions.Expect(Page.GetByText("Restore this cancelled invoice to Draft and issue it before delivery.", new() { Exact = true })).ToBeVisibleAsync();
            await Page.GetByTestId("invoice-status-select").SelectOptionAsync("Draft");
            await ExpectNotificationAsync("Draft", "success");
            await Assertions.Expect(Page.GetByTestId("invoice-preview-button")).ToBeEnabledAsync();
            await AssertStoredWatermarkAsync(invoiceId, expected: true);
        });

    private async Task IssueWithStablePreviewAsync(string invoiceId)
    {
        var frame = Page.GetByTestId("invoice-thumbnail-frame");
        var thumbnail = Page.GetByRole(AriaRole.Button, new() { Name = "Open PDF preview for invoice", Exact = false });
        await Assertions.Expect(thumbnail).ToBeEnabledAsync(new() { Timeout = 30_000 });
        var before = await frame.BoundingBoxAsync();
        Assert.NotNull(before);
        await frame.EvaluateAsync("element => element.dataset.uatIdentity = 'stable'");
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fulfilled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var forwardedRequests = 0;
        var pattern = $"**/invoices/{invoiceId}/status";
        await Page.RouteAsync(pattern, async route =>
        {
            requested.TrySetResult();
            try
            {
                Interlocked.Increment(ref forwardedRequests);
                var response = await route.FetchAsync();
                await release.Task;
                await route.FulfillAsync(new() { Response = response });
                fulfilled.TrySetResult();
            }
            catch (Exception failure)
            {
                // Surface handler failures through the awaited task, rather
                // than losing them in an asynchronous route callback.
                fulfilled.TrySetException(failure);
            }
        });
        try
        {
            await Page.GetByTestId("invoice-status-select").SelectOptionAsync("Issued");
            await Page.GetByTestId("invoice-issue-confirm-button").ClickAsync();
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await Assertions.Expect(frame).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-updating"));
            await Assertions.Expect(thumbnail).ToBeDisabledAsync();
            var during = await frame.BoundingBoxAsync();
            Assert.NotNull(during);
            Assert.True(Math.Abs(before.Height - during.Height) < 2);
            Assert.True(await Page.GetByTestId("invoice-thumbnail-canvas").EvaluateAsync<bool>("canvas => canvas.width > 0"));
        }
        finally
        {
            release.TrySetResult();
            // Removing interception while FetchAsync is still running can
            // forward the original mutation too, then reject fulfilment with
            // "Route is already handled!". Drain the handler before unroute.
            if (requested.Task.IsCompleted)
            {
                await fulfilled.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            }
            await Page.UnrouteAsync(pattern);
        }
        await ExpectNotificationAsync("Issued", "success");
        await Assertions.Expect(thumbnail).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(frame).ToHaveAttributeAsync("data-uat-identity", "stable");
        var after = await frame.BoundingBoxAsync();
        Assert.NotNull(after);
        Assert.True(Math.Abs(before.Height - after.Height) < 2);
        Assert.Equal(1, Volatile.Read(ref forwardedRequests));
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(390, 844)]
    public Task LegacyDraftRecoveryRequiresExplicitRetryAndPreservesLifecycle(int width, int height) => RunWithDiagnosticsAsync(
        $"{nameof(LegacyDraftRecoveryRequiresExplicitRetryAndPreservesLifecycle)}-{width}x{height}",
        async () =>
        {
            await Page.SetViewportSizeAsync(width, height);
            var runId = CreateRunId();
            var clientName = $"{runId} Legacy Client";
            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, $"{runId} Legacy Gig", DateTime.UtcNow.AddDays(45).ToString("yyyy-MM-dd"));
            var generatedResponse = Page.WaitForResponseAsync(response =>
                response.Url.EndsWith("/generate-invoice", StringComparison.Ordinal) && response.Request.Method == "POST");
            await GenerateInvoiceAndWaitForPreviewAsync();
            var invoice = JsonNode.Parse(await (await generatedResponse).TextAsync())!;
            var id = invoice["id"]!.GetValue<string>();
            var number = invoice["invoiceNumber"]!.GetValue<string>();
            var firstIssued = invoice["firstIssuedUtc"]?.ToJsonString();
            var reissueCount = invoice["reissueCount"]!.GetValue<int>();
            var deliveryCount = invoice["deliveryCount"]!.GetValue<int>();
            invoice["documentState"] = "Failed";
            invoice["isPdfCurrent"] = false;
            invoice["pdfHasDraftWatermark"] = null;
            invoice["documentRevision"] = invoice["documentRevision"]!.GetValue<int>() + 1;
            invoice["documentFailureMessage"] = "Regenerate this invoice PDF to add the DRAFT watermark.";
            var attempts = 0;
            await Page.RouteAsync("**/invoices", route => route.FulfillAsync(new()
            {
                ContentType = "application/json", Body = $"[{invoice.ToJsonString()}]"
            }));
            await Page.RouteAsync($"**/invoices/{id}", route => route.FulfillAsync(new()
            {
                ContentType = "application/json", Body = invoice.ToJsonString()
            }));
            await Page.RouteAsync($"**/invoices/{id}/regenerate-pdf", async route =>
            {
                attempts++;
                if (attempts == 1)
                {
                    invoice["documentFailureMessage"] = "Invoice PDF could not be regenerated. Try again.";
                }
                else
                {
                    invoice["documentState"] = "Current";
                    invoice["isPdfCurrent"] = true;
                    invoice["pdfHasDraftWatermark"] = true;
                    invoice["pdfDocumentRevision"] = invoice["documentRevision"]!.GetValue<int>();
                    invoice["documentFailureMessage"] = null;
                }
                await route.FulfillAsync(new() { ContentType = "application/json", Body = invoice.ToJsonString() });
            });
            await Page.ReloadAsync();
            await Page.GetByTestId("nav-invoices").ClickAsync();
            await Page.GetByTestId("invoice-search-input").FillAsync(number);
            await Page.GetByTestId("invoice-card").Filter(new() { HasText = number }).ClickAsync();
            var hint = Page.GetByTestId("invoice-draft-watermark-hint");
            await Assertions.Expect(hint).ToBeVisibleAsync();
            await Assertions.Expect(hint).ToContainTextAsync("Issue to remove DRAFT");
            Assert.Equal(0, attempts);
            await Assertions.Expect(Page.GetByTestId("invoice-preview-button")).ToBeDisabledAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-download-pdf-button")).ToBeDisabledAsync();
            await Page.GetByTestId("invoice-regenerate-pdf-button").ClickAsync();
            await ExpectNotificationAsync("could not be regenerated", "error");
            await Assertions.Expect(Page.GetByTestId("invoice-regenerate-pdf-button")).ToBeVisibleAsync();
            await Page.GetByTestId("invoice-regenerate-pdf-button").ClickAsync();
            await ExpectNotificationAsync("PDF regenerated", "success");
            await Assertions.Expect(Page.GetByTestId("invoice-preview-button")).ToBeEnabledAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-regenerate-pdf-button")).ToHaveCountAsync(0);
            Assert.Equal(2, attempts);
            Assert.Equal(firstIssued, invoice["firstIssuedUtc"]?.ToJsonString());
            Assert.Equal(reissueCount, invoice["reissueCount"]!.GetValue<int>());
            Assert.Equal(deliveryCount, invoice["deliveryCount"]!.GetValue<int>());
            await AssertStoredWatermarkAsync(id, expected: true);
        });

    private async Task AssertStoredWatermarkAsync(string invoiceId, bool expected)
    {
        var response = await Page.Context.APIRequest.GetAsync($"/invoices/{invoiceId}/pdf");
        Assert.True(response.Ok);
        var content = Encoding.ASCII.GetString(await response.BodyAsync());
        Assert.Equal(expected, content.Contains("(DRAFT)", StringComparison.Ordinal));
        await response.DisposeAsync();
    }

    private async Task IssueSelectedInvoiceAsync(string invoiceId)
    {
        var status = Page.GetByTestId("invoice-status-select");
        await Assertions.Expect(status).ToHaveValueAsync("Draft");
        await status.SelectOptionAsync("Issued");
        var response = await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("invoice-issue-confirm-button").ClickAsync(),
            response => response.Request.Method == "PUT" &&
                new Uri(response.Url).AbsolutePath == $"/invoices/{invoiceId}/status");
        Assert.True(response.Ok, $"Expected issuance to succeed, got HTTP {response.Status}.");
        await Assertions.Expect(status).ToHaveValueAsync("Issued");
        await Assertions.Expect(Page.GetByTestId("invoice-issue-confirmation")).ToBeHiddenAsync();
        await ExpectNotificationAsync("Issued", "success");
    }
}
