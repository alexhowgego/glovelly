using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class UploadAndQuickCaptureWorkflowTests : InvoiceUatTestBase
{
    [Fact]
    public Task BrowserReceiptAndAttachmentUploadsRoundTripThroughGigUi() => RunWithDiagnosticsAsync(
        nameof(BrowserReceiptAndAttachmentUploadsRoundTripThroughGigUi),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Upload Client";
            var gigTitle = $"{runId} Upload Gig";
            var expenseDescription = $"{runId} Parking receipt";
            var attachmentTitle = $"{runId} Contract file";
            var fixture = await CreateTinyPdfFixtureAsync(runId);

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(
                clientName,
                gigTitle,
                DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd"),
                expenses: [new GigExpense(expenseDescription, "12.34")]);

            var expenseRow = Page.GetByTestId("gig-expense-item").Filter(new LocatorFilterOptions
            {
                HasText = expenseDescription,
            });
            await expenseRow.Locator(".associated-item-summary").ClickAsync();
            await expenseRow.GetByTestId("gig-expense-receipt-file-input").SetInputFilesAsync(fixture);
            await Assertions.Expect(expenseRow).ToContainTextAsync("1 receipt", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(expenseRow.GetByTestId("gig-expense-reimbursement-select")).ToHaveValueAsync("Unreimbursed");
            await expenseRow.GetByTestId("gig-expense-receipt-download-button").ClickAsync();
            await expenseRow.GetByTestId("gig-expense-receipt-delete-button").ClickAsync();
            await Assertions.Expect(expenseRow).ToContainTextAsync("0 receipts", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(expenseRow.GetByTestId("gig-expense-reimbursement-select")).ToHaveValueAsync("Unreimbursed");

            await Page.GetByTestId("add-gig-attachment-button").ClickAsync();
            await Page.GetByTestId("gig-attachment-type-select").SelectOptionAsync("File");
            await Page.GetByTestId("gig-attachment-title-input").FillAsync(attachmentTitle);
            await Page.GetByText("Add attachment", new PageGetByTextOptions { Exact = true }).Last.ClickAsync();

            var attachmentRow = Page.GetByTestId("gig-attachment-item").Filter(new LocatorFilterOptions
            {
                HasText = attachmentTitle,
            });
            await Assertions.Expect(attachmentRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await attachmentRow.Locator(".associated-item-summary").ClickAsync();
            await attachmentRow.GetByTestId("gig-attachment-file-input").SetInputFilesAsync(fixture);
            await Assertions.Expect(attachmentRow).ToContainTextAsync("1 file", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await attachmentRow.GetByRole(AriaRole.Button, new() { Name = "Download" }).ClickAsync();
            await AcceptNextDialogAsync(async () => await attachmentRow.GetByLabel(new System.Text.RegularExpressions.Regex("Delete file")).ClickAsync());
            await Assertions.Expect(attachmentRow).ToContainTextAsync("0 files", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(attachmentRow).ToBeVisibleAsync();
        });

    [Fact]
    public Task QuickAttachmentMobileFlowSavesDraftAndOpensTargetGig() => RunWithDiagnosticsAsync(
        nameof(QuickAttachmentMobileFlowSavesDraftAndOpensTargetGig),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Quick Attachment Client";
            var gigTitle = $"!!! 0 {runId} Quick Attachment Gig";
            var attachmentTitle = $"{runId} Quick gig plan";

            await AuthenticateWithUatSecretAsync();
            await Page.SetViewportSizeAsync(390, 844);
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.ToString("yyyy-MM-dd"));

            await Page.GetByTestId("quick-attachment-button").ClickAsync();
            await Page.GetByTestId("quick-attachment-modal").WaitForAsync();
            await Page.GetByTestId("quick-attachment-link-mode-button").ClickAsync();
            await Page.GetByTestId("quick-attachment-url-input").FillAsync("https://example.com/uat-gig-plan");
            var gigSelect = Page.GetByTestId("quick-capture-gig-select");
            await Assertions.Expect(gigSelect).ToContainTextAsync(gigTitle);
            var optionValue = await gigSelect.Locator("option").Filter(new LocatorFilterOptions
            {
                HasText = gigTitle,
            }).GetAttributeAsync("value");
            Assert.False(string.IsNullOrWhiteSpace(optionValue), $"Expected quick attachment candidates to include '{gigTitle}'.");
            await gigSelect.SelectOptionAsync(optionValue);
            await Page.GetByTestId("quick-attachment-save-draft-button").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("quick-attachment-modal")).ToContainTextAsync("Attachment saved", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Page.GetByTestId("quick-attachment-title-input").FillAsync(attachmentTitle);
            await Page.GetByTestId("quick-attachment-save-details-button").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("quick-attachment-modal")).ToContainTextAsync("details saved", new LocatorAssertionsToContainTextOptions
            {
                IgnoreCase = true,
                Timeout = 30_000,
            });
            await Page.GetByTestId("quick-attachment-go-to-gig-button").ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = gigTitle })).ToBeInViewportAsync(new LocatorAssertionsToBeInViewportOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(Page.GetByTestId("gig-attachment-item").Filter(new LocatorFilterOptions
            {
                HasText = attachmentTitle,
            })).ToBeVisibleAsync();
        });

    [Fact]
    public Task QuickReceiptFlowOpensTargetGigInViewport() => RunWithDiagnosticsAsync(
        nameof(QuickReceiptFlowOpensTargetGigInViewport),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Quick Receipt Client";
            var gigTitle = $"!!! 0 {runId} Quick Receipt Gig";
            var fixture = await CreateTinyPdfFixtureAsync(runId);

            await AuthenticateWithUatSecretAsync();
            await Page.SetViewportSizeAsync(390, 844);
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.ToString("yyyy-MM-dd"));

            await Page.GetByTitle("Quick add expense receipt").Locator("input[type=file]").SetInputFilesAsync(fixture);
            await Page.GetByRole(AriaRole.Heading, new() { Name = "Receipt saved" }).WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000,
            });
            var gigSelect = Page.GetByTestId("quick-capture-gig-select");
            var optionValue = await gigSelect.Locator("option").Filter(new LocatorFilterOptions
            {
                HasText = gigTitle,
            }).GetAttributeAsync("value");
            Assert.False(string.IsNullOrWhiteSpace(optionValue), $"Expected quick receipt candidates to include '{gigTitle}'.");
            await gigSelect.SelectOptionAsync(optionValue);
            var quickReceiptModal = Page.GetByTestId("quick-receipt-modal");
            await quickReceiptModal.GetByLabel("Description").FillAsync($"{runId} Receipt draft");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save details" }).ClickAsync();
            await Assertions.Expect(quickReceiptModal).ToContainTextAsync("details saved", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Page.GetByRole(AriaRole.Button, new() { Name = "Go to gig" }).ClickAsync();

            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = gigTitle })).ToBeInViewportAsync(new LocatorAssertionsToBeInViewportOptions
            {
                Timeout = 30_000,
            });
        });
}
