# UAT Playbook

This guide captures high-value manual regression journeys for Glovelly. Use it before shipping changes that touch gigs, expenses, receipts, invoices, expense statements, delivery, or user settings.

The aim is not to test every field. The aim is to walk the product like a user would and catch cross-workflow breakage that automated backend tests or frontend build checks may miss.

## Before You Start

Run the automated checks first:

```bash
./verify.sh
```

Then start the app:

```bash
./run-dev.sh
```

Use a fresh browser session if possible. If you are testing against seeded local development data, note any existing clients, gigs, invoices, and receipts before you begin.

## Core Smoke Journey

1. Sign in.
2. Open Clients and confirm the client list loads.
3. Open Gigs and confirm existing gigs load.
4. Open Invoices and confirm existing invoices load.
5. Open user settings or seller profile and confirm the modal opens and closes.
6. Refresh the page and confirm the same data returns without session issues.

Expected result: navigation, session state, and core reads are healthy.

## Editor Navigation Regression Checks

This guards against issue 121 and nearby editor discard paths.

For Clients:

1. Open Clients, select a client, and click `Edit`.
2. Change a field without saving.
3. Select a different client in the list and decline the discard prompt.
4. Confirm the original client remains selected and the unsaved edit remains visible.
5. Select the different client again and accept the discard prompt.
6. Confirm the editor updates to the newly selected client.
7. Change a field again, click `New client`, and decline the discard prompt.
8. Click `New client` again and accept the discard prompt.

Expected result: unsaved client edits are never discarded without confirmation. Accepted navigation switches the editor to the selected client or a blank new-client form.

For Gigs:

1. Open Gigs, select a gig, and click `Edit gig`.
2. Change a field or add an unsaved expense row.
3. Select a different gig in the list and decline the discard prompt.
4. Confirm the original gig remains selected and the unsaved edit remains visible.
5. Select the different gig again and accept the discard prompt.
6. Confirm the editor updates to the newly selected gig.
7. Change a field again, click `New gig`, and decline the discard prompt.
8. Click `New gig` again and accept the discard prompt.

Expected result: unsaved gig edits and unsaved expense draft fields are never discarded without confirmation. Accepted navigation switches the editor to the selected gig or a blank new-gig form.

For Admin:

1. Open Admin as an administrator, select a user, and click `Edit access`.
2. Change a field without saving.
3. Select a different user in the list and decline the discard prompt.
4. Confirm the original user remains selected and the unsaved edit remains visible.
5. Select the different user again and accept the discard prompt.
6. Confirm the editor updates to the newly selected user.
7. Change a field again, click `Add user`, and decline the discard prompt.
8. Click `Add user` again and accept the discard prompt.

Expected result: unsaved admin access edits are never discarded without confirmation. Accepted navigation switches the editor to the selected user or a blank add-user form.

## Gig To Invoice Journey

1. Create a client or choose an existing client.
2. Create a gig with fee, date, venue, status `Planned`, and at least one expense.
3. Save the gig.
4. Generate an invoice from that gig.
5. Open the invoice.
6. Confirm invoice lines include the performance fee and chargeable expenses.
7. Download the PDF.
8. Return to the gig.
9. Confirm the gig shows as invoiced and links back to the invoice.

Expected result: generating an invoice links the gig, creates expected lines, and produces a downloadable PDF.

## Editing An Invoiced Gig

This guards against issue 113.

1. Start with a gig that is already linked to an invoice.
2. Edit the gig.
3. Change only the gig status between `Planned`, `Completed`, and `Draft`.
4. Save.
5. Open the linked invoice.

Expected result: no prompt appears, the gig remains linked to the invoice, and invoice lines are unchanged.

Then test invoice-relevant changes:

1. Edit the same linked gig.
2. Change an invoice-relevant field such as fee, title, date, venue, or expenses.
3. Save.

Expected result: if the linked invoice is a draft, the app asks whether to regenerate the draft. If accepted, the draft invoice lines/PDF update. If declined, the existing invoice stays unchanged. Issued invoices should not be silently changed.

## Cancelling A Gig With A Linked Invoice

