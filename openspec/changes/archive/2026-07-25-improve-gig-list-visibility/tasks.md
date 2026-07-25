## 1. List-State Foundation

- [x] 1.1 Add the minimal Vitest configuration and scripts required for frontend unit tests.
- [x] 1.2 Extract pure local-date, historical visibility, filtering, sorting, selection reconciliation, and target-reveal functions for the gig workspace.
- [x] 1.3 Add unit tests for default historical visibility, past Draft/Confirmed visibility, today boundary behavior, composed filters, and sort order.
- [x] 1.4 Add unit tests for initial selection, preserved visible selection, fallback selection, empty results, and explicit target reveal instructions.

## 2. Gig Workspace Behavior

- [x] 2.1 Add session-scoped `Show past gigs` state and integrate the pure list-state result into `useGigsWorkspace`.
- [x] 2.2 Reconcile stored and rendered selection against the final visible ordered gig list for load, refresh, filtering, editing, and deletion.
- [x] 2.3 Route saved-gig and deep-link selection through the explicit target-reveal behavior, clearing incompatible filters and enabling historical visibility as needed.
- [x] 2.4 Replace list-specific UTC date comparisons with the local calendar-date helper, including upcoming and summary calculations.

## 3. Gig Workspace UI

- [x] 3.1 Add an accessible, visually active `Show past gigs` control to the gig list controls.
- [x] 3.2 Render the reconciled selection and appropriate empty-detail state when no gigs are visible.
- [x] 3.3 Display a clear workspace status message when explicit navigation changes filters or historical visibility to reveal a gig.

## 4. Regression Coverage

- [x] 4.1 Extend Playwright UAT coverage for historical visibility, the `Show past gigs` control, and list-derived initial/fallback selection.
- [x] 4.2 Extend invoice-line navigation UAT coverage for opening a historical gig hidden by the active view.
- [x] 4.3 Update the gig section of `docs/uat/pre-merge-regression.md` with the historical visibility and target-reveal journey.
- [x] 4.4 Run frontend unit tests, frontend lint/build, and the affected UAT suite.
