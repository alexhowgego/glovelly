using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Glovelly.Uat.Tests;

public sealed class InvoiceLineRefreshWorkflowTests : InvoiceUatTestBase
{
    [Fact]
    public Task LinkedDraftRefreshesGeneratedLinesWithoutDuplicatingOrDroppingManualAdjustments() => RunWithDiagnosticsAsync(
        nameof(LinkedDraftRefreshesGeneratedLinesWithoutDuplicatingOrDroppingManualAdjustments),
        async () =>
        {
            var runId = CreateRunId();
            var clientName = $"{runId} Refresh Client";
            var gigTitle = $"{runId} Refresh Gig";
            var reimbursedExpense = $"{runId} Train";
            var claimableExpense = $"{runId} Parking";
            var adjustmentReason = $"{runId} Manual goodwill adjustment";

            await AuthenticateWithUatSecretAsync();
            await CreateClientAsync(clientName);
            await CreateGigAsync(
                clientName,
                gigTitle,
                DateTime.UtcNow.AddDays(45).ToString("yyyy-MM-dd"),
                fee: "250.00",
                wasDriving: true,
                travelMiles: "18",
                passengerCount: "1",
                expenses:
                [
                    new GigExpense(reimbursedExpense, "21.00"),
                    new GigExpense(claimableExpense, "14.50"),
                ]);

            await GenerateInvoiceAndWaitForPreviewAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();
            await AssertCoreRefreshLinesAsync(reimbursedExpense, claimableExpense);
            await AddManualAdjustmentAsync(adjustmentReason);

            await RedraftSelectedInvoiceAndOpenPreviewAsync();
            await OpenPreviewedInvoiceAsync();
            await OpenInvoiceLinesAsync();
            await AssertCoreRefreshLinesAsync(reimbursedExpense, claimableExpense);
            await AssertInvoiceLineCountAsync(adjustmentReason, 1);

            await OpenGigAsync(gigTitle);
            await Page.GetByTestId("gig-edit-button").ClickAsync();
            await SetExpenseReimbursementAsync(0, "Reimbursed");

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            await AssertInvoiceLineAbsentAsync(reimbursedExpense);
            await AssertInvoiceLineCountAsync(claimableExpense, 1);
            await AssertInvoiceLineCountAsync(adjustmentReason, 1);
            await AssertInvoiceLineTypeCountAsync("PerformanceFee", 1);

            await OpenGigAsync(gigTitle);
            await Page.GetByTestId("gig-edit-button").ClickAsync();
            await SetExpenseReimbursementAsync(0, "Unreimbursed");

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            await AssertInvoiceLineCountAsync(reimbursedExpense, 1);
            await AssertInvoiceLineCountAsync(adjustmentReason, 1);

            await OpenGigAsync(gigTitle);
            await Page.GetByTestId("gig-edit-button").ClickAsync();
            await Page.GetByTestId("gig-driving-checkbox").UncheckAsync();
            await SaveGigAndAcceptLinkedRedraftAsync();

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            await AssertInvoiceLineTypeCountAsync("Mileage", 0);
            await AssertInvoiceLineTypeCountAsync("PassengerMileage", 0);
            await AssertInvoiceLineTypeCountAsync("PerformanceFee", 1);

            await OpenGigAsync(gigTitle);
            await Page.GetByTestId("gig-edit-button").ClickAsync();
            await Page.GetByTestId("gig-driving-checkbox").CheckAsync();
            await Assertions.Expect(Page.GetByTestId("gig-travel-miles-input")).ToHaveValueAsync("18");
            await Assertions.Expect(Page.GetByTestId("gig-passenger-count-input")).ToHaveValueAsync("1");
            await SaveGigAndAcceptLinkedRedraftAsync();

            await OpenLinkedInvoiceFromGigAsync();
            await OpenInvoiceLinesAsync();
            await AssertInvoiceLineTypeCountAsync("Mileage", 1);
            await AssertInvoiceLineTypeCountAsync("PassengerMileage", 1);
            await AssertInvoiceLineTypeCountAsync("PerformanceFee", 1);
            await AssertInvoiceLineCountAsync(reimbursedExpense, 1);
            await AssertInvoiceLineCountAsync(claimableExpense, 1);
            await AssertInvoiceLineCountAsync(adjustmentReason, 1);
        });

