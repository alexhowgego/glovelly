## Why

Quick receipt details can change generated invoice lines without refreshing the linked invoice PDF or document revision, allowing stale documents to be delivered. The shared generated-line synchronizer also mutates non-draft invoices, despite issued and other finalized invoices being immutable.

## What Changes

- Make generated invoice-line synchronization draft-only wherever the shared workflow is used.
- Add a shared refresh workflow that rebuilds generated lines for affected draft invoices, advances each document revision once, and regenerates each final PDF once.
- Automatically use that workflow when quick receipt details are saved or moved between gigs.
- Preserve receipt and generated-line updates if PDF rendering fails, mark only the affected draft document unavailable, and retain existing retry and delivery-boundary blocking.
- Return and publish affected invoice updates so the invoice workspace reflects quick receipt changes immediately.
- Explain in the quick-receipt workflow that only linked draft invoices refresh automatically, and extend backend and UAT coverage.

## Capabilities

### New Capabilities
- `draft-invoice-source-refresh`: rebuild generated invoice content from changed linked gig data only while the invoice is a draft.

### Modified Capabilities
- `invoice-document-freshness`: require generated-line changes to invalidate and regenerate the corresponding draft invoice PDF safely.

## Impact

- Backend invoice workflow, generated-line service callers, quick-receipt endpoint, invoice API responses, and workspace events.
- Authenticated React quick-receipt and invoice workspace state handling.
- Backend integration tests and the quick-receipt UAT journey.
