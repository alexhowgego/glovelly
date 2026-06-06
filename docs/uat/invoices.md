# Invoice UAT Journeys

## Purpose

Use these journeys when a change may affect invoice creation, generated invoice lines, PDF preview/download, status changes, email delivery, Google Drive publishing, or linked gig behavior.

## Preconditions

- You can sign in.
- At least one client exists.
- You can create or edit gigs for that client.
- If testing delivery, confirm whether email and Google Drive are configured in the test environment.

## Gig To Invoice

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.CoreInvoiceWorkflowTests.AuthenticatedUserCanCreatePreviewAndSendInvoiceToConfiguredRecipient`

### Steps

1. Create a client or choose an existing client.
2. Create a gig with fee, date, venue, status `Planned`, and at least one claimable expense.
3. Save the gig.
4. Generate an invoice from that gig.
5. Open the invoice.
6. Confirm invoice lines include the performance fee and chargeable expenses.
7. Download the PDF.
8. Return to the gig.
9. Confirm the gig shows as invoiced and links back to the invoice.

### Expected Results

Generating an invoice links the gig, creates expected lines, and produces a downloadable PDF.

## Combined Invoice

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.InvoiceAggregationWorkflowTests.CanGenerateCombinedInvoiceForSameClientGigs`

### Steps

1. Create two uninvoiced gigs for the same client.
2. Select both gigs in the gig list.
3. Generate a combined invoice.
4. Confirm the generated invoice preview modal opens.
5. Download the PDF or open the invoice.

### Expected Results

One invoice is created, both gigs are linked, invoice lines are ordered sensibly by gig date and line type, and the generated PDF can be previewed before navigating away.

### Negative Check

1. Select gigs from different clients.
2. Try to generate a combined invoice.

Expected result: the app blocks generation and explains that selected gigs must belong to the same client.

## Monthly Invoice

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.InvoiceAggregationWorkflowTests.CanGenerateMonthlyInvoiceForEligibleClientGigs`

### Steps

1. Create or identify multiple uninvoiced gigs for the same client in the same month.
2. Open the client.
3. Choose the month.
4. Generate a monthly invoice.
5. Confirm the generated invoice preview modal opens.
6. Download the PDF or open the invoice.

### Expected Results

The invoice is created as a draft, linked gigs remain linked, lines are generated after redraft, and the PDF can be previewed and downloaded.

## Invoice Line Refresh Regression

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.InvoiceLineRefreshWorkflowTests.LinkedDraftRefreshesGeneratedLinesWithoutDuplicatingOrDroppingManualAdjustments` and `Glovelly.Uat.Tests.InvoiceLineRefreshWorkflowTests.MileageDefaultsAndEstimationFlowsAreCoveredEndToEnd`

### Preconditions

Use seeded data or freshly created test data. The important shape is a gig linked to a draft invoice, with fee, mileage, and expenses that can be regenerated.

### Basic Refresh

1. Create or identify a gig with a performance fee and at least one chargeable expense.
2. Generate a draft invoice from the gig.
3. Redraft or otherwise regenerate the invoice.
4. Open the invoice lines.

Expected result: generated gig lines are replaced cleanly, not appended repeatedly. There is exactly one generated performance fee for the gig, plus the expected mileage and chargeable expense lines.

### Reimbursement Changes On A Linked Draft

1. Start with a gig linked to a draft invoice and at least two expenses.
2. Mark one expense as `Reimbursed`.
3. Accept the prompt to regenerate the linked draft invoice.
4. Confirm the reimbursed expense is removed from the invoice.
5. Change that same expense back to `Claimable`.
6. Accept the prompt to regenerate the linked draft invoice.
7. Reopen the invoice.

Expected result: the expense is added back exactly once. Existing generated lines are not duplicated, and any manual adjustment lines remain intact.

### Driving And Mileage

1. Start with a gig linked to a draft invoice where `I was driving for this gig` is enabled, travel miles are greater than zero, and passenger count is set if passenger mileage is relevant.
2. Confirm the linked draft invoice includes mileage, and passenger mileage when applicable.
3. Edit the gig and clear `I was driving for this gig`.
4. Save and accept the prompt to regenerate the linked draft invoice.
5. Confirm mileage and passenger mileage lines are removed from the invoice.
6. Edit the same gig again and re-enable `I was driving for this gig`.
7. Confirm the previous travel miles and passenger count are still present in the edit form.
8. Save and accept the prompt to regenerate the linked draft invoice.
9. Reopen the invoice.

Expected result: mileage lines disappear while driving is disabled and return when driving is re-enabled. Toggling driving does not erase previously saved mileage or passenger values.

