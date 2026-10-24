using System.Text.Json.Nodes;
using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class WorkspaceLayoutTests : InvoiceUatTestBase
{
    [Theory]
    [InlineData(1280, 800)]
    [InlineData(1440, 900)]
    public Task DesktopPanesOwnTheirScrollAndKeepControlsReachable(int width, int height) => RunWithDiagnosticsAsync(
        $"{nameof(DesktopPanesOwnTheirScrollAndKeepControlsReachable)}_{width}", async () =>
        {
            await Page.SetViewportSizeAsync(width, height);
            await AuthenticateWithUatSecretAsync();
            var prefix = $"L{Guid.NewGuid().ToString("N")[..6]}";
            await SeedLayoutRecordsAsync(prefix);
            await ConfigureAdminLayoutFixtureAsync(prefix);
            await Page.ReloadAsync();

            foreach (var section in new[] { "clients", "gigs", "invoices", "admin" })
            {
                await Page.GetByTestId($"nav-{section}").ClickAsync();
                await Assertions.Expect(Page.Locator(".workspace-title, .admin-banner")).ToHaveCountAsync(0);
                if (section == "admin")
                {
                    await Assertions.Expect(Page.GetByTestId("dashboard-users-with-access").Locator("strong")).ToHaveTextAsync("32");
                    await Assertions.Expect(Page.GetByTestId("dashboard-active-accounts").Locator("strong")).ToHaveTextAsync("32");
                    await Assertions.Expect(Page.GetByTestId("dashboard-administrators").Locator("strong")).ToHaveTextAsync("0");
                    await Assertions.Expect(Page.GetByTestId("dashboard-upcoming-gigs")).ToHaveCountAsync(0);
                }
                var list = Page.Locator(".compact-record-list");
                var search = Page.Locator(".compact-list-controls input[type=search]");
                await search.FillAsync(prefix);
                await Assertions.Expect(list.Locator("button.compact-record-row")).ToHaveCountAsync(32);
                await list.Locator("button.compact-record-row").First.ClickAsync();
                await AssertBoundedDocumentAsync();
                Assert.True(await Page.Locator(".record-overview").EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight + 1"), $"{section} overview must fit without scrolling");
                Assert.True(await Page.Locator(".record-overview").EvaluateAsync<bool>(
                    "el => !el.querySelector('.overview-dock') || Math.abs(el.querySelector('.overview-dock').getBoundingClientRect().bottom - (el.getBoundingClientRect().bottom - parseFloat(getComputedStyle(el).paddingBottom) - parseFloat(getComputedStyle(el).borderBottomWidth))) <= 2"),
                    $"{section} controls should be docked at the bottom of the pane");
                Assert.True(await list.EvaluateAsync<bool>("el => el.scrollHeight > el.clientHeight && el.clientHeight >= 100"), $"{section} needs a usable internal list viewport");
                await list.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
                await Assertions.Expect(list.Locator("button.compact-record-row").Last).ToBeInViewportAsync();
                await Assertions.Expect(Page.Locator(".compact-list-controls")).ToBeInViewportAsync();
                await Assertions.Expect(list.Locator(".compact-record-header")).ToBeInViewportAsync();

                // The first row was clicked earlier, so blur it before testing
                // a genuine focus transition from outside the scrolled list.
                await search.FocusAsync();
                await Assertions.Expect(search).ToBeFocusedAsync();
                await Assertions.Expect(list.Locator("button.compact-record-row").First).Not.ToBeInViewportAsync();
                await list.Locator("button.compact-record-row").First.FocusAsync();
                await Assertions.Expect(list.Locator("button.compact-record-row").First).ToBeFocusedAsync();
                await Assertions.Expect(list.Locator("button.compact-record-row").First).ToBeInViewportAsync();
                await AssertBoundedDocumentAsync();
                await list.Locator("button.compact-record-row").First.ClickAsync();
                await Page.Locator(".editor-toggle").ClickAsync();
                await Assertions.Expect(Page.Locator(".editor-slot.open")).ToBeVisibleAsync();
                await AssertBoundedDocumentAsync();
                Assert.True(await Page.Locator(".record-overview").EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight + 1"), $"{section} overview must also fit beside its editor");
                var editor = Page.Locator(".editor-slot.open > .panel");
                await editor.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
                Assert.True(await editor.EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight + el.scrollTop + 1"));
                await Page.Locator(".editor-toggle").ClickAsync();
                await AssertBoundedDocumentAsync();

                await search.FillAsync("no-layout-record-matches-this");
                await Assertions.Expect(list.Locator("button.compact-record-row")).ToHaveCountAsync(0);
                await Assertions.Expect(search).ToBeInViewportAsync();
                await AssertBoundedDocumentAsync();
                await search.FillAsync(prefix);
                await Assertions.Expect(list.Locator("button.compact-record-row")).ToHaveCountAsync(32);
                await Assertions.Expect(list.Locator("button.compact-record-row").First).ToBeVisibleAsync();
                await Assertions.Expect(Page.GetByLabel("Dashboard summary")).ToBeInViewportAsync();
            }

            await Page.GetByTestId("nav-gigs").ClickAsync();
            await Page.Locator(".compact-list-controls input[type=search]").FillAsync(prefix);
            await Page.Locator(".compact-record-list button.compact-record-row").First.ClickAsync();
            var detail = Page.Locator("#gig-overview");
            await detail.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
            Assert.True(await detail.EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight + 1 && el.scrollTop === 0"));
            var body = detail.Locator(".overview-body");
            await Assertions.Expect(body).ToContainTextAsync("Long detail content for independent pane scrolling.");
            Assert.True(await body.EvaluateAsync<bool>("el => el.scrollHeight > el.clientHeight"));
            Assert.Equal("left", await body.EvaluateAsync<string>("el => getComputedStyle(el).textAlign"));
            await body.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
            Assert.True(await body.EvaluateAsync<bool>("el => el.scrollTop > 0 && el.scrollTop + el.clientHeight >= el.scrollHeight - 1"));
            await Page.Locator(".compact-record-list button.compact-record-row").Nth(1).ClickAsync();
            await Page.WaitForFunctionAsync("() => document.querySelector('#gig-overview .overview-body').scrollTop === 0");
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Notes and details", Exact = true })).ToHaveCountAsync(0);
            await OpenGigExpensesAsync();
            await Page.GetByTestId("open-gig-expense-dialog-button").ClickAsync();
            var expenseEditor = Page.GetByRole(AriaRole.Dialog, new() { Name = "Add expense", Exact = true });
            await Assertions.Expect(expenseEditor).ToBeFocusedAsync();
            for (var index = 0; index < 10; index++)
            {
                await Page.Keyboard.PressAsync("Tab");
                Assert.True(await expenseEditor.EvaluateAsync<bool>("el => el.contains(document.activeElement)"));
            }
            for (var index = 0; index < 10; index++)
            {
                await Page.Keyboard.PressAsync("Shift+Tab");
                Assert.True(await expenseEditor.EvaluateAsync<bool>("el => el.contains(document.activeElement)"));
            }
            await Page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(expenseEditor).ToHaveCountAsync(0);
            await Assertions.Expect(Page.GetByRole(AriaRole.Dialog, new() { Name = "Gig expenses", Exact = true })).ToBeVisibleAsync();
            await CloseRecordDetailsAsync();
            await AssertBoundedDocumentAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Open profile menu" }).ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Menuitem, new() { Name = "Sign out" })).ToBeVisibleAsync();
        });

    [Fact]
    public Task EmptyWorkspacesDoNotDependOnDetailHeight() => RunWithDiagnosticsAsync(
        nameof(EmptyWorkspacesDoNotDependOnDetailHeight), async () =>
        {
            await Page.SetViewportSizeAsync(1280, 800);
            await AuthenticateWithUatSecretAsync();
            foreach (var path in new[] { "clients", "gigs", "invoices" })
            {
                await Page.RouteAsync($"**/{path}", route => route.FulfillAsync(new() { ContentType = "application/json", Body = "[]" }));
            }
            await Page.ReloadAsync();
            foreach (var section in new[] { "clients", "gigs", "invoices" })
            {
                await Page.GetByTestId($"nav-{section}").ClickAsync();
                await Assertions.Expect(Page.Locator(".compact-record-list .empty-state")).ToBeInViewportAsync();
                await Assertions.Expect(Page.Locator(".compact-list-controls")).ToBeInViewportAsync();
                await AssertBoundedDocumentAsync();
            }
        });

    [Theory]
    [InlineData(390, 844)]
    [InlineData(1280, 600)]
    [InlineData(640, 400)] // Effective CSS viewport of a 1280x800 window at 200% zoom.
    public Task ConstrainedViewportsKeepNaturalFlowAndSettingsAccessible(int width, int height) => RunWithDiagnosticsAsync(
        $"{nameof(ConstrainedViewportsKeepNaturalFlowAndSettingsAccessible)}_{width}_{height}", async () =>
        {
            await Page.SetViewportSizeAsync(width, height);
            await AuthenticateWithUatSecretAsync();
            var clientName = $"{CreateRunId()} Responsive Client";
            await CreateClientAsync(clientName);
            await Page.GetByTestId("client-edit-button").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("client-save-close-button")).ToBeVisibleAsync();
            await Page.GetByTestId("client-save-close-button").FocusAsync();
            await Assertions.Expect(Page.GetByTestId("client-save-close-button")).ToBeInViewportAsync();
            Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
            Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollHeight > innerHeight"));

            await Page.GetByTestId("client-edit-button").ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Contact and billing details", Exact = true })).ToHaveCountAsync(0);
            await CreateGigAsync(clientName, $"{CreateRunId()} Responsive Gig", DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"));
            await OpenGigResourcesAsync();
            var resources = Page.GetByRole(AriaRole.Dialog, new() { Name = "Gig resources", Exact = true });
            Assert.True(await resources.EvaluateAsync<bool>("el => el.getBoundingClientRect().height <= innerHeight && el.getBoundingClientRect().width <= innerWidth"));
            await Page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(Page.GetByTestId("gig-resources-button")).ToBeFocusedAsync();

            await Page.GetByTestId("profile-menu-button").ClickAsync();
            await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Settings", Exact = true }).ClickAsync();
            await Assertions.Expect(Page.Locator(".settings-overlay").First).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator(".settings-overlay").First.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true })).ToBeVisibleAsync();
            var notificationViewport = Page.GetByTestId("notification-layer");
            await Assertions.Expect(notificationViewport).ToHaveCountAsync(1);
            Assert.True(await Page.Locator(".settings-overlay").First.EvaluateAsync<bool>(
                "el => { const notification = document.querySelector('[data-testid=\"notification-layer\"]'); return !!notification && Number(getComputedStyle(el).zIndex) > Number(getComputedStyle(notification).zIndex); }"));
        });

    private Task AssertBoundedDocumentAsync() => Page.WaitForFunctionAsync(
        """
        () => document.documentElement.scrollHeight <= innerHeight + 1
          && document.documentElement.scrollWidth <= innerWidth + 1
          && scrollY === 0
        """);

    private Task SeedLayoutRecordsAsync(string prefix) => Page.EvaluateAsync(
        """
        async (prefix) => {
          const post = async (path, data) => {
            const response = await fetch(path, { method: 'POST', credentials: 'include',
              headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
            if (!response.ok) throw new Error(`${path}: ${response.status} ${await response.text()}`);
            return response.json();
          };
          const date = new Date(Date.now() + 86400000 * 14).toISOString().slice(0, 10);
          for (let index = 0; index < 32; index++) {
            const name = `${prefix} Layout ${String(index).padStart(2, '0')}`;
            const client = await post('/clients', { name, email: 'layout@example.com',
              billingAddress: { line1: 'Layout test address', city: 'Bristol', country: 'United Kingdom' } });
            // Exercise scrolling while staying within PostgreSQL's 4,000-character notes limit.
            await post('/gigs', { clientId: client.id, title: name, date, venue: 'Layout Hall',
              fee: 100, status: 'Confirmed', travelMiles: 0, passengerCount: 0, wasDriving: false,
              notes: 'Long detail content for independent pane scrolling. '.repeat(70) });
            await post('/invoices', { clientId: client.id, invoiceNumber: `${prefix}-${index}`,
              invoiceDate: date, dueDate: date, status: 'Draft', description: 'Layout invoice detail. '.repeat(100) });
          }
        }
        """, prefix);

    // Layout-only admin projection: the staging UAT identity deliberately has
    // no administrator permissions. Never grant it backend access for CSS tests.
    private async Task ConfigureAdminLayoutFixtureAsync(string prefix)
    {
        await Page.RouteAsync("**/auth/me", async route =>
        {
            var response = await route.FetchAsync();
            var user = JsonNode.Parse(await response.TextAsync())!;
            user["role"] = "Admin";
            await route.FulfillAsync(new() { Response = response, Body = user.ToJsonString() });
        });
        await Page.RouteAsync("**/admin/users", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(0, 32).Select(index => new
            {
                id = $"layout-user-{index}", email = $"layout-{index}@example.com", displayName = $"{prefix} Layout User {index:00}",
                googleSubject = (string?)null, isEnrolled = false, role = "User", isActive = true,
                createdUtc = "2026-01-01T00:00:00Z", lastLoginUtc = (string?)null,
            })),
        }));
        await Page.RouteAsync("**/admin/access-requests**", route => route.FulfillAsync(new() { ContentType = "application/json", Body = "[]" }));
    }
}