    [Fact]
    public Task MileageDefaultsAndEstimationFlowsAreCoveredEndToEnd() => RunWithDiagnosticsAsync(
        nameof(MileageDefaultsAndEstimationFlowsAreCoveredEndToEnd),
        async () =>
        {
            await AuthenticateWithUatSecretAsync();

            try
            {
                await SaveUserMileageSettingsViaUiAsync(mileageRate: string.Empty, passengerMileageRate: string.Empty, travelOriginPostcode: "BS1 5AA");

                var runId = CreateRunId();
                var clientName = $"{runId} Mileage Client";
                var defaultGigTitle = $"{runId} Default Mileage Gig";
                var estimatedGigTitle = $"{runId} Estimated Mileage Gig";
                var fallbackGigTitle = $"{runId} Manual Mileage Gig";
                var originRequiredGigTitle = $"{runId} Origin Required Gig";

                await CreateClientAsync(clientName);
                await CreateGigAsync(
                    clientName,
                    defaultGigTitle,
                    DateTime.UtcNow.AddDays(50).ToString("yyyy-MM-dd"),
                    wasDriving: true,
                    travelMiles: "12",
                    passengerCount: "1");
                await GenerateInvoiceAndWaitForPreviewAsync();
                await DownloadPreviewPdfAsync();
                await OpenPreviewedInvoiceAsync();
                await OpenInvoiceLinesAsync();
                await AssertInvoiceLineTypeCountAsync("Mileage", 1);
                await AssertInvoiceLineTypeCountAsync("PassengerMileage", 1);
                await AssertInvoiceLineContainsAsync("0.45");
                await AssertInvoiceLineContainsAsync("0.10");
                await DownloadInvoicePdfAsync();

                await CreateGigAsync(
                    clientName,
                    estimatedGigTitle,
                    DateTime.UtcNow.AddDays(51).ToString("yyyy-MM-dd"),
                    venue: "Bristol Beacon, Bristol");
                await OpenGigAsync(estimatedGigTitle);
                await Page.GetByTestId("gig-edit-button").ClickAsync();
                await Page.GetByTestId("gig-driving-checkbox").CheckAsync();
                await Page.GetByTestId("gig-estimate-mileage-button").ClickAsync();
                await Assertions.Expect(Page.GetByTestId("gig-travel-miles-input")).Not.ToHaveValueAsync(string.Empty, new LocatorAssertionsToHaveValueOptions
                {
                    Timeout = 60_000,
                });
                var estimatedMiles = await Page.GetByTestId("gig-travel-miles-input").InputValueAsync();
                Assert.True(decimal.Parse(estimatedMiles, CultureInfo.InvariantCulture) > 0, "Expected Google Routes to return positive mileage.");
                await SaveGigAndWaitForResponseAsync();
                await GenerateInvoiceAndWaitForPreviewAsync();
                await OpenPreviewedInvoiceAsync();
                await OpenInvoiceLinesAsync();
                await AssertInvoiceLineTypeCountAsync("Mileage", 1);

                await CreateGigAsync(
                    clientName,
                    fallbackGigTitle,
                    DateTime.UtcNow.AddDays(52).ToString("yyyy-MM-dd"),
                    venue: "The Moon");
                await OpenGigAsync(fallbackGigTitle);
                await Page.GetByTestId("gig-edit-button").ClickAsync();
                await Page.GetByTestId("gig-driving-checkbox").CheckAsync();
                await Page.GetByTestId("gig-estimate-mileage-button").ClickAsync();
                await Assertions.Expect(Page.GetByTestId("gig-status")).ToContainTextAsync(new Regex("unable|route|estimate|address not found", RegexOptions.IgnoreCase), new LocatorAssertionsToContainTextOptions
                {
                    Timeout = 60_000,
                });
                await Page.GetByTestId("gig-travel-miles-input").FillAsync("9");
                await SaveGigAndWaitForResponseAsync();
                await GenerateInvoiceAndWaitForPreviewAsync();
                await OpenPreviewedInvoiceAsync();
                await OpenInvoiceLinesAsync();
                await AssertInvoiceLineTypeCountAsync("Mileage", 1);

                await SaveUserMileageSettingsViaUiAsync(mileageRate: string.Empty, passengerMileageRate: string.Empty, travelOriginPostcode: string.Empty);
                await PutSellerProfileAsync(postcode: string.Empty);
                await CreateGigAsync(
                    clientName,
                    originRequiredGigTitle,
                    DateTime.UtcNow.AddDays(53).ToString("yyyy-MM-dd"),
                    venue: "Bristol Beacon, Bristol");
                await OpenGigAsync(originRequiredGigTitle);
                await Page.GetByTestId("gig-edit-button").ClickAsync();
                await Page.GetByTestId("gig-driving-checkbox").CheckAsync();
                await Page.GetByTestId("gig-travel-miles-input").FillAsync("12");
                await Page.GetByTestId("gig-estimate-mileage-button").ClickAsync();
                await Assertions.Expect(Page.GetByTestId("gig-status")).ToContainTextAsync("travel origin postcode", new LocatorAssertionsToContainTextOptions
                {
                    Timeout = 30_000,
                    IgnoreCase = true,
                });
                await Assertions.Expect(Page.GetByTestId("gig-travel-miles-input")).ToHaveValueAsync("12");
            }
            finally
            {
                await RestoreSharedUatProfileAsync();
            }
        });

