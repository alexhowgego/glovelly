using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class InvoicePromptChoiceTests : InvoiceUatTestBase
{
    [Theory]
    [InlineData(true, "Completed")]
    [InlineData(false, "Planned")]
    public Task IssuingLinkedDraftHonoursLinkedGigCompletionChoice(bool acceptCompletion, string expectedGigStatus) => RunWithDiagnosticsAsync(
        $"{nameof(IssuingLinkedDraftHonoursLinkedGigCompletionChoice)}_{acceptCompletion}",
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Prompt Client {acceptCompletion}";
            var gigTitle = $"{runId} Prompt Gig {acceptCompletion}";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.AddDays(24).ToString("yyyy-MM-dd"));
            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();

            await Page.GetByTestId("invoice-status-select").SelectOptionAsync("Issued");
            var confirmation = Page.GetByTestId("invoice-issue-confirmation");
            await Assertions.Expect(confirmation).ToBeVisibleAsync();
            if (acceptCompletion) await confirmation.GetByRole(AriaRole.Checkbox).CheckAsync();
            await Page.GetByTestId("invoice-issue-confirm-button").ClickAsync();

            await ExpectNotificationAsync("Issued", "success");
            await OpenGigAsync(gigTitle);
            await Assertions.Expect(Page.GetByTestId("selected-gig-status")).ToContainTextAsync(expectedGigStatus, new LocatorAssertionsToContainTextOptions
            {
                Timeout = 30_000,
            });
        });

    [Fact]
    public Task CancellingIssuanceLeavesDraftAndLinkedGigUnchanged() => RunWithDiagnosticsAsync(
        nameof(CancellingIssuanceLeavesDraftAndLinkedGigUnchanged), async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Cancel Issue Client";
            var gigTitle = $"{runId} Cancel Issue Gig";
            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.AddDays(24).ToString("yyyy-MM-dd"));
            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();
            await Page.GetByTestId("invoice-status-select").SelectOptionAsync("Issued");
            var confirmation = Page.GetByTestId("invoice-issue-confirmation");
            await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-status-select")).ToHaveValueAsync("Draft");
            await Assertions.Expect(Page.GetByTestId("invoice-preview-button")).ToBeEnabledAsync();
            await OpenGigAsync(gigTitle);
            await Assertions.Expect(Page.GetByTestId("selected-gig-status")).ToContainTextAsync("Planned");
        });

    [Fact]
    public Task DecliningLinkedDraftRegenerationLeavesExistingInvoiceLinesUnchanged() => RunWithDiagnosticsAsync(
        nameof(DecliningLinkedDraftRegenerationLeavesExistingInvoiceLinesUnchanged),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Decline Redraft Client";
            var gigTitle = $"{runId} Decline Redraft Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, gigTitle, DateTime.UtcNow.AddDays(25).ToString("yyyy-MM-dd"), fee: "125.00");
            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();
            await Assertions.Expect(Page.GetByTestId("invoice-line-item").Filter(new LocatorFilterOptions
            {
                HasText = gigTitle,
            })).ToContainTextAsync("125.00");

            await OpenGigFromInvoiceLineAsync(gigTitle);
            await EnsureGigEditorOpenAsync();
            await Page.GetByTestId("gig-fee-input").FillAsync("250.00");
            await DismissNextDialogAsync(async () => await Page.GetByTestId("gig-save-close-button").ClickAsync());

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            var linkedLine = Page.GetByTestId("invoice-line-item").Filter(new LocatorFilterOptions
            {
                HasText = gigTitle,
            });
            await Assertions.Expect(linkedLine).ToContainTextAsync("125.00");
            await Assertions.Expect(linkedLine).Not.ToContainTextAsync("250.00");
        });
}
