## 1. Draft Invoice Refresh Workflow

- [x] 1.1 Make shared generated-line synchronization resolve the linked invoice and skip all automatic mutation for non-draft invoice statuses.
- [x] 1.2 Add a collection-oriented invoice workflow operation that groups affected gigs by draft invoice, rebuilds their generated lines, advances each document revision once, and reloads final lines before rendering.
- [x] 1.3 Persist regenerated lines and document revision before rendering, and isolate per-invoice render or storage failures by marking only the failed document unavailable.
- [x] 1.4 Adapt invoice generation and explicit redraft callers to preserve their one-final-PDF behavior under the draft-aware shared workflow.

## 2. Quick Receipt And Workspace Integration

- [x] 2.1 Invoke the shared draft-invoice refresh operation after quick receipt detail saves and reassignment, returning affected invoice updates with the saved gig response.
- [x] 2.2 Publish invoice-scoped workspace events for automatic refreshes and refresh invoice state when those events arrive in other authenticated sessions.
- [x] 2.3 Merge affected invoices into the initiating invoice workspace immediately after a quick receipt save or move.
- [x] 2.4 Add concise quick-receipt guidance that explains automatic refresh applies only to linked draft invoices.

## 3. Regression Coverage And Documentation

- [x] 3.1 Add backend coverage for selected-gig and manually linked/monthly draft-invoice receipt refreshes, including generated lines, document revision, and PDF content.
- [x] 3.2 Add backend coverage for receipt reassignment across distinct invoices and shared monthly invoices, ensuring each draft document refreshes once.
- [x] 3.3 Add backend coverage proving issued, overdue, paid, and cancelled invoices retain generated lines and PDF metadata when shared synchronization is invoked.
- [x] 3.4 Add backend coverage for automatic-refresh PDF failures that preserves receipt and line changes, reports document failure, and blocks download, email delivery, and Drive publishing until retry.
- [x] 3.5 Update the quick-receipt UAT journey with draft-only automatic refresh, finalized-invoice immutability, document-failure recovery, and invoice-workspace update expectations.
- [x] 3.6 Run the focused backend suite and relevant frontend lint/build checks.
