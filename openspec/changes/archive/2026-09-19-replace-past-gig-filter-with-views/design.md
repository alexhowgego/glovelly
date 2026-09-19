## Context

The gig workspace currently stores a quick filter and independent `showPastGigs` state. `getVisibleGigs` applies the historical gate before the selected quick filter, so the latter cannot describe a complete result set. `getGigReveal` and its callers compensate by clearing filters and enabling the historical gate for an explicitly selected hidden gig.

The list state is a pure, unit-tested frontend helper. The workspace hook owns its state and passes the selected filter to the Gigs section through App. Existing UAT and the `gig-list-visibility` specification describe the historical gate and must be revised with the UI behavior.

## Goals / Non-Goals

**Goals:**
- Make each visible quick-filter label define the whole gig population it can return.
- Default the workspace to a useful work queue without concealing completed uninvoiced work.
- Preserve current search, type filtering, sorting, selection reconciliation, and navigation behavior where their semantics remain valid.
- Make explicit reveal deterministic by selecting the complete `All` view.

**Non-Goals:**
- Correct stale gigs that remain `Confirmed` after their date has passed.
- Change priority sorting, gig statuses, invoice-generation rules, APIs, or persisted user preferences.
- Add a date-range filter or persist list controls between workspace sessions.

## Decisions

### Model visibility exclusively as a selected view

Remove `showPastGigs` from the list-filter type, workspace state, component props, and reveal result. The selected `GigQuickFilter` gains `work-queue` and remains the sole source of view membership.

Applying a global historical-date filter after a quick filter was rejected because it recreates the hidden constraint that caused the defect. Treating `Show past gigs` as a special quick-filter would also leave the named views incomplete and require users to combine controls.

### Define view predicates in the pure list-state helper

`getVisibleGigs` will select candidates by one predicate before applying type, search, and sorting:

| View | Candidate gigs |
| --- | --- |
| Work queue | All drafts; non-cancelled gigs dated today or later; completed, uninvoiced gigs |
| Upcoming | Non-cancelled gigs dated today or later |
| Uninvoiced | Completed and not invoiced gigs |
| Drafts | Draft gigs |
| Completed | Completed gigs |
| All | Every gig |

The broad Upcoming predicate intentionally retains future drafts and completed records, as agreed. `Work queue` reuses that same upcoming category and adds historical actionable work. A narrow `Confirmed`-only interpretation was rejected because it would make the default queue less complete than the agreed Upcoming view.

### Reveal hidden targets through All

When explicit selection targets a gig outside the rendered collection, clear search and type filters, select `All`, preserve sorting, and select the target. The status message will describe filters being cleared without implying a separate historical setting. `All` includes cancelled and historical gigs, making it the only view that universally fulfils reveal intent.

### Test behavior at the pure list-state boundary

Replace historical-gate unit tests with a representative mixed dataset checked against every named view, including historical completed uninvoiced records, historical drafts, cancelled records, and future records. Keep selection/reveal tests, updating expected reveal results to choose `All`.

## Risks / Trade-offs

- [A user expects Work queue to be only confirmed scheduled work] -> Document and test the intentionally broad Upcoming predicate, including future drafts and completed records.
- [Selection can change when the default view changes] -> Retain reconciliation against the rendered collection and cover fallback selection through the existing tests and UAT.
- [A call site retains removed historical state] -> Remove the state and props at the type boundary so TypeScript identifies all remaining references.
- [The UAT retains obsolete language] -> Replace the Gig History And Selection journey with named-view assertions and All-based reveal behavior.

## Migration Plan

1. Deploy as a frontend-only change; existing list controls are session-local and require no data migration.
2. Existing sessions receive the new default Work queue when the updated client loads.
3. Roll back by deploying the prior frontend bundle; no stored state or backend contract requires reversal.

## Open Questions

None.
