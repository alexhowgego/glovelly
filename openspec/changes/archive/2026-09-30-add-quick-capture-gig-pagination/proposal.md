## Why

Quick receipt and attachment capture only present a small set of date-nearby gigs. When the intended gig is valid but outside that set, users must abandon their pending capture and navigate elsewhere, which is especially problematic for delayed receipts and historical attachments.

## What Changes

- Add a shared, read-only paged quick-capture gig-candidate API that can progressively expose all visible, non-cancelled gigs.
- Add a Load more gigs control to the shared picker used by quick receipt and quick attachment flows, including attachment link capture.
- Preserve entered capture data, existing candidates, and the selected gig while an additional page loads or when loading fails.
- Represent loading, recoverable error, and exhausted-result states in the picker.
- Keep the initial nearby-candidate, ambiguity-warning, and auto-attach behavior unchanged.
- Cover the paging and filtering contract with backend integration tests; validate the dialog interactions manually for this change.

## Capabilities

### New Capabilities
- `quick-capture-gig-selection`: Progressive, user-requested selection of eligible gigs in receipt and attachment quick-capture dialogs.

### Modified Capabilities

None.

## Impact

- Backend quick-capture candidate lookup and authenticated `/gigs` API surface.
- Quick receipt and attachment hooks, shared gig picker, attachment-link candidate initialization, and frontend API types.
- Backend integration tests and the relevant UAT journey documentation for manual validation coverage.
