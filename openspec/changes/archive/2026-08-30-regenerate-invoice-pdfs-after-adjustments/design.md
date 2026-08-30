## Context

Invoices store PDF blob metadata directly on the `Invoice` model. Adding a manual adjustment changes invoice lines and totals but does not replace those PDF bytes. `IInvoicePdfService.OpenReadAsync` is used by download and delivery flows, but it currently establishes only blob presence, not that the blob represents the current invoice.

This change introduces a document lifecycle shared by adjustment, PDF generation, PDF access, email delivery, and Google Drive publishing. The invoice PDF remains a derived representation; an adjustment does not create a new invoice or reset delivery history.

## Goals / Non-Goals

**Goals:**
- Persist enough document state to distinguish a current PDF from a missing, regenerating, or failed one.
- Regenerate the PDF after each successful adjustment and only expose it after replacement succeeds.
- Make all user-facing PDF delivery boundaries reject non-current documents.
- Give users a clear recovery path when rendering fails.
- Provide readiness information for the future review-and-send invoice email change.

**Non-Goals:**
- Redesigning invoice templates, invoice numbering, or delivery-history semantics.
- Changing the immutable content of a previously delivered email attachment.
- Introducing background job infrastructure; regeneration is performed by an explicit application request in this change.
- Implementing the review-and-send dialog from issue #253.

## Decisions

### Persist a derived-document state and source revision on the invoice

The invoice will record a monotonically increasing revision of PDF-relevant invoice data, the revision represented by the stored PDF, and a document state (`Current`, `Regenerating`, `Missing`, or `Failed`). A PDF is current only when its state is `Current`, it has a readable stored file, and its represented revision equals the invoice's current document revision.

This explicit state is preferred to comparing timestamps. Timestamps cannot prove ordering under clock precision, concurrent writes, or a failed regeneration, and they provide no useful user-facing failure state. It also prevents a newly added rendering path from assuming `PdfStorageKey != null` means deliverable.

### Invalidate before regenerating, and publish only a completed replacement

An adjustment first records the updated invoice data and advances its document revision while marking the document unavailable. The generation workflow renders from the resulting current invoice data, stores the replacement, and atomically records the represented revision and `Current` state. If rendering or storage fails, it records `Failed` state and a safe failure message; the old blob is never treated as current.

This ordering prioritizes the no-stale-delivery invariant over retaining an outdated but readable attachment. Repeated regeneration requests target the latest revision and are idempotent with respect to the document representation: they replace document metadata without adding invoice lines or delivery records.

### Centralize readiness enforcement in the PDF service

`IInvoicePdfService` (or a closely associated document-readiness service) will provide one guarded operation for obtaining a current invoice PDF. Download, email delivery, and Google Drive publishing will use it rather than inspecting PDF fields independently. Endpoints translate the typed readiness result into actionable validation responses; the frontend consumes the invoice document state to disable actions and explain why.

Enforcing at the service boundary is preferred to endpoint-only checks because email delivery currently reopens the PDF after its endpoint validation, and future delivery channels must inherit the same guarantee.

### Regenerate synchronously after an adjustment and offer inline recovery

The adjustment response will represent a completed current document or an explicit failed/unavailable state. Rendering occurs as part of the adjustment operation so a successful response never silently leaves stale output. If regeneration fails after the adjustment persists, the response communicates that the financial adjustment succeeded and provides a transient inline retry from that failure state. That retry renders the latest revision without repeating the adjustment; it is not a persistent invoice-toolbar action.

Synchronous regeneration is preferred to a background queue because the existing application has no job infrastructure and the user needs an immediate, unambiguous result. A later asynchronous implementation can preserve this state contract. The existing Redraft/Re-issue control is not used for recovery because it has distinct invoice-lifecycle side effects, including reissue audit fields and, for drafts, invoice-date changes.

### Expose document readiness in the Invoice DTO

The invoice API response will include a small document-status shape or fields sufficient for the UI to distinguish current, regenerating, missing, and failed PDFs and show generated time when current. This is also the contract that #253's delivery-preparation endpoint will rely on. Internal failure details will be logged, while the client receives a safe actionable message.

## Risks / Trade-offs

- [A render failure occurs after the adjustment has been saved] -> Persist `Failed` state, block all document access, and provide transient inline retry without duplicating the financial adjustment.
- [Blob replacement succeeds but database metadata update fails] -> The old document remains blocked because its represented revision does not match. The new blob is an orphan candidate for storage cleanup rather than an incorrect delivery.
- [Two adjustments or retries overlap] -> Use the invoice's current revision at completion and refuse to mark an older render current; retries render the latest revision.
- [Synchronous rendering slows adjustment submission] -> Surface a regenerating/progress state while the request is pending; retain a later path to move the same state machine to queued work if latency becomes material.
- [A delivery path bypasses the guarded service] -> Cover download, email, and Drive publishing with integration tests and keep direct PDF metadata checks out of endpoints.

## Migration Plan

1. Add nullable/backfilled document revision and state fields through EF Core migration.
2. Mark existing invoices with a readable stored PDF as `Current` at matching initial revisions; mark invoices without a readable PDF as `Missing`.
3. Deploy the guarded read path before exposing adjustment regeneration so legacy stale documents cannot be delivered without a known state.
4. Deploy synchronous regeneration and inline retry support, then surface document state in the frontend, including the Drive publishing action.
5. Rollback preserves invoices and their existing blobs. If application code is rolled back after the migration, the new fields are ignored; no destructive data migration is required.
