## Why

Past completed and cancelled gigs crowd the default workspace even though upcoming and actionable work is more useful. The current selection is computed separately from the rendered list, so users can be shown an irrelevant or hidden gig detail.

## What Changes

- Hide past `Completed` and `Cancelled` gigs by default while retaining a clear session-scoped `Show past gigs` control.
- Use the user's local calendar date for gig visibility and related list calculations.
- Make the rendered, filtered, sorted gig collection the source of truth for the selected gig.
- Preserve a valid selection, fall back to the first visible gig when it is removed from the view, and clear selection for an empty result set.
- Make explicit gig navigation reveal a target by clearing incompatible list filters and enabling historical visibility when required, with a workspace message explaining the changed view.
- Add focused frontend tests for visibility, sorting, selection reconciliation, and explicit target reveal, with UAT coverage for the rendered controls and navigation.

## Capabilities

### New Capabilities
- `gig-list-visibility`: Defines historical gig visibility, list-derived selection, and explicit navigation behavior in the gig workspace.

### Modified Capabilities

None.

## Impact

- Affects `frontend/glovelly-web/src/hooks/useGigsWorkspace.ts`, `frontend/glovelly-web/src/components/GigsSection.tsx`, and gig navigation in `frontend/glovelly-web/src/App.tsx`.
- Adds a minimal frontend test runner and focused test files; existing Playwright UAT coverage and gig regression documentation will be extended.
- No API, database, or persisted-preference change. Gig dates remain required by the existing backend contract.