1. Start with a gig linked to an invoice.
2. Edit the gig.
3. Change status to `Cancelled`.
4. Save.

Expected result: the app asks whether to cancel the linked invoice. If accepted and the invoice status allows cancellation, the invoice moves to `Cancelled`. If declined, the gig is cancelled but the invoice remains unchanged.

## Combined Invoice Journey

1. Create two uninvoiced gigs for the same client.
2. Select both gigs in the gig list.
3. Generate a combined invoice.
4. Confirm the generated invoice preview modal opens.
5. Download the PDF or open the invoice.

Expected result: one invoice is created, both gigs are linked, invoice lines are ordered sensibly by gig date and line type, and the generated PDF can be previewed before navigating away.

Negative check:

1. Select gigs from different clients.
2. Try to generate a combined invoice.

Expected result: the app blocks generation and explains that selected gigs must belong to the same client.

## Monthly Invoice Journey

1. Create or identify multiple uninvoiced gigs for the same client in the same month.
2. Open the client.
3. Choose the month.
4. Generate a monthly invoice.
5. Confirm the generated invoice preview modal opens.
6. Download the PDF or open the invoice.

Expected result: the invoice is created as a draft, linked gigs remain linked, lines are generated after redraft, and the PDF can be previewed and downloaded.

## Expense Receipt Journey

1. Open a saved gig with an expense.
2. Upload a PDF or image receipt to the expense.
3. Confirm it appears in the receipt list.
4. Download the receipt.
5. Delete the receipt.
6. Confirm the receipt disappears and cannot be downloaded.

Expected result: receipt metadata, storage, download, and deletion all work without changing the expense reimbursement state.

## Quick Receipt Journey

1. Use quick receipt capture to upload a receipt.
2. If the app suggests a nearby gig, accept or choose a different gig.
3. Fill in the receipt draft description and amount.
4. Save it.
5. Open the target gig.

Expected result: the draft becomes a normal gig expense with its receipt attached. Existing expenses and attachments remain intact.

## Expense Reimbursement Journey

1. Create a gig with at least two expenses.
2. Mark one expense as `Reimbursed`.
3. Enter a reimbursed date and method/note when prompted.
4. Save/confirm the change.
5. Generate an invoice from the gig, or regenerate a linked draft invoice if prompted.

Expected result: reimbursed expenses are visually distinct and excluded from newly generated invoice lines by default. Claimable expenses still appear.

Also test:

1. Change a reimbursed expense back to `Claimable`.
2. Regenerate a linked draft invoice if prompted.

Expected result: the expense becomes eligible for generated invoice lines again.

## Invoice Line Refresh Regression Checks

Use these checks with either seeded data or freshly created test data. The important thing is the workflow shape: a gig linked to a draft invoice, with fee, mileage, and expenses that can be regenerated.

1. Create or identify a gig with a performance fee and at least one chargeable expense.
2. Generate a draft invoice from the gig.
3. Redraft or otherwise regenerate the invoice.
4. Open the invoice lines.

Expected result: generated gig lines are replaced cleanly, not appended repeatedly. There should be exactly one generated performance fee for the gig, plus the expected mileage and chargeable expense lines.

Then check reimbursement changes on a linked draft invoice:

1. Start with a gig linked to a draft invoice and at least two expenses.
2. Mark one expense as `Reimbursed`.
3. Accept the prompt to regenerate the linked draft invoice.
4. Confirm the reimbursed expense is removed from the invoice.
5. Change that same expense back to `Claimable`.
6. Accept the prompt to regenerate the linked draft invoice.
7. Reopen the invoice.

Expected result: the expense is added back exactly once. Existing generated lines are not duplicated, and any manual adjustment lines remain intact.

Then check driving and mileage:

1. Start with a gig linked to a draft invoice where `I was driving for this gig` is enabled, travel miles are greater than zero, and passenger count is set if passenger mileage is relevant.
2. Confirm the linked draft invoice includes mileage, and passenger mileage when applicable.
3. Edit the gig and clear `I was driving for this gig`.
4. Save and accept the prompt to regenerate the linked draft invoice.
5. Confirm mileage and passenger mileage lines are removed from the invoice.
6. Edit the same gig again and re-enable `I was driving for this gig`.
7. Confirm the previous travel miles and passenger count are still present in the edit form.
8. Save and accept the prompt to regenerate the linked draft invoice.
9. Reopen the invoice.

