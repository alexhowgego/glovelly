using Microsoft.Playwright;
using System.Text.Json.Nodes;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class WorkspaceDeepLinkTests : UatTestBase
{
    [Theory]
    [InlineData("clients")]
    [InlineData("gigs")]
    [InlineData("invoices")]
    public Task DirectNavigationAndRefreshSelectWorkspace(string section) => RunWithDiagnosticsAsync(
        $"{nameof(DirectNavigationAndRefreshSelectWorkspace)}-{section}", async () =>
        {
            await AuthenticateWithUatSecretAsync();
            await Page.GotoAsync($"/workspaces/{section}");
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", section);
            await Page.ReloadAsync();
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", section);
        });

    [Fact]
    public Task SwitchingReplacesHistoryAndUnknownPathsDefaultToGigs() => RunWithDiagnosticsAsync(
        nameof(SwitchingReplacesHistoryAndUnknownPathsDefaultToGigs), async () =>
        {
            await AuthenticateWithUatSecretAsync();
            await Page.GotoAsync("/workspaces/clients");
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", "clients");
            var length = await Page.EvaluateAsync<int>("history.length");
            await Page.GetByTestId("nav-invoices").ClickAsync();
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", "invoices");
            Assert.EndsWith("/workspaces/invoices", Page.Url);
            Assert.Equal(length, await Page.EvaluateAsync<int>("history.length"));
            await Page.ReloadAsync();
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", "invoices");
            await Page.GotoAsync("/workspaces/unknown");
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", "gigs");
        });

    [Fact]
    public Task NonAdminReviewLinkCanBeDismissedWithoutReopening() => RunWithDiagnosticsAsync(
        nameof(NonAdminReviewLinkCanBeDismissedWithoutReopening), async () =>
        {
            await AuthenticateWithUatSecretAsync();
            await Page.GotoAsync("/access-requests/11111111-1111-1111-1111-111111111111");
            var modal = Page.GetByTestId("access-requests-modal");
            await Assertions.Expect(modal).ToBeVisibleAsync();
            await Assertions.Expect(modal).ToContainTextAsync("Administrator access is required");
            Assert.Contains("/access-requests/", Page.Url);
            await modal.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
            Assert.EndsWith("/workspaces/gigs", Page.Url);
            await Page.ReloadAsync();
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", "gigs");
            await Assertions.Expect(modal).ToHaveCountAsync(0);
        });

    [Fact]
    public Task SignInHandoffPreservesWorkspaceReturnUrl() => RunWithDiagnosticsAsync(
        nameof(SignInHandoffPreservesWorkspaceReturnUrl), async () =>
        {
            await AuthenticateWithUatSecretAsync();
            await Page.EvaluateAsync("fetch('/auth/logout', { method: 'POST', credentials: 'include' })");
            await Page.GotoAsync("/workspaces/invoices");
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Continue with Google" })).ToBeVisibleAsync();
            string? capturedReturn = null;
            await Page.RouteAsync("**/auth/login?**", async route =>
            {
                var query = new Uri(route.Request.Url).Query;
                capturedReturn = Uri.UnescapeDataString(query["?returnUrl=".Length..]);
                var secret = Environment.GetEnvironmentVariable("GLOVELLY_UAT_SECRET") ?? throw new InvalidOperationException("Missing UAT secret.");
                var login = await Page.Context.APIRequest.PostAsync("/test-auth/login", new()
                {
                    Headers = new Dictionary<string, string> { ["X-Glovelly-Uat-Secret"] = secret },
                });
                Assert.Equal(200, login.Status);
                await route.FulfillAsync(new() { Status = 302, Headers = new Dictionary<string, string> { ["Location"] = capturedReturn } });
            });
            await Page.GetByRole(AriaRole.Button, new() { Name = "Continue with Google" }).ClickAsync();
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", "invoices");
            Assert.EndsWith("/workspaces/invoices", capturedReturn);
            Assert.EndsWith("/workspaces/invoices", Page.Url);
        });

    [Fact]
    public Task AdminReviewLinkSelectsRequestAndSurvivesUntilDismissal() => RunWithDiagnosticsAsync(
        nameof(AdminReviewLinkSelectsRequestAndSurvivesUntilDismissal), async () =>
        {
            await AuthenticateWithUatSecretAsync();
            await Page.RouteAsync("**/auth/me", async route =>
            {
                var response = await route.FetchAsync();
                var user = JsonNode.Parse(await response.TextAsync())!;
                user["role"] = "Admin";
                await route.FulfillAsync(new() { Response = response, Body = user.ToJsonString() });
            });
            await Page.RouteAsync("**/admin/users", route => route.FulfillAsync(new() { ContentType = "application/json", Body = "[]" }));
            await Page.RouteAsync("**/admin/access-requests**", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = """[{"id":"11111111-1111-1111-1111-111111111111","email":"review@example.test","displayName":"Review Fixture","requestedAtUtc":"2026-01-01T00:00:00Z","status":"Pending"}]""",
            }));
            await Page.GotoAsync("/access-requests/11111111-1111-1111-1111-111111111111");
            var modal = Page.GetByTestId("access-requests-modal");
            await Assertions.Expect(modal).ToContainTextAsync("review@example.test");
            Assert.Contains("/access-requests/", Page.Url);
            await modal.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
            Assert.EndsWith("/workspaces/gigs", Page.Url);
            await Page.ReloadAsync();
            await Assertions.Expect(Page.Locator("main.workspace-shell")).ToHaveAttributeAsync("data-section", "gigs");
            await Assertions.Expect(modal).ToHaveCountAsync(0);
        });
}
