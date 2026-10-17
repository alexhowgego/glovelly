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
            await Page.GetByTestId("gig-attachment-type-select").SelectOptionAsync("Url");
            await Page.GetByTestId("gig-attachment-title-input").FillAsync(attachmentTitle);
            await Page.GetByTestId("gig-attachment-url-input").FillAsync("ftp://example.com/contract");
            await Page.GetByText("Add attachment", new PageGetByTextOptions { Exact = true }).Last.ClickAsync();
            await Assertions.Expect(Page.GetByTestId("gig-attachment-status")).ToContainTextAsync(
                "URL must be an absolute http or https URL.",
                new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
            await Page.GetByTestId("gig-attachment-url-input").FillAsync("https://example.com/contract");
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
    public Task UnifiedResourceMobileFlowKeepsUploadOpenAndOffersOptionalReview() => RunWithDiagnosticsAsync(
        nameof(UnifiedResourceMobileFlowKeepsUploadOpenAndOffersOptionalReview),
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

            await Page.GetByRole(AriaRole.Button, new() { Name = "Add to Glovelly", Exact = true }).ClickAsync();
            var upload = Page.GetByTestId("unified-intake-modal");
            await upload.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^URL") }).ClickAsync();
            await upload.GetByLabel("URL", new() { Exact = true }).FillAsync("https://example.com/uat-gig-plan");
            await upload.GetByRole(AriaRole.Button, new() { Name = "Upload and analyse" }).ClickAsync();
            var gigSelect = Page.GetByTestId("quick-capture-gig-select");
            await Assertions.Expect(gigSelect).ToContainTextAsync(gigTitle);
            var optionValue = await gigSelect.Locator("option").Filter(new LocatorFilterOptions
            {
                HasText = gigTitle,
            }).GetAttributeAsync("value");
            Assert.False(string.IsNullOrWhiteSpace(optionValue), $"Expected quick attachment candidates to include '{gigTitle}'.");
            await gigSelect.SelectOptionAsync(optionValue);
            await upload.GetByLabel("Title", new() { Exact = true }).FillAsync(attachmentTitle);
            await upload.GetByRole(AriaRole.Button, new() { Name = "Attach resource" }).ClickAsync();
            await Assertions.Expect(upload).ToContainTextAsync($"Attached to {gigTitle}", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(Page.GetByTestId("attachment-review-modal")).Not.ToBeVisibleAsync();
            await upload.GetByRole(AriaRole.Button, new() { Name = "Review attachment" }).ClickAsync();
            var review = Page.GetByTestId("attachment-review-modal");
            await review.GetByLabel("Notes", new() { Exact = true }).FillAsync("Reviewed on mobile");
            await review.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
            await Assertions.Expect(review).ToContainTextAsync("Attachment updated.");
            await review.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
            await Assertions.Expect(Page.GetByTestId("gig-attachment-item").Filter(new LocatorFilterOptions
            {
                HasText = attachmentTitle,
            })).ToBeVisibleAsync();
        });

    [Fact]
    public Task UnifiedReceiptMobileFlowSavesThenReviewsTheExactAttachment() => RunWithDiagnosticsAsync(
        nameof(UnifiedReceiptMobileFlowSavesThenReviewsTheExactAttachment),
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

            await Page.GetByTestId("profile-menu-button").ClickAsync();
            await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Settings", Exact = true }).ClickAsync();
            var preference = Page.GetByTestId("user-settings-automatic-receipt-matching-select");
            var originalPreference = await preference.InputValueAsync();
            await preference.SelectOptionAsync("ManualOnly");
            await Page.GetByTestId("user-settings-save-button").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("user-settings-status")).ToContainTextAsync("Settings updated.");
            await Page.GetByRole(AriaRole.Dialog, new() { Name = "Your settings" }).GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Add to Glovelly", Exact = true }).ClickAsync();
            var upload = Page.GetByTestId("unified-intake-modal");
            await upload.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Photo or file") }).ClickAsync();
            await upload.Locator("input[type=file]").SetInputFilesAsync(fixture);
            var gigSelect = Page.GetByTestId("quick-capture-gig-select");
            var optionValue = await gigSelect.Locator("option").Filter(new LocatorFilterOptions
            {
                HasText = gigTitle,
            }).GetAttributeAsync("value");
            Assert.False(string.IsNullOrWhiteSpace(optionValue), $"Expected quick receipt candidates to include '{gigTitle}'.");
            await gigSelect.SelectOptionAsync(optionValue);
            await upload.GetByRole(AriaRole.Button, new() { Name = "Attach receipt" }).ClickAsync();
            await Assertions.Expect(upload).ToContainTextAsync($"Attached to {gigTitle}", new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(Page.GetByTestId("attachment-review-modal")).Not.ToBeVisibleAsync();
            await upload.GetByRole(AriaRole.Button, new() { Name = "Review attachment" }).ClickAsync();
            var review = Page.GetByTestId("attachment-review-modal");
            await review.GetByLabel("Description", new() { Exact = true }).FillAsync($"{runId} Receipt draft");
            await review.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
            await Assertions.Expect(review).ToContainTextAsync("Attachment updated.");
            await review.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
            await Page.GetByTestId("profile-menu-button").ClickAsync();
            await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Settings", Exact = true }).ClickAsync();
            await preference.SelectOptionAsync(originalPreference);
            await Page.GetByTestId("user-settings-save-button").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("user-settings-status")).ToContainTextAsync("Settings updated.");
            await Page.GetByRole(AriaRole.Dialog, new() { Name = "Your settings" }).GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        });
}
