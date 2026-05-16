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
4. Open the invoice.

Expected result: one invoice is created, both gigs are linked, and invoice lines are ordered sensibly by gig date and line type.

Negative check:

1. Select gigs from different clients.
2. Try to generate a combined invoice.

Expected result: the app blocks generation and explains that selected gigs must belong to the same client.

## Monthly Invoice Journey

1. Create or identify multiple uninvoiced gigs for the same client in the same month.
2. Open the client.
3. Choose the month.
4. Generate a monthly invoice.
5. Open the invoice and PDF.

Expected result: the invoice is created as a draft, linked gigs remain linked, lines are generated after redraft, and the PDF downloads.

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

## Expense Statement Journey

1. Create or identify multiple gigs for the same client with expenses.
2. Include at least one receipt attachment.
3. Mark one expense as `Reimbursed`.
4. Request an expense statement preview for those gigs.

Expected result: expenses are grouped by gig, totals are correct, receipt attachment metadata appears when requested, and reimbursed expenses are excluded by default.

Then:

1. Include reimbursed expenses in the request.
2. Download the PDF with receipt appendix enabled.

Expected result: reimbursed expenses appear only when requested, the PDF downloads, and the receipt appendix lists attachment metadata.

## Invoice Status And Delivery Journey

1. Open a draft invoice.
2. Issue it.
3. Confirm issue date/due date/PDF update.
4. Send it by email if a recipient is configured.
5. Optionally include receipt attachments.
6. Publish to Google Drive if connected.
7. Re-issue an issued invoice.

Expected result: status transitions are explicit, delivery state is recorded, PDF remains downloadable, and receipt attachments are included only when requested.

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
