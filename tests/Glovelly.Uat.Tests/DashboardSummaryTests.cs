using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class DashboardSummaryTests : InvoiceUatTestBase
{
    [Fact]
    public Task DashboardShowsContextSpecificCardsAndInvoiceDrillDown() => RunWithDiagnosticsAsync(
        nameof(DashboardShowsContextSpecificCardsAndInvoiceDrillDown),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Dashboard Client";
            var completedGigTitle = $"000 {runId} Completed Dashboard Gig";
            var upcomingGigTitle = $"000 {runId} Upcoming Dashboard Gig";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(
                clientName,
                completedGigTitle,
                DateTime.UtcNow.ToString("yyyy-MM-dd"),
                fee: "187.00",
                status: "Completed");
            await CreateGigAsync(
                clientName,
                upcomingGigTitle,
                DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
                fee: "187.00",
                status: "Confirmed");

            await Assertions.Expect(Page.GetByTestId("dashboard-upcoming-gigs")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("dashboard-awaiting-confirmation")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("dashboard-completed-uninvoiced")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            await Page.GetByTestId("nav-invoices").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("dashboard-outstanding-balance")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("dashboard-overdue-invoices")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Assertions.Expect(Page.GetByTestId("dashboard-income-this-financial-year")).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            await Page.GetByTestId("dashboard-outstanding-balance").GetByRole(AriaRole.Button, new() { Name = "View invoices" }).ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Outstanding", Exact = true })).ToHaveClassAsync(
                "compact-filter-chip selected",
                new LocatorAssertionsToHaveClassOptions { Timeout = 30_000 });
        });
}
