using Microsoft.Playwright;
using System.Text.Json;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class GigCloningTests : InvoiceUatTestBase
{
    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Draft")]
    public Task CloningGig_CreatesDraftRegardlessOfSourceStatus(string sourceStatus) => RunWithDiagnosticsAsync(
        nameof(CloningGig_CreatesDraftRegardlessOfSourceStatus),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Clone Client";
            var gigTitle = $"{runId} {sourceStatus} Source Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(
                clientName,
                gigTitle,
                DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd"),
                status: sourceStatus);

            var response = await Page.RunAndWaitForResponseAsync(
                async () => await Page.GetByRole(AriaRole.Button, new() { Name = "Clone gig" }).ClickAsync(),
                value => value.Request.Method == "POST" && new Uri(value.Url).AbsolutePath == "/gigs");

            Assert.True(response.Ok, $"Expected gig clone to succeed, got HTTP {response.Status} for {response.Url}.");
            var clonedGig = JsonDocument.Parse(await response.TextAsync()).RootElement;
            Assert.Equal("Draft", clonedGig.GetProperty("status").GetString());
            await Assertions.Expect(Page.GetByTestId("gig-form")).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByTestId("gig-status-select")).ToHaveValueAsync("Draft");
        });
}
