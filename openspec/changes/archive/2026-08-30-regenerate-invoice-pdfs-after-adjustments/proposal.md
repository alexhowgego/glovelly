## Why

Manual invoice adjustments update the authoritative line items and totals but leave the stored PDF unchanged. This can expose a document that no longer represents the invoice through download or email delivery, creating an accounting and customer-communication risk.

## What Changes

- Regenerate an invoice PDF after a successful manual adjustment so the stored document matches the current invoice data.
- Model and expose whether an invoice document is current, regenerating, missing, or has failed regeneration.
- Prevent downloading, emailing, publishing, or otherwise presenting a non-current invoice PDF as ready.
- Provide clear, recoverable UI feedback for document regeneration progress and failures.
- Preserve invoice identity, numbering, and delivery audit history when the PDF is regenerated.

## Capabilities

### New Capabilities
- `invoice-document-freshness`: Maintains a current derived invoice PDF and blocks document delivery while it is unavailable or out of date.

### Modified Capabilities

- None.

## Impact

- Backend invoice adjustment, PDF generation, download, email delivery, and Google Drive publishing endpoints/services.
- Invoice persistence and frontend `Invoice` API type to carry document readiness state.
- Invoice workspace actions and feedback for adjustments, preview/download, and delivery.
- Backend integration tests for regeneration and stale-document blocking; frontend coverage for visible document states.
