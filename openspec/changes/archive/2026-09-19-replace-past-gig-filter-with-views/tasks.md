## 1. Gig List Views

- [x] 1.1 Add `work-queue` to the gig quick-filter type and make it the workspace default and reset view.
- [x] 1.2 Replace the global historical-gig gate in the pure list-state helper with the six specified self-contained view predicates.
- [x] 1.3 Remove `showPastGigs` state and its filter, component, and App prop plumbing.
- [x] 1.4 Replace the historical-gigs control with ordered chips for `Work queue`, `Upcoming`, `Uninvoiced`, `Drafts`, `Completed`, and `All`.

## 2. Explicit Navigation

- [x] 2.1 Update hidden-gig reveal results and callers to clear search and type filters, select `All`, preserve sorting, and describe the changed view without referring to past-gig visibility.

## 3. Coverage And Documentation

- [x] 3.1 Expand pure list-state tests to cover every named view, including historical completed uninvoiced gigs, historical drafts, and cancelled records.
- [x] 3.2 Update reveal tests to assert `All` view selection for hidden targets.
- [x] 3.3 Replace obsolete historical-gig-control steps in the Gig History And Selection UAT journey with the named-view and All-based reveal behavior.
- [x] 3.4 Sync the completed `gig-list-visibility` delta specification to the main specification.

## 4. Verification

- [x] 4.1 Run `npm --prefix frontend/glovelly-web test -- --run src/hooks/gigListState.test.ts`.
- [x] 4.2 Run `npm --prefix frontend/glovelly-web run lint` and `npm --prefix frontend/glovelly-web run build`.
