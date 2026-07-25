## Context

The gig workspace currently sorts and filters the full gig collection in `useGigsWorkspace`, but initial load and deletion select the first API-order entry. The selected gig is resolved from all loaded gigs before the rendered list, so the detail panel can display a gig that is absent from the list. Date comparisons use UTC date strings even though the intended list behavior is based on the user's local calendar date.

The change affects the hook that owns gig state, the gig-list controls, and callers that request selection, including invoice-line navigation. The API already supplies required date-only values and needs no change.

## Goals / Non-Goals

**Goals:**

- Make one ordered visible collection authoritative for both the gig list and normal selection behavior.
- Default the workspace to active work while retaining an obvious, session-only historical visibility control.
- Let explicit navigation reveal its requested target consistently, rather than creating separate exceptions for deep links and saved gigs.
- Add deterministic frontend coverage for local-date and state-transition rules.

**Non-Goals:**

- Persisting list filters or `Show past gigs` between sessions.
- Allowing undated gigs or changing backend gig validation.
- Changing API filtering, database schema, lifecycle rules, or the existing sort options.
- Retaining hidden selections after a passive list-state change.

## Decisions

### Use a pure gig-list state module

Extract local-date formatting, historical inclusion, sorting/filtering, selection reconciliation, and target-reveal calculation from the React hook into a small pure frontend module. The hook remains responsible for React state, unsaved-editor confirmation, requests, and rendering inputs.

This gives tests a supplied local `today` value rather than relying on browser timezone or clock behavior. Keeping the logic embedded in the hook would be a smaller code movement, but would require component-level test infrastructure to cover the same state matrix.

### Apply one visibility pipeline before selection

The pipeline is: historical inclusion, type and quick filters, text search, then the configured stable sort. The final ordered result is the list rendered by `GigsSection` and the only normal source for selected-gig resolution.

The selected ID is retained only while it appears in that result. Otherwise the hook uses the first visible ID, or an empty ID for an empty list. This prevents detail/list disagreement during filter changes, edits, deletion, initial load, and server refreshes.

### Treat explicit selection as intent to reveal

All explicit selection paths route through `selectGig`. If the requested gig is not visible, the action clears the search, type, and quick filters; enables `Show past gigs` when the target is a normally hidden past `Completed` or `Cancelled` gig; and then selects the target. Sorting remains unchanged. A workspace status message states whether filters were cleared and/or past gigs were shown.

This central rule covers row-adjacent programmatic navigation, invoice-line deep links, and newly saved gigs without separate per-caller behavior. Passive state changes do not reveal a hidden selection; they reconcile to the current list instead.

### Keep historical visibility session-scoped

`Show past gigs` is local React workspace state and resets with the rest of the workspace. Existing list controls are also session-scoped, and persistence would require a user-preference contract that is outside this change.

### Add focused Vitest coverage plus targeted UAT

Add the minimal Vitest setup needed to test the pure module. Unit tests cover visibility, local-date boundaries, sorting, reconciliation, and reveal instructions. Playwright UAT tests remain responsible for proving the control and invoice-line navigation wire those rules into the actual UI.

## Risks / Trade-offs

- [Extracted functions duplicate existing list logic during transition] -> Move the existing logic rather than retaining parallel implementations, and make the hook consume only the extracted result.
- [React updates can temporarily combine stale selected IDs with new filters] -> Derive the rendered selected gig from the visible collection and reconcile the stored ID whenever list inputs change.
- [Clearing filters on explicit navigation changes the user's view] -> Limit resets to an explicit request for a target that is not visible, preserve sort order, and display an explanatory status message.
- [Local date tests can be timezone-sensitive] -> Pass a `YYYY-MM-DD` local date into pure functions rather than constructing dates from UTC timestamps in assertions.

## Migration Plan

The change is frontend-only and deploys with the existing application build. No data migration, API versioning, or rollback procedure is needed; reverting the frontend release restores the previous list behavior.

## Open Questions

None. The selected behavior is session-scoped history visibility and automatic target reveal for explicit selection.