Expected result: mileage lines disappear while driving is disabled and return when driving is re-enabled. Toggling driving must not erase previously saved mileage or passenger values.

## Expense Statement Journey

1. Create or identify multiple gigs for the same client with expenses.
2. Include at least one receipt attachment.
3. Mark one expense as `Reimbursed`.
4. Select the same-client gigs in the Gigs list.
5. Open the expense statement workflow.

Expected result: the expense statement modal opens, expenses are grouped by gig, reimbursed or not-claimable expenses are visually distinct, and reimbursed expenses are excluded by default.

Then:

1. Select the reimbursed expense in the modal.
2. Toggle receipt attachment and receipt appendix options.
3. Preview the PDF.
4. Download the PDF.

Expected result: selected reimbursed expenses appear in the statement, receipt options affect the generated preview/download, the embedded PDF preview loads in the modal, and the downloaded PDF matches the preview.

Negative checks:

1. Select a gig for one client.
2. Try to select a gig for a different client.

Expected result: the app prevents mixed-client selections before generating the statement.

Then:

1. Open a gig with no expenses.
2. Try to launch an expense statement.

Expected result: the app explains that expenses are needed before a statement can be generated. No invoice state changes.

## Invoice Preview Journey

1. Generate an invoice from a gig or from selected gigs.
2. Confirm the invoice preview modal opens immediately.
3. Download the PDF from the modal.
4. Open the invoice from the modal.
5. Use the invoice pane `Preview` button.

Expected result: the same invoice PDF can be previewed reactively from the invoice pane, downloaded from the modal, and opened in the invoice workspace.

Then:

1. Redraft a draft invoice.
2. Confirm the regenerated PDF preview modal opens after redraft.
3. Re-issue an issued invoice.
4. Confirm the regenerated PDF preview modal opens after re-issue.

Expected result: redraft and re-issue update the PDF, preserve the expected invoice history rules, and show the latest PDF in the preview modal.

## Invoice Status And Delivery Journey

1. Open a draft invoice linked to one or more non-cancelled gigs.
2. Issue it.
3. Accept the prompt to mark the linked gig or gigs as completed.
4. Repeat with another linked draft invoice and decline the gig completion prompt.
5. Open another draft invoice and send it by email if a recipient is configured.
6. Optionally include receipt attachments, then accept the prompt to mark the delivered draft as issued.
7. Accept or decline the follow-up linked gig completion prompt.
8. Publish a draft invoice to Google Drive if connected, then repeat the delivered-draft issue prompt check.
9. Re-issue an issued invoice.

Expected result: status transitions are explicit, delivery state is recorded, delivered drafts can be promoted to issued by choice, issuing an invoice can complete linked gigs by choice, declined prompts leave existing invoice/gig state unchanged, PDF remains downloadable, and receipt attachments are included only when requested.

## Seller Profile And Defaults Journey

1. Open seller profile.
2. Add or edit seller name, address, email, and payment details.
3. Save.
4. Generate or redraft an invoice.
5. Download the PDF.

Expected result: invoice PDF reflects seller profile and payment details. Missing profile details should produce helpful UI notices rather than broken invoices.

## Admin Access Journey

For admin users:

1. Open Admin.
2. Create or edit a user record.
3. Toggle active state or role.
4. Save.
5. Confirm the user list updates.

Expected result: admin changes persist and non-admin users cannot access admin workflows.

## Regression Notes

Pay special attention to workflows that cross boundaries:

- Editing gigs should not silently mutate historical or issued invoices.
- Draft invoice regeneration should happen only after an explicit user choice.
- Receipt attachment changes should not mutate reimbursement status.
- Reimbursement status should affect future generated documents, not old PDFs.
- Expense statements are projections and should not create invoices or mutate gig invoice links.

When a manual journey reveals a bug, add or update backend tests for the server-side rule, and consider whether the frontend flow should eventually become an automated browser test.