### App-Level Mileage Defaults

1. Open user settings and clear both mileage rate fields so the user has no personal mileage or passenger mileage defaults.
2. Open or create a client and leave both client mileage rate fields blank so they inherit defaults.
3. Create a gig for that client with `I was driving for this gig` enabled, travel miles greater than zero, and passenger count greater than zero.
4. Generate an invoice from the gig.
5. Open the invoice lines and PDF preview.

Expected result: the invoice includes both mileage and passenger mileage lines using the configured app defaults, not blank or omitted lines. The PDF preview/download shows the same mileage lines as the invoice workspace.

### Estimated Mileage Happy Path

Preconditions:

- Google Routes mileage estimation is configured in the environment.
- User settings have a travel origin postcode, or the seller profile has a postcode and country.

1. Create a gig with a clear venue or location and save it.
2. Edit the saved gig.
3. Enable `I was driving for this gig`.
4. Click `Estimate mileage`.
5. Confirm the travel miles field is filled with a positive value.
6. Optionally adjust the estimated value.
7. Save the gig.
8. Generate an invoice from the gig.
9. Open the invoice lines and PDF preview.

Expected result: the estimate fills the editable travel miles field but does not save until the gig is saved. Mileage estimation uses the travel origin postcode when set, otherwise the seller profile postcode. The generated invoice includes a mileage line using the saved travel miles and the applicable mileage rate.

### Estimated Mileage Manual Fallback

1. Use an environment where Google Routes mileage estimation is not configured, or temporarily use a gig location that cannot be routed (i.e. 'The Moon').
2. Create or edit a saved gig.
3. Enable `I was driving for this gig`.
4. Click `Estimate mileage`.
5. Confirm the app shows a clear error in the gig status area.
6. Manually type a valid travel miles value.
7. Save the gig.
8. Generate an invoice from the gig.

Expected result: failed estimation does not block saving the gig. Manual mileage remains editable and still flows into generated invoice mileage lines.

### Estimated Mileage Origin Requirement

1. Use an environment where Google Routes mileage estimation is configured.
2. Clear the user settings travel origin postcode and seller profile postcode, or use a test user without either value.
3. Open a saved gig with a location.
4. Enable `I was driving for this gig`.
5. Click `Estimate mileage`.

Expected result: the app explains that a travel origin postcode, seller profile postcode, or request origin is required. The existing travel miles value is not overwritten.

## Invoice Preview

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.InvoicePreviewWorkflowTests.InvoicePreviewDownloadRedraftAndReissueShowLatestPdf`, with generation preview also covered by the core and aggregation invoice tests.

### Steps

1. Generate an invoice from a gig or from selected gigs.
2. Confirm the invoice preview modal opens immediately.
3. Download the PDF from the modal.
4. Open the invoice from the modal.
5. Use the invoice pane `Preview` button.

### Expected Results

The same invoice PDF can be previewed reactively from the invoice pane, downloaded from the modal, and opened in the invoice workspace.

### Regeneration Checks

1. Redraft a draft invoice.
2. Confirm the regenerated PDF preview modal opens after redraft.
3. Re-issue an issued invoice.
4. Confirm the regenerated PDF preview modal opens after re-issue.

Expected result: redraft and re-issue update the PDF, preserve the expected invoice history rules, and show the latest PDF in the preview modal.

## Invoice Status And Delivery

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.InvoiceStatusEndpointsTests` and `Glovelly.Api.Tests.InvoiceDeliveryEndpointsTests` cover server-side status and delivery rules; browser prompts and configured email/Drive checks remain manual.

### Steps

1. Open a draft invoice linked to one or more non-cancelled gigs.
2. Issue it.
3. Accept the prompt to mark the linked gig or gigs as completed.
4. Repeat with another linked draft invoice and decline the gig completion prompt.
5. Open another draft invoice and send it by email if a recipient is configured.
6. Optionally include receipt attachments, then accept the prompt to mark the delivered draft as issued.
7. Accept or decline the follow-up linked gig completion prompt.
8. Publish a draft invoice to Google Drive if connected, then repeat the delivered-draft issue prompt check.
9. Re-issue an issued invoice.

### Expected Results

Status transitions are explicit, delivery state is recorded, delivered drafts can be promoted to issued by choice, issuing an invoice can complete linked gigs by choice, declined prompts leave existing invoice/gig state unchanged, PDF remains downloadable, and receipt attachments are included only when requested.

## Notes

- Issued invoices should not be silently changed by later gig edits.
- Draft invoice regeneration should happen only after an explicit user choice.
- Delivery checks depend on test environment configuration. If email or Google Drive is not configured, record that the delivery step was skipped.
