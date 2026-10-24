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
2. Create a gig with a type, fee, date, location, status `Planned`, and at least one claimable expense.
3. Save the gig.
4. Generate an invoice from that gig.
5. Open the invoice.
6. Confirm invoice lines include the type-appropriate fee description and chargeable expenses.
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

### Fractional Mileage And Zero-Balance Adjustment (#300)

1. Set the client's mileage rate to £0.45 per mile. Create a gig with a £0 fee, driving enabled, no passengers, and no chargeable expenses.
2. Use `Estimate mileage` (the mileage calculation feature). Use an odd-tenth mileage value such as 118.9 miles; adjust the editable result if necessary, then save.
3. Generate an invoice and open its lines and PDF. For 118.9 miles, confirm the mileage line and total both show £53.51.
4. Add a negative adjustment equal to the displayed invoice total (for example, -£53.51).
5. Confirm the saved invoice total and regenerated PDF show £0.00, never -£0.01.
6. Repeat with multiple fractional-mileage lines and confirm the displayed total equals the sum of the displayed line amounts before cancelling it with an adjustment.
7. Repeat the adjustment on a mobile viewport. Confirm the completion notification reports £0.00, does not obscure modal controls, and persistent failure feedback remains available if PDF regeneration fails.

Expected result: each line is rounded to pennies before summing; cancelling the displayed total leaves an exact zero balance. Mileage quantities and rates remain unchanged.

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

### Supplementary first-page thumbnail

1. Select an invoice with a current PDF at a sufficiently wide/tall pane size. Confirm page one appears beside the standard facts, preserves its proportions/white paper background in both themes, and contains no embedded PDF viewer controls.
2. Inspect passive requests: only the existing authenticated PDF GET and a post-download invoice metadata GET are required. No generation, repair, issue/reissue or delivery mutation is allowed. Ordinary UI renders do not repeat the download; revisiting an unmounted invoice may download/render again.
3. Change selection, revision or document readiness during a pending fetch/render. Obsolete work must not fill the new record's canvas. A detected metadata change gives a quiet refresh/reselect explanation without retry loops. A residual concurrent-replacement race is accepted; no immutable storage or reusable PDF-byte cache is assumed.
4. Activate the thumbnail by keyboard to open the existing full preview, which obtains its own PDF bytes. Keep explicit Preview, download, readiness warnings and repair controls working if the thumbnail is missing, oversized, unrenderable or unavailable.
5. Open an editor, shrink/shorten the pane and switch workspaces or end the session. The rail is omitted when ineligible, hidden-only work stops, and bytes/render tasks/canvas resources are released. Facts, headers and docks remain usable without a stacked landscape strip or document overflow.

Run browser rendering checks only in Ubuntu CI. Documentation captures use actual product rendering and wait for supplementary previews to settle in both light/dark modes; do not fabricate thumbnail screenshots.

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

### Manual Adjustment PDF Freshness

1. Open a draft invoice with at least one generated line and open its Line items pane.
2. Add a manual adjustment and confirm the success notification says the PDF was regenerated.
3. Preview or download the PDF and confirm it contains the adjustment and updated total.
4. Remove that adjustment and confirm the success notification says the PDF was regenerated.
5. Preview or download the PDF again and confirm the adjustment is absent and the total is restored.

Expected result: adding and removing manual adjustments always regenerate the current PDF. If regeneration fails, the adjustment change remains saved, document delivery actions are disabled with an explanation, and the inline retry restores the PDF without changing invoice lifecycle history.

### Draft Description Customization

1. Open a draft invoice and open its Line items pane.
2. Change the Description and save it.
3. Confirm the invoice details show the saved description.
4. Redraft the invoice and preview or download the PDF.
5. Confirm the regenerated PDF uses the saved description.
6. Open an issued invoice's Line items pane.

Expected result: only draft invoices expose an editable Description and save action. Saving does not immediately regenerate the PDF; redrafting does. Non-draft invoice descriptions are read-only.

