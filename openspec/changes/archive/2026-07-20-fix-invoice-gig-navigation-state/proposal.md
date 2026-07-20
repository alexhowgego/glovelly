## Why

Issue #206 exposed a production data-loss path: navigating from a generated invoice line to a second gig after editing a first gig can leave the second gig's editor populated with the first gig's saved form. Saving then overwrites the second gig's details and expenses, requiring a database rollback to recover.

## What Changes

- Route every cross-workspace gig navigation through gig-selection behavior that keeps the selected record and editable form state aligned.
- Preserve the existing explicit-discard behavior when a user has unsaved gig edits before navigating to another gig.
- Add browser UAT coverage for editing one gig, regenerating its linked draft invoice, navigating to a different linked gig from invoice lines in the same browser session, and verifying neither record is overwritten.
- Update the corresponding UAT documentation to describe the automated regression coverage.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `uat-browser-automation`: Cross-workspace invoice-line navigation must verify the selected gig editor is hydrated from the target record and cannot persist data from a previously edited gig.

## Impact

- Frontend gig workspace selection and cross-workspace navigation callbacks, principally `App.tsx` and `useGigsWorkspace.ts`.
- Playwright UAT tests, likely the cross-workspace navigation suite and its shared invoice helpers.
- `docs/uat` cross-workspace navigation guidance.
- No API contract, database schema, or external-service dependency changes are expected.
