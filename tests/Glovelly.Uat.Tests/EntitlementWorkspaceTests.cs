using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class EntitlementWorkspaceTests : InvoiceUatTestBase
{
    [Theory]
    [InlineData(390, 844)]
    [InlineData(1280, 800)]
    public Task FreeReportingShowsRestrictionWithoutHidingRecords(int width, int height) => RunWithDiagnosticsAsync(
        $"{nameof(FreeReportingShowsRestrictionWithoutHidingRecords)}_{width}", async () =>
        {
            await Page.SetViewportSizeAsync(width, height);
            await AuthenticateWithUatSecretAsync();
            await Page.RouteAsync("**/entitlements/me", route => route.FulfillAsync(new()
            {
                ContentType = "application/json", Body = JsonSerializer.Serialize(Snapshot(false)),
            }));
            await Page.RouteAsync("**/invoices/paid-income-summary", route => route.FulfillAsync(new()
            {
                Status = 403, ContentType = "application/problem+json",
                Body = """{"code":"feature_not_entitled","feature":"PaidIncomeSummary"}""",
            }));
            await Page.ReloadAsync();
            await Page.GetByTestId("nav-invoices").ClickAsync();
            var summary = Page.GetByTestId("dashboard-income-this-financial-year");
            await Assertions.Expect(summary).ToContainTextAsync("Solo / Pro");
            await Assertions.Expect(summary).ToContainTextAsync("history remains available");
            await Assertions.Expect(Page.GetByText("2 first-issued this month", new() { Exact = false })).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator("[data-sonner-toast][data-type='error']")).ToHaveCountAsync(0);
            await Assertions.Expect(Page.GetByTestId("nav-gigs")).ToBeVisibleAsync();
        });

    [Theory]
    [InlineData(390, 844)]
    [InlineData(1280, 800)]
    public Task LicenceOnlyWorkspaceHasInlineConflictsAndTerminalSuccess(int width, int height) => RunWithDiagnosticsAsync(
        $"{nameof(LicenceOnlyWorkspaceHasInlineConflictsAndTerminalSuccess)}_{width}", async () =>
        {
            await Page.SetViewportSizeAsync(width, height);
            await AuthenticateWithUatSecretAsync();
            var userManagementReads = 0;
            Page.Request += (_, request) =>
            {
                if (new Uri(request.Url).AbsolutePath == "/admin/users") Interlocked.Increment(ref userManagementReads);
            };
            await Page.RouteAsync("**/auth/me", async route =>
            {
                var response = await route.FetchAsync();
                var user = JsonNode.Parse(await response.TextAsync())!.AsObject();
                user["permissions"] = new JsonArray("ManageLicences");
                user["permissionSets"] = new JsonArray("LicenseAdministrator");
                await route.FulfillAsync(new() { Response = response, Body = user.ToJsonString() });
            });
            var target = Guid.NewGuid();
            var changed = false;
            var attempts = 0;
            await Page.RouteAsync("**/admin/licences/summary", route => route.FulfillAsync(new()
            {
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new { freeUsers = changed ? 0 : 1, paidUsers = changed ? 1 : 0, expiringGrants = 0 }),
            }));
            await Page.RouteAsync("**/admin/licences/users**", route =>
            {
                if (route.Request.Method == "PUT")
                {
                    attempts++;
                    if (attempts == 1) return route.FulfillAsync(new() { Status = 409, Body = "{}" });
                    changed = true;
                    return route.FulfillAsync(new() { ContentType = "application/json", Body = """{"result":"Changed"}""" });
                }
                return route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new
                    {
                        users = new[] { new
                        {
                            id = target, email = "licence-fixture@example.test", displayName = "Licence fixture",
                            isActive = true, isEnrolled = true, version = Guid.NewGuid(), entitlements = Snapshot(changed),
                        } },
                        total = 1, page = 1, pageSize = 25,
                    }),
                });
            });
            await Page.RouteAsync("**/admin/licences/audit?*", route =>
                route.FulfillAsync(new() { ContentType = "application/json", Body = "[]" }));
            await Page.ReloadAsync();
            await Assertions.Expect(Page.GetByTestId("nav-admin")).ToHaveCountAsync(0);
            await Page.GetByTestId("nav-licences").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("dashboard-free-accounts").Locator("strong")).ToHaveTextAsync("1");
            await Assertions.Expect(Page.GetByTestId("dashboard-solo-pro-accounts").Locator("strong")).ToHaveTextAsync("0");
            await Assertions.Expect(Page.GetByTestId("dashboard-expiring-grants")).ToContainTextAsync("30 days");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Licence fixture", Exact = true }).ClickAsync();
            var overview = Page.Locator("#licence-overview");
            await Assertions.Expect(overview.Locator(".overview-facts")).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Form, new() { Name = "Update licence" })).ToHaveCountAsync(0);
            await overview.GetByRole(AriaRole.Button, new() { Name = "Edit licence", Exact = true }).ClickAsync();
            var editor = Page.GetByRole(AriaRole.Form, new() { Name = "Update licence", Exact = true });
            await Assertions.Expect(editor).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator(".editor-slot.open > .editor-panel.panel")).ToBeVisibleAsync();
            if (width > 1180)
            {
                Assert.True(await overview.EvaluateAsync<bool>("el => el.scrollHeight <= el.clientHeight + 1"));
                await Assertions.Expect(overview.Locator(".overview-dock")).ToBeInViewportAsync();
                Assert.True(await Page.Locator(".record-workspace").EvaluateAsync<bool>(
                    "el => Math.abs(el.getBoundingClientRect().bottom - document.querySelector('.app-frame').getBoundingClientRect().bottom) <= 2"));
                await Assertions.Expect(Page.Locator(".app-footer")).ToBeInViewportAsync();
            }
            await editor.GetByRole(AriaRole.Combobox, new() { Name = "Plan", Exact = true }).SelectOptionAsync("pro");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save and close", Exact = true }).ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Reload");
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save and close", Exact = true })).ToBeVisibleAsync();
            Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Reload", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Licence fixture", Exact = true }).ClickAsync();
            await overview.GetByRole(AriaRole.Button, new() { Name = "Edit licence", Exact = true }).ClickAsync();
            await Assertions.Expect(editor).ToBeVisibleAsync();
            await editor.GetByRole(AriaRole.Combobox, new() { Name = "Plan", Exact = true }).SelectOptionAsync("pro");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save and close", Exact = true }).ClickAsync();
            await Assertions.Expect(Page.Locator("[data-sonner-toast][data-type='success']")).ToContainTextAsync("Licence saved");
            await Assertions.Expect(Page.GetByRole(AriaRole.Form, new() { Name = "Update licence" })).ToHaveCountAsync(0);
            await Assertions.Expect(overview).ToContainTextAsync("Pro");
            await Assertions.Expect(Page.GetByTestId("dashboard-solo-pro-accounts").Locator("strong")).ToHaveTextAsync("1");
            Assert.Equal(0, userManagementReads);
            Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
        });

    private static object Snapshot(bool paid) => new
    {
        effectivePlan = new { code = paid ? "pro" : "free", displayName = paid ? "Pro" : "Free" },
        assignedPlan = paid ? new { code = "pro", displayName = "Pro" } : null,
        assignmentStatus = paid ? "Active" : "Default",
        endsAtUtc = (string?)null,
        features = new { paidIncomeSummary = paid },
        invoiceIssuance = new
        {
            used = 2, limit = paid ? (int?)null : 2, remaining = paid ? (int?)null : 0,
            periodStartsAtUtc = "2026-11-01T00:00:00Z", resetsAtUtc = "2100-01-01T00:00:00Z", timeZone = "Europe/London",
        },
    };
}
