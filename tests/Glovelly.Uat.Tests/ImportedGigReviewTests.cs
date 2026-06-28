using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class ImportedGigReviewTests : UatTestBase
{
    [Fact]
    public Task ImportedGigModalAutosavesAndCommitsReviewedRows() => RunWithDiagnosticsAsync(
        nameof(ImportedGigModalAutosavesAndCommitsReviewedRows),
        async () =>
        {
            var runId = CreateRunId();
            var sourceName = $"{runId} Import Batch";
            var autosavedTitle = $"{runId} autosaved import title";

            await AuthenticateWithUatSecretAsync();
            var batchId = await CreateGigImportBatchAsync(sourceName);

            await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
            await Page.GetByTestId("profile-menu-button").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
            });

            await OpenImportedGigsModalAsync();
            await Assertions.Expect(Page.GetByTestId("gig-import-batch-card").Filter(new LocatorFilterOptions
            {
                HasText = sourceName,
            })).ToBeVisibleAsync();

            var draftIds = await GetDraftIdsBySourceReferenceAsync(batchId);

            var acceptedRow = ImportRowByDraftId(draftIds["accepted-row"]);
            var rejectedRow = ImportRowByDraftId(draftIds["rejected-row"]);
            var pendingRow = ImportRowByDraftId(draftIds["pending-row"]);

            await acceptedRow.GetByTestId("gig-import-draft-title-input").FillAsync(autosavedTitle);
            await Assertions.Expect(acceptedRow.GetByTestId("gig-import-draft-title-input"))
                .ToHaveValueAsync(autosavedTitle, new LocatorAssertionsToHaveValueOptions { Timeout = 30_000 });
            await acceptedRow.GetByTestId("gig-import-draft-source-reference-input").ClickAsync();
            await WaitForImportTitleAsync(batchId, autosavedTitle);

            await Page.GetByTestId("gig-imports-modal").GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
            await Page.GetByTestId("gig-imports-modal").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 30_000,
            });
            await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load });
            await OpenImportedGigsModalAsync();
            await Assertions.Expect(acceptedRow.GetByTestId("gig-import-draft-title-input"))
                .ToHaveValueAsync(autosavedTitle, new LocatorAssertionsToHaveValueOptions { Timeout = 30_000 });

            await acceptedRow.GetByTestId("gig-import-draft-accept-button").ClickAsync();
            await WaitForImportDraftStatusAsync(batchId, draftIds["accepted-row"], "Accepted");
            await rejectedRow.GetByTestId("gig-import-draft-reject-button").ClickAsync();
            await WaitForImportDraftStatusAsync(batchId, draftIds["rejected-row"], "Rejected");
            await Assertions.Expect(pendingRow).ToBeVisibleAsync();

            await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Commit decisions") }).ClickAsync();
            await Assertions.Expect(Page.GetByTestId("gig-imports-modal")).ToContainTextAsync("created", new LocatorAssertionsToContainTextOptions
            {
                IgnoreCase = true,
                Timeout = 30_000,
            });

            await Page.GetByTestId("gig-imports-modal").GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
            await Page.GetByTestId("nav-gigs").ClickAsync();
            await Page.GetByTestId("gig-search-input").FillAsync(autosavedTitle);
            await Assertions.Expect(Page.GetByTestId("gig-card").Filter(new LocatorFilterOptions
            {
                HasText = autosavedTitle,
            })).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            await OpenImportedGigsModalAsync();
            await Assertions.Expect(pendingRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
            {
                Timeout = 30_000,
            });
            await Assertions.Expect(rejectedRow).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions
            {
                Timeout = 30_000,
            });
        });

    private ILocator ImportRowByDraftId(string draftId) => Page.Locator($"[data-testid='gig-import-draft-row'][data-draft-id='{draftId}']");

    private async Task<Dictionary<string, string>> GetDraftIdsBySourceReferenceAsync(string batchId)
    {
        var batch = await FetchJsonWithSessionAsync($"/gig-imports/{batchId}");
        return batch.GetProperty("drafts")
            .EnumerateArray()
            .ToDictionary(
                draft => draft.GetProperty("sourceReference").GetString() ?? string.Empty,
                draft => draft.GetProperty("draftId").GetString() ?? string.Empty);
    }

    private async Task OpenImportedGigsModalAsync()
    {
        await Page.GetByTestId("profile-menu-button").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000,
        });
        await Page.GetByTestId("profile-menu-button").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("profile-imported-gigs-menuitem")).ToContainTextAsync("Imported gigs");
        await Page.GetByTestId("profile-imported-gigs-menuitem").ClickAsync();
        await Page.GetByTestId("gig-imports-modal").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000,
        });
    }

    private async Task WaitForImportTitleAsync(string batchId, string expectedTitle)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var batch = await FetchJsonWithSessionAsync($"/gig-imports/{batchId}");
            var drafts = batch.GetProperty("drafts").EnumerateArray();
            if (drafts.Any(draft => draft.GetProperty("title").GetString() == expectedTitle))
            {
                return;
            }

            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Expected imported gig autosave to persist title '{expectedTitle}'.");
    }

    private async Task WaitForImportDraftStatusAsync(string batchId, string draftId, string expectedStatus)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var batch = await FetchJsonWithSessionAsync($"/gig-imports/{batchId}");
            var draft = batch.GetProperty("drafts")
                .EnumerateArray()
                .FirstOrDefault(value => value.GetProperty("draftId").GetString() == draftId);

            if (draft.ValueKind != JsonValueKind.Undefined &&
                draft.GetProperty("status").GetString() == expectedStatus)
            {
                return;
            }

            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Expected imported gig draft '{draftId}' to reach status '{expectedStatus}'.");
    }

    private static async Task<string> CreateGigImportBatchAsync(string sourceName)
    {
        var secret = RequiredEnvironmentVariable(
            "GLOVELLY_UAT_SECRET",
            "Set GLOVELLY_UAT_SECRET to create staging UAT imported gig setup data.");
        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/test-auth/gig-import-batches")
        {
            Content = JsonContent.Create(new { sourceName }),
        };
        request.Headers.Add("X-Glovelly-Uat-Secret", secret);

        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Expected imported gig setup to succeed, got HTTP {(int)response.StatusCode}.");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(payload.TryGetProperty("batchId", out _), "Expected setup response to include batchId.");
        return payload.GetProperty("batchId").GetString() ?? throw new InvalidOperationException("Setup response batchId was empty.");
    }
}
