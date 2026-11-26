# Invoice PDF watermarking

Draft marking is a document-state rule, independent of subscription plans and administrative permissions. It is a workflow distinction and deterrent, not DRM.

## Rendering and readiness

`Invoice.RequiresDraftWatermark` is true for Draft invoices and for Cancelled invoices without `FirstIssuedUtc`. It is false for Issued, Paid, Overdue, and previously issued Cancelled invoices. Previously issued invoices returned to Draft are still watermarked.

`InvoicePdfRenderer` appends a diagonal DRAFT to each page's content stream after normal drawing, using an isolated translucent graphics state. The watermark travels with previewed/downloaded/delivered bytes. It is not an HTML overlay, annotation, or optional layer; determined PDF editing remains possible.

`InvoicePdfService.SaveGeneratedPdfAsync` requires the caller's generated watermark mode to match the invoice's intended state. Successful storage stamps nullable `PdfHasDraftWatermark`; null denotes legacy/unknown, never proof of a watermarked draft.

The metadata response's computed `isPdfCurrent` and `InvoiceDocumentReadiness` share the same model readiness predicate: current document state, matching revisions, and compatible watermark provenance. Draft/never-issued Cancelled requires known watermarked output. Clean-eligible states reject known watermarked output but accept unknown legacy provenance under normal freshness checks. The frontend also checks these fields before exposing document actions.

Authenticated PDF reads, email preparation/delivery, and Drive publishing use `OpenCurrentReadAsync`; they do not repair documents. Low-level `OpenReadAsync` is a storage primitive, not an authorized document-delivery boundary. Thumbnails retain independent uncached reads and metadata revalidation; viewing does not generate or mutate invoices.

Issue renders for Issued state; redraft/reissue renders for Draft state. Ordinary regeneration preserves lifecycle audit fields. A reissue restores the watermark even if an earlier version was issued.

Cancelled → Draft repairs a non-current/incompatible document within that explicit mutation: it persists the new status and Regenerating revision before replacing bytes, preserves original issue/reissue history and dates, and saves Failed readiness with retry if rendering fails. Both invoice-update and status-update endpoints share this behavior. It is not passive read repair.

`InvoiceDeliveryPolicy` permits actual delivery only for Issued, Paid, and Overdue. Both channel endpoints and the shared delivery service reject Draft and Cancelled before external side effects. Draft email review remains read-only and available for preparing mandatory issue-and-send; Cancelled review/delivery is blocked until restoration and issuance. Authorized local PDF reads remain governed by watermark/readiness rather than delivery status.

The frontend `executeInvoiceDelivery` coordinator owns the common issue-if-Draft → optional gig completion → delivery sequence for both channels. Confirmation is a UI concern, never hidden inside transport functions. Direct issuance and all Drive states use `InvoiceActionConfirmation`; email uses its existing composition review. There are no draft-only delivery choices or browser publication prompts. Failed issuance prevents delivery; successful issuance is retained in shared/review state before delivery so retry does not issue again. Combined operations are sequential, not atomic.

The thumbnail overview/frame is keyed by invoice ID rather than document revision. Only previous displayed pixels of the same mounted selection survive refresh, blurred/labelled/disabled. A short-lived staging canvas receives a fresh render; the visible canvas is replaced only after validated rendering succeeds. Error states retain frame dimensions but clear old pixels. Selection, session changes, loss of eligibility, and unmount release old pixels/tasks; PDF bytes are not cached. Opacity transitions respect reduced-motion settings.

## Deployment and legacy recovery

Migration `20261121091126_AddDraftInvoicePdfWatermark` adds nullable provenance and invalidates old Draft and never-issued Cancelled documents by advancing their document revision and setting a recoverable Failed state/message. It preserves old blob/revision metadata, invoice data, and audit history. Clean-eligible legacy invoices remain readable without regeneration.

Deploy using normal migration/application CI. Old application instances do not enforce the new predicate: finish rollout/drain old instances before considering the invariant established. No schema migration opens blobs or renders PDFs.

Users recover affected invoices explicitly with the inline **Try again** action. Successful regeneration creates a watermarked current document without issuing, reissuing, or sending it. Failure leaves the document unavailable and retryable. Missing gig-line refresh work is handled through the existing regeneration workflow.

Prefer a forward fix. Retain the additive column on application rollback; do not restore stale PDFs by undoing migration data. Old application code removes enforcement, so pause any dependent quota rollout.

## Limits and follow-on work

- Already downloaded, emailed, or Drive-published copies cannot be recalled or modified.
- Mutable per-invoice blob keys retain the existing concurrent-replacement race; thumbnail revalidation is best effort, not an immutable byte/revision guarantee.
- Database and blob writes are not a distributed transaction. First-issue allowance consumption, retries, concurrency, and clean-document publication need an explicit design in #192 before quotas are enabled.
- Subscription/Founder migration is outside this change.

## Verification

Backend coverage includes per-page render output, issue/reissue, cancelled never-issued documents, provenance compatibility, authorized delivery boundaries, failures/retries, and a PostgreSQL legacy migration fixture. Migration tests use the existing `GLOVELLY_AUTH_TEST_CONNECTION` isolated CI database; do not target production.

See [invoice UAT](../uat/invoices.md). Browser UAT and documentation capture run only in Ubuntu CI. Review actual single-page and multi-page PDFs in staging for watermark visibility/readability; do not manufacture replacement guide screenshots.