## Invoice Status And Delivery

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.InvoicePromptChoiceTests.IssuingLinkedDraftHonoursLinkedGigCompletionChoice` covers accepting and declining linked-gig completion after issuing; backend tests cover status and delivery rules, while configured email/Drive checks remain manual.

### Steps

1. Open a draft invoice linked to one or more non-cancelled gigs.
2. Issue it.
3. Accept the prompt to mark the linked gig or gigs as completed.
4. Repeat with another linked draft invoice and decline the gig completion prompt.
5. Open another draft invoice and select Send to client; confirm the review dialog shows the recipient, subject, plain-text email preview, and current PDF attachment before sending.
6. Type a multi-line optional message and confirm Enter adds a line rather than sending. Cancel once and confirm no delivery is recorded, then reopen the dialog.
7. Optionally include receipt attachments and select Mark invoice as issued after sending before explicitly selecting Send invoice.
8. Confirm the delivery success notification appears. If the issue or linked-gig follow-up fails, confirm the email success remains visible alongside an actionable error.
9. Publish a draft invoice to Google Drive if connected, then repeat the delivered-draft issue prompt check.
10. Re-issue an issued invoice.

### Expected Results

Status transitions are explicit, email delivery is reviewed before its explicit final action, delivery state is recorded, delivered drafts can be promoted to issued by choice, issuing from the dialog completes linked gigs without another browser confirmation, PDF remains downloadable, and receipt attachments are included only when requested.

## Paid Income Dashboard

> **Automation:** Backend integration tests cover payment-date lifecycle, UK financial-year boundaries, income inclusion, ownership, and total reconciliation. Frontend Vitest coverage verifies the invoice dashboard card and its distinct loading, zero, and error states.

### Steps

1. Open an issued or overdue invoice and mark it as Paid.
2. Open the Invoices workspace and confirm the `Income this financial year` dashboard card includes the payment.
3. Confirm the card displays the current UK financial-year period, from 6 April through 5 April.
4. Select the income card.
5. Confirm the invoice list shows the `Income this financial year` filter and only invoices contributing to the card total.
6. Re-issue the paid invoice.
7. Return to the Invoices workspace and confirm that invoice no longer contributes to the income card or its drill-down.

### Expected Results

Marking an invoice Paid records its payment date using the current UK date. The dashboard total includes only paid invoices received within the displayed financial year, the drill-down reconciles with that total, and reissuing the invoice clears its payment contribution.

## Notes

- Issued invoices should not be silently changed by later gig edits.
- Draft invoice regeneration should happen only after an explicit user choice.
- Delivery checks depend on test environment configuration. If email or Google Drive is not configured, record that the delivery step was skipped.
# Supplementary thumbnail sizing

After generating an invoice against PostgreSQL, verify the initial first-page thumbnail loads without a spurious “PDF changed” warning. Persisted timestamp precision/offset formatting alone must not invalidate it; genuine PDF replacement/revision changes must still invalidate stale thumbnails.

When the invoice thumbnail is eligible, verify its dedicated layout distributes width evenly across two facts columns and the PDF column rather than crowding the facts beside an oversized preview rail. The page remains centred and proportional, with no stretch, cropping, new PDF reads or loading changes. Check short/wide panes and long fact values, and confirm Clients/Gigs retain their distinct map layouts.

On a wide, tall invoice pane with a current PDF, confirm page one is centred beside the facts and uses the available column width/height without stretching. Resize within eligible dimensions: the whole page stays visible, no permanent caption adds scrolling, and resizing alone does not fetch the PDF again. Hover and keyboard focus show page-one/action help; Enter still opens full preview. Existing Preview controls remain available when the thumbnail is omitted. Repeat with a short pane and on mobile, where insufficient space omits the visual rather than stacking it.

## Invoice delivery action area

Confirm the delivery count appears in the bottom delivery area, not the facts grid. **Invoice PDF** status occupies its former compact facts slot (with retry still available when applicable), rather than a trailing full-width row.

Confirm **Send to client** and **Publish to Drive** appear only in the labelled bottom **Invoice delivery** area, with delivery status/recipient context. The header retains Preview, download, line items and record actions. Existing readiness/Drive restrictions and seller-profile guidance remain intact. Sending still opens the review workflow, supports cancellation, and restores focus to the bottom Send button when closed. Verify completion notifications, persistent errors in still-open dialogs, and mobile placement without controls overflowing.
