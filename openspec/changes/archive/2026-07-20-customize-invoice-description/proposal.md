## Why

Generated invoices receive a default description, but users cannot correct or tailor that wording for a particular invoice. Giving draft invoices a focused description editor lets users produce an accurate document without changing gig data or rebuilding the invoice.

## What Changes

- Add a draft-only API operation to update an invoice's description independently of other invoice fields.
- Add an editable Description field to the invoice Line items pane and retain the existing read-only display in invoice details.
- Preserve a saved custom description when a draft invoice PDF is regenerated.
- Reject description changes once an invoice is no longer Draft, preserving issued document history.

## Capabilities

### New Capabilities
- `invoice-description-customization`: Allows users to edit a draft invoice's document-level description and retain it through PDF regeneration.

### Modified Capabilities

None.

## Impact

- Backend invoice endpoint mapping and integration tests.
- React invoice workspace state and Line items pane.
- Existing invoice PDF regeneration behavior will use the saved description; no schema or dependency changes are required.
