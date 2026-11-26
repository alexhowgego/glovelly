## Why

Draft invoice PDFs currently look ready to send. Embedding a visible draft watermark will distinguish work in progress from issued documents while allowing unrestricted experimentation, and establishes a prerequisite for first-issue invoice allowances in GitHub issue #192 without introducing billing logic.

Tracking issue: [#309](https://github.com/alexhowgego/glovelly/issues/309).

## What Changes

- Render an unmistakable, readable `DRAFT` watermark across the content of every draft PDF page, in the PDF bytes rather than in the preview UI.
- Keep issued, paid, overdue, and previously issued cancelled invoice PDFs clean. Never-issued cancelled invoices retain the DRAFT watermark; cancellation must not substitute for issuance.
- Show a concise visible hint on draft invoices explaining that the watermark is removed when the invoice is issued.
- Make creation, redraft, reissue, issue, adjustment, source refresh, and explicit regeneration produce the correct document state.
- Restore a current watermarked PDF during Cancelled → Draft; use a single pre-issue confirmation with optional linked-gig completion, and issue before combined email/Drive delivery so the delivered PDF is clean.
- Track watermark provenance and invalidate legacy stored draft and never-issued cancelled PDFs so preview, download, email, and Drive publishing cannot expose an unwatermarked document where a watermark is required.
- Recover invalidated drafts through explicit regeneration, never through passive reads or thumbnail rendering.
- Require issuance before application-managed delivery, reject Cancelled delivery, and use consistent styled confirmations and one shared issue-before-deliver workflow.
- Keep the selected invoice preview frame stable during replacement, using a blurred, disabled previous rendering only as an updating placeholder.
- Add automated coverage and update invoice UAT and user/operator documentation.
- Leave subscriptions, quotas, Founder migration, payment processing, and anti-tampering controls to separate changes.

## Capabilities

### New Capabilities

- `draft-invoice-pdf-watermarking`: Every draft page is visibly marked in its stored bytes; issued documents are clean; lifecycle transitions preserve this distinction.

### Modified Capabilities

- `invoice-document-freshness`: Document readiness includes compatibility between stored watermark provenance and the current invoice lifecycle state, including safe legacy-draft recovery.
- `invoice-pdf-thumbnails`: Preserve a stable frame and allow clearly marked non-interactive previous pixels during same-invoice refresh without retaining reusable PDF bytes.

## Impact

- Backend PDF renderer and its internal PDF builder, invoice workflow service, PDF storage/readiness service, invoice model, EF mapping, and migration.
- Existing authenticated PDF, email, and Google Drive delivery boundaries; no new API prefix or billing endpoints.
- Invoice document metadata/frontend types if required to expose readiness consistently; preserve existing thumbnail byte ownership and read-only behavior.
- Backend renderer/workflow/integration and migration tests, invoice frontend recovery tests where affected, `docs/uat/invoices.md`, the public guide, and relevant engineering documentation.
- No new runtime setting, external rendering service, payment provider, or PDF dependency is expected.
