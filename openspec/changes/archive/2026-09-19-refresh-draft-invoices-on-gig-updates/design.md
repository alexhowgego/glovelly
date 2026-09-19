## Context

Generated invoice lines are derived from linked gig data. The current shared synchronizer deletes and recreates those lines without checking invoice status, does not advance the document revision, and does not regenerate the PDF. Quick receipt detail saves call that synchronizer after persisting an expense, so a linked invoice can have current lines but a stale PDF. Moving a receipt can affect two gigs, including two gigs on the same monthly invoice.

Invoice document freshness already models revision, current, regenerating, and failed states. Download, email, and Google Drive publishing already reject a non-current document. Manual adjustments establish the required failure behavior: persist the business change, then mark the document failed if rendering fails.

## Goals / Non-Goals

**Goals:**
- Enforce draft-only mutation for the shared generated-line synchronization path.
- Provide one reusable refresh operation for one or more changed gigs that produces complete, current PDFs for their affected draft invoices.
- Regenerate each affected invoice once after all of its changed gig lines are rebuilt.
- Keep persisted gig and invoice-line changes if an individual PDF render or storage operation fails.
- Reconcile affected invoice state in the initiating workspace and notify other open sessions.

**Non-Goals:**
- Automatically regenerate invoices after ordinary gig edits; that workflow retains its existing confirmation prompt and explicit redraft action.
- Change invoice status transitions, numbering, delivery history, or reissue auditing.
- Alter non-draft invoice content, including when a linked gig or receipt changes.
- Add asynchronous document generation or a background retry worker.

## Decisions

### Put the draft guard in the shared workflow service

The generated-line synchronization API will resolve the linked invoice and skip mutation unless it is `Draft`. This makes every current and future caller safe by default rather than relying on endpoint-specific status checks. Creation and explicit redraft flows remain valid because they operate on drafts.

An endpoint-only quick-receipt guard was rejected because manual linking and future callers could still mutate finalized invoice lines.

### Add a collection-oriented draft refresh operation

The invoice workflow service will expose a refresh operation accepting affected gigs. It will group gigs by their linked draft invoice, rebuild each changed gig's generated lines, advance the invoice document revision once, persist the final line collection, and then render the final PDF once per invoice.

Calling a per-gig PDF refresh in a loop was rejected because reassignment or a monthly invoice could generate intermediate PDFs and increment the same revision multiple times.

### Persist source changes and line changes before rendering

Quick receipt details are saved before the refresh operation. The operation persists regenerated lines and the new document revision before rendering so a render failure cannot roll back the receipt or leave the updated lines represented as current by the previous PDF. A failed render marks only that invoice `Failed`, retaining the prior blob as inaccessible through the existing revision/state checks.

Wrapping receipt, lines, blob storage, and database state in one transaction was rejected because blob storage is external and cannot participate reliably; the durable document-state model is the recovery mechanism.

### Return invoices and publish invoice-scoped events

The quick-receipt PATCH response will include affected draft invoices, allowing the initiating React workspace to merge authoritative state without waiting for SignalR. The endpoint will also publish `invoices` workspace events; `App.tsx` will refresh invoices for those events so other sessions converge.

Relying on a SignalR event alone was rejected because the initiating client can miss or lag the connection event after its successful mutation.

### Retain local quick-receipt feedback

The quick-receipt modal remains open after saving, so its status message will report whether linked draft invoices refreshed or require PDF retry. Guidance near the workflow will state that automatic refresh applies only to draft invoices. This follows the existing policy of keeping terminal feedback within an open multi-step modal rather than competing with global notifications.

## Risks / Trade-offs

- [A PDF failure after multiple affected invoices] -> Catch failures per invoice, persist each failure state independently, and continue processing other affected drafts.
- [EF tracked lines do not represent the final collection during rendering] -> Save rebuilt lines and reload each invoice's lines before rendering.
- [A non-draft invoice is linked accidentally] -> The shared service skips line and document mutation; regression tests cover every non-draft status.
- [Multiple open sessions show stale invoice data] -> Return affected invoices to the initiator and publish invoice-scoped workspace events for other sessions.
- [Explicit redraft duplicates refresh work] -> Keep explicit redraft as its own all-linked-gigs rebuild followed by one render; the new automatic path is used only by quick receipts.

## Migration Plan

1. Deploy the service and endpoint changes together; no schema migration is required because invoice document state and revision fields already exist.
2. Existing non-draft invoices remain untouched. Existing draft invoices refresh the next time an applicable quick receipt is saved or moved.
3. If an unexpected production regression requires rollback, revert the application deployment. No data backfill or destructive rollback is needed; a draft marked failed can use the existing regeneration retry.

## Open Questions

None.
