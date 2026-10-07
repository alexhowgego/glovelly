# Expense And Receipt UAT Journeys

## Purpose

Use these journeys when a change may affect gig expenses, receipt attachments, quick receipt capture, reimbursement status, or expense statements.

## Preconditions

- You can sign in.
- At least one client and one saved gig exist.
- You have a small PDF or image available for receipt upload.
- If testing expense statements, use gigs for the same client unless the journey asks for a mixed-client negative check.

## Expense Receipt Journey

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.UploadAndQuickCaptureWorkflowTests.BrowserReceiptAndAttachmentUploadsRoundTripThroughGigUi` covers browser receipt upload/delete and reimbursement preservation; backend tests cover API download/storage rules.

### Steps

1. Open a saved gig with an expense.
2. Upload a PDF or image receipt to the expense.
3. Confirm it appears in the receipt list.
4. Download the receipt.
5. Delete the receipt.
6. Confirm the receipt disappears and cannot be downloaded.

### Expected Results

Receipt metadata, storage, download, and deletion all work without changing the expense reimbursement state. Each completed file action shows a terminal notification; a missing receipt file shows a persistent error notification with a clear unavailable-file message.

## Receipt Analysis Journey

> **Automation:** Backend automated; browser review remains manual until a consented Vertex test environment and fixture set are available.

### Steps

1. Open a saved gig with an existing expense receipt and select **Analyse** beside that receipt.
2. Confirm the review dialog identifies the receipt and either shows a prior analysis or offers **Analyse receipt**.
3. Analyse a supported test JPEG, PNG, WebP, or PDF receipt.
4. Confirm merchant and total are clearly labelled as suggestions, confidence and warnings are visible, and date, currency, and category are labelled review-only.
5. Select **Use merchant and total** and confirm the normal expense editor opens with the suggested values but does not save them automatically.
6. Save the expense only after reviewing the populated fields.
7. Repeat using a receipt saved through **Scan receipt**. Confirm **Analyse receipt** opens the same review dialog and only fills the editable draft fields after explicit use.
8. Check an unsupported or oversized test attachment. Confirm the saved attachment and expense remain unchanged and the dialog gives a safe analysis failure message.

### Expected Results

Receipt analysis augments, but never blocks or silently changes, the manual expense workflow.

## Add To Glovelly Receipt Journey

> **Automation:** `IntakeEndpointsTests` cover proactive/explicit application, preferences, recovery, source cleanup, and scoping. `GigEndpointsTests` retain receipt correction and invoice-refresh regressions. Frontend journey tests cover stable upload, optional review, early finish and interrupted responses. `Glovelly.Uat.Tests.UploadAndQuickCaptureWorkflowTests.UnifiedReceiptMobileFlowSavesThenReviewsTheExactAttachment` covers mobile manual application and saved-record review in Ubuntu CI. Live-provider and visual variants remain manual.

### Steps

1. Select **Add to Glovelly** and confirm source type selection appears before a file picker. Choose **Photo or file**, enter unified upload, and choose a receipt photo/PDF. Repeat with pasted receipt text; URLs follow the resource journey.
2. Confirm analysis starts immediately in upload, retains the source and shows progress. Provider failure offers retry and explicit **Attach as Receipt** treatment without losing the source.
3. With High confidence enabled and a qualifying receipt, confirm it is proactively saved to the nearest visible non-cancelled nearby gig even when other candidates exist. Upload stays open with compact **Attached to [Gig]**, **Review attachment**, and **Done**. No dialog automatically closes, no review appears automatically, and no workspace navigation occurs.
4. Select **Done** without entering review, then confirm the receipt is saved in its gig. Reopen Add to Glovelly and confirm source selection is clean. Repeat closing after attached state; background/refresh during a save and confirm recovery never creates another receipt.
5. Set **Automatic receipt application** to Manual only and confirm upload waits for explicit attachment. Check Settings label/select alignment, hover help, and focus explanation. Restore the original preference after testing.
6. With no nearby gig or a historical intended destination, use **Load more gigs**. Confirm source, selection and fields survive loading/failure, retry works, and the action disappears when exhausted. Explicitly attach and confirm the same compact attached state.
7. Select **Review attachment** and confirm it opens the exact saved receipt with original file access and editable destination, description, amount and category. Save corrections/reassignment and confirm affected draft invoices refresh. Close review without edits and confirm the existing receipt is untouched.
8. Repeat with an issued or other non-draft invoice and confirm its lines/PDF remain unchanged. If draft PDF regeneration fails, confirm the receipt stays saved and the invoice explains its document unavailability and supports retry.
9. Repeat on a phone-sized viewport with notifications already visible. Progress, inline failures, attached state and review controls must remain readable/clickable above the notification viewport; save feedback must not disappear in a transient notification.
10. Close an unapplied upload and resume it from source selection. Discard it explicitly and confirm the next acquisition has empty source fields. Test deletion in saved-attachment review and confirm only that receipt/expense is removed.

### Expected Results

Every receipt uses source selection → stable upload/application → optional saved-attachment review. Both save modes reach the same attached presentation, and users can finish there. Candidate expansion is read-only and preserves pending fields. Linked draft invoices refresh; issued, overdue, paid and cancelled invoices remain unchanged. Failed PDF regeneration preserves the receipt while preventing stale document delivery. Unapplied sources remain recoverable; completed private intake is removed without erasing open attached state. Existing unrelated expenses and attachments remain intact.

## Expense Category Journey

### Steps

1. Open a saved gig and add or edit an ordinary expense.
2. Enter a category and save the expense.
3. Reopen the expense and change or clear the category, then save again.

### Expected Results

The category remains editable alongside amount and description, and saving it does not affect receipts or reimbursement status.

## Expense Reimbursement Journey

> **Automation:** Backend automated; manual UAT: reimbursement and generated-line rules have backend coverage; browser prompts and visual state remain manual.

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

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.ExpenseStatementTests.CanGenerateExpenseStatementPreviewAndDownload` and `ExpenseStatementVariantsRespectReimbursementSelectionAndInvoiceLinks` cover main preview/download, reimbursed inclusion, mixed-client blocking, and invoiced-gig projection rules; receipt-option variants remain manual/backend-covered.

### Steps

1. Create or identify multiple gigs for the same client with expenses.
2. Include at least one receipt attachment.
3. Mark one expense as `Reimbursed`.
4. Select the same-client gigs in the Gigs list.
5. Open the expense statement workflow.

### Expected Results

The expense statement modal opens, expenses are grouped by gig, reimbursed or not-claimable expenses are visually distinct, and reimbursed expenses are excluded by default.

### Invoiced Gig Selection

1. Identify a gig that already has a linked invoice and at least one expense.
2. Select it in the Gigs list with another gig for the same client.
3. Open the expense statement workflow.

Expected result: invoiced gigs remain selectable for expense statements, the statement opens for the same-client selection, and invoice links are not changed.

### Preview And Download

1. Select the reimbursed expense in the modal.
2. Toggle receipt attachment and receipt appendix options.
3. Preview the PDF.
4. Download the PDF.

Expected result: selected reimbursed expenses appear in the statement, receipt options affect the generated preview/download, the embedded PDF preview loads in the modal without being pushed out of view by long expense lists, and the downloaded PDF matches the preview.

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