    private async Task AssertCoreRefreshLinesAsync(string firstExpense, string secondExpense)
    {
        await AssertInvoiceLineTypeCountAsync("PerformanceFee", 1);
        await AssertInvoiceLineTypeCountAsync("Mileage", 1);
        await AssertInvoiceLineTypeCountAsync("PassengerMileage", 1);
        await AssertInvoiceLineCountAsync(firstExpense, 1);
        await AssertInvoiceLineCountAsync(secondExpense, 1);
    }

    private async Task AddManualAdjustmentAsync(string adjustmentReason)
    {
        await Page.GetByTestId("invoice-adjustment-amount-input").FillAsync("-5.00");
        await Page.GetByTestId("invoice-adjustment-reason-input").FillAsync(adjustmentReason);
        await Page.GetByTestId("invoice-add-adjustment-button").ClickAsync();
        await AssertInvoiceLineCountAsync(adjustmentReason, 1);
    }

    private async Task SetExpenseReimbursementAsync(int expenseIndex, string status)
    {
        Page.Dialog += AcceptReimbursementDialogs;
        try
        {
            await ExpenseRowAt(expenseIndex)
                .GetByTestId("gig-expense-reimbursement-select")
                .SelectOptionAsync(new[] { status });
            await ExpectContainsAsync(Page.GetByTestId("gig-status"), "regenerated");
        }
        finally
        {
            Page.Dialog -= AcceptReimbursementDialogs;
        }

        static void AcceptReimbursementDialogs(object? _, IDialog dialog)
        {
            _ = dialog.Type switch
            {
                "prompt" when dialog.Message.Contains("date", StringComparison.OrdinalIgnoreCase) => dialog.AcceptAsync(DateTime.UtcNow.ToString("yyyy-MM-dd")),
                "prompt" => dialog.AcceptAsync("UAT reimbursement"),
                "confirm" => dialog.AcceptAsync(),
                _ => dialog.DismissAsync(),
            };
        }
    }

    private async Task SaveGigAndAcceptLinkedRedraftAsync()
    {
        Page.Dialog += AcceptLinkedRedraftDialog;
        try
        {
            await Page.GetByTestId("gig-save-close-button").ClickAsync();
            await ExpectContainsAsync(Page.GetByTestId("gig-status"), "regenerated");
        }
        finally
        {
            Page.Dialog -= AcceptLinkedRedraftDialog;
        }

        static void AcceptLinkedRedraftDialog(object? _, IDialog dialog)
        {
            _ = dialog.Type switch
            {
                "confirm" => dialog.AcceptAsync(),
                _ => dialog.DismissAsync(),
            };
        }
    }

    private async Task SaveUserMileageSettingsViaUiAsync(string mileageRate, string passengerMileageRate, string travelOriginPostcode)
    {
        await Page.GetByLabel("Open profile menu").ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Settings" }).ClickAsync();
        await Page.GetByTestId("user-settings-mileage-rate-input").FillAsync(mileageRate);
        await Page.GetByTestId("user-settings-passenger-mileage-rate-input").FillAsync(passengerMileageRate);
        await Page.GetByTestId("user-settings-travel-origin-postcode-input").FillAsync(travelOriginPostcode);
        await Page.GetByTestId("user-settings-save-button").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("user-settings-status")).ToContainTextAsync("updated", new LocatorAssertionsToContainTextOptions
        {
            IgnoreCase = true,
            Timeout = 30_000,
        });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
    }

    private async Task RestoreSharedUatProfileAsync()
    {
        var settingsStatus = await FetchWithSessionAsync("/auth/me/settings", "PUT", new
        {
            mileageRate = 0.45m,
            passengerMileageRate = 0.10m,
            defaultPaymentWindowDays = 14,
            invoiceFilenamePattern = "{invoiceNumber}-{clientName}-{periodDate}",
            invoiceEmailSubjectPattern = "Invoice {invoiceNumber} for {clientName}",
            invoiceReplyToEmail = "regression@glovelly.net",
            travelOriginPostcode = "BS1 5AA",
            invoiceUploadFolderId = (string?)null,
        });
        Assert.True(settingsStatus is >= 200 and < 300, $"Expected settings restore to succeed, got HTTP {settingsStatus}.");

        await PutSellerProfileAsync(postcode: "BS1 5AA");
    }

    private async Task PutSellerProfileAsync(string postcode)
    {
        var sellerStatus = await FetchWithSessionAsync("/seller-profile", "PUT", new
        {
            sellerName = "Glovelly UAT Music",
            addressLine1 = "1 Regression Yard",
            addressLine2 = (string?)null,
            city = "Bristol",
            region = "Bristol",
            postcode,
            country = "United Kingdom",
            email = "regression@glovelly.net",
            phone = "07123 000000",
            accountName = "Glovelly UAT Music",
            sortCode = "00-00-00",
            accountNumber = "00000000",
            paymentReferenceNote = "UAT-only invoice payment reference.",
        });
        Assert.True(sellerStatus is >= 200 and < 300, $"Expected seller profile update to succeed, got HTTP {sellerStatus}.");
    }
}
