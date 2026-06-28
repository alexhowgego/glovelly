using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class EditorNavigationGuardTests : InvoiceUatTestBase
{
    [Fact]
    public Task ClientEditorRequiresExplicitDiscardBeforeChangingSelection() => RunWithDiagnosticsAsync(
        nameof(ClientEditorRequiresExplicitDiscardBeforeChangingSelection),
        async () =>
        {
            var runId = CreateRunId();
            var firstClient = $"{runId} Dirty Client A";
            var secondClient = $"{runId} Dirty Client B";
            var unsavedName = $"{runId} Unsaved Client Name";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(firstClient);
            await CreateClientAsync(secondClient);

            await Page.GetByTestId("nav-clients").ClickAsync();
            await Page.GetByTestId("client-search-input").FillAsync(string.Empty);
            await ClientCard(firstClient).ClickAsync();
            await Page.GetByTestId("client-edit-button").ClickAsync();
            await Page.GetByTestId("client-name-input").FillAsync(unsavedName);

            await DismissNextDialogAsync(async () => await ClientCard(secondClient).ClickAsync());
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = firstClient })).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByTestId("client-name-input")).ToHaveValueAsync(unsavedName);

            await AcceptNextDialogAsync(async () => await ClientCard(secondClient).ClickAsync());
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = secondClient })).ToBeVisibleAsync();
            await Assertions.Expect(ClientCard(firstClient)).ToBeVisibleAsync();
            await Assertions.Expect(ClientCard(unsavedName)).ToHaveCountAsync(0);
        });

    [Fact]
    public Task GigEditorRequiresExplicitDiscardBeforeChangingSelection() => RunWithDiagnosticsAsync(
        nameof(GigEditorRequiresExplicitDiscardBeforeChangingSelection),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Dirty Gig Client";
            var firstGig = $"{runId} Dirty Gig A";
            var secondGig = $"{runId} Dirty Gig B";
            var unsavedTitle = $"{runId} Unsaved Gig Title";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(clientName, firstGig, DateTime.UtcNow.AddDays(31).ToString("yyyy-MM-dd"));
            await CreateGigAsync(clientName, secondGig, DateTime.UtcNow.AddDays(32).ToString("yyyy-MM-dd"));

            await OpenGigAsync(firstGig);
            await EnsureGigEditorOpenAsync();
            await Page.GetByTestId("gig-title-input").FillAsync(unsavedTitle);

            await Page.GetByTestId("gig-search-input").FillAsync(secondGig);
            await DismissNextDialogAsync(async () => await GigCard(secondGig).ClickAsync());
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = firstGig })).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByTestId("gig-title-input")).ToHaveValueAsync(unsavedTitle);

            await AcceptNextDialogAsync(async () => await GigCard(secondGig).ClickAsync());
            await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = secondGig })).ToBeVisibleAsync();
            await Page.GetByTestId("gig-search-input").FillAsync(string.Empty);
            await Assertions.Expect(GigCard(firstGig)).ToBeVisibleAsync();
            await Assertions.Expect(GigCard(unsavedTitle)).ToHaveCountAsync(0);
        });
}
