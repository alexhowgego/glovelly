# Expense And Receipt UAT Journeys

## Purpose

Use these journeys when a change may affect gig expenses, receipt attachments, quick receipt capture, reimbursement status, or expense statements.

## Preconditions

- You can sign in.
- At least one client and one saved gig exist.
- You have a small PDF or image available for receipt upload.
- If testing expense statements, use gigs for the same client unless the journey asks for a mixed-client negative check.

## Expense Receipt Journey

### Steps

1. Open a saved gig with an expense.
2. Upload a PDF or image receipt to the expense.
3. Confirm it appears in the receipt list.
4. Download the receipt.
5. Delete the receipt.
6. Confirm the receipt disappears and cannot be downloaded.

### Expected Results

Receipt metadata, storage, download, and deletion all work without changing the expense reimbursement state.

## Quick Receipt Journey

### Steps

1. Use quick receipt capture to upload a receipt.
2. If the app suggests a nearby gig, accept it or choose a different gig.
3. Fill in the receipt draft description and amount.
4. Save it.
5. Open the target gig.

### Expected Results

The draft becomes a normal gig expense with its receipt attached. Existing expenses and attachments remain intact.

## Expense Reimbursement Journey

### Steps

1. Create a gig with at least two expenses.
2. Mark one expense as `Reimbursed`.
3. Enter a reimbursed date and method or note when prompted.
4. Save or confirm the change.
5. Generate an invoice from the gig, or regenerate a linked draft invoice if prompted.

### Expected Results

Reimbursed expenses are visually distinct and excluded from newly generated invoice lines by default. Claimable expenses still appear.

### Claimable Again

1. Change a reimbursed expense back to `Claimable`.
2. Regenerate a linked draft invoice if prompted.

Expected result: the expense becomes eligible for generated invoice lines again.

## Expense Statement Journey

### Steps

1. Create or identify multiple gigs for the same client with expenses.
2. Include at least one receipt attachment.
3. Mark one expense as `Reimbursed`.
4. Select the same-client gigs in the Gigs list.
5. Open the expense statement workflow.

### Expected Results

The expense statement modal opens, expenses are grouped by gig, reimbursed or not-claimable expenses are visually distinct, and reimbursed expenses are excluded by default.

### Preview And Download

1. Select the reimbursed expense in the modal.
2. Toggle receipt attachment and receipt appendix options.
3. Preview the PDF.
4. Download the PDF.

Expected result: selected reimbursed expenses appear in the statement, receipt options affect the generated preview/download, the embedded PDF preview loads in the modal, and the downloaded PDF matches the preview.

### Negative Checks

1. Select a gig for one client.
2. Try to select a gig for a different client.

Expected result: the app prevents mixed-client selections before generating the statement.

Then:

1. Open a gig with no expenses.
2. Try to launch an expense statement.

Expected result: the app explains that expenses are needed before a statement can be generated. No invoice state changes.

## Notes

- Receipt attachment changes should not mutate reimbursement status.
- Reimbursement status should affect future generated documents, not old PDFs.
- Expense statements are projections and should not create invoices or mutate gig invoice links.
