## Context

Quick receipt and attachment draft creation calls `GigQuickCaptureSupport.FindCandidatesAsync`, which returns a limited set of date-nearby visible, non-cancelled gigs. The shared `QuickCaptureGigSelect` is a native select containing only those candidates. Receipt and attachment hooks preserve their pending values in modal state, but neither has candidate-expansion state. Attachment link capture separately derives initial candidates in `App.tsx`, duplicating the backend policy.

The normal quick-capture path must remain fast and behaviorally stable: the 30-day candidate range, candidate-count cutoff with distance ties, ambiguity warning, and automatic selection are not browse pagination.

## Goals / Non-Goals

**Goals:**
- Let users progressively browse every gig visible to them that is not cancelled, including historical gigs, without closing a quick-capture dialog.
- Provide one server-side eligibility and ordering contract for receipt, attachment file, and attachment link capture.
- Keep the nearby initial-candidate and automatic-attachment policies independent from user-requested expansion.
- Preserve candidate list, selection, and pending capture fields through expansion loading and retryable failure.
- Establish backend integration coverage and manual validation steps for the dialog behavior.

**Non-Goals:**
- Adding arbitrary search, filters, date-window stages, or a custom combobox to the picker.
- Changing which gig the initial quick-capture request automatically attaches to, or changing ambiguity warnings.
- Allowing cancelled or inaccessible gigs to be selected.
- Uploading, saving, moving, or modifying a draft as a side effect of loading candidates.
- Introducing a frontend automated test framework in this change.

## Decisions

### Use one candidate query with initial and continuation modes

Replace `FindCandidatesAsync` with one common candidate-query method used by both the draft endpoints and a read-only authenticated quick-capture candidate API under `/gigs`. With no cursor, the method returns the existing initial nearby result, including its configured candidate-count cutoff and distance ties. With a continuation cursor, it returns the next bounded page from all eligible gigs. Each result includes candidates, `hasMore`, and an opaque continuation value.

The receipt and attachment draft endpoints use the no-cursor result for their existing automatic-selection and ambiguity evaluation. Attachment-link initialization and Load more gigs use the API response directly. This avoids duplicate candidate policies while keeping an explicit browse operation from altering draft creation or automatic-selection behavior. Reusing the existing unpaged general gig list was rejected because it exposes a heavier, differently shaped contract and does not establish the required paging, filtering, or ordering guarantees.

### Page all eligible gigs by proximity

The expandable collection includes all gigs visible to the authenticated user except cancelled gigs. It has no date-range restriction. Order candidates by absolute distance from today, then gig date, title, and gig ID as the final tiebreaker.

The existing initial result remains a nearby subset. Its continuation begins after the final initial result in that same total order, so loading more appends the closest remaining eligible gigs, including historical gigs, without repeating the initial set. When the initial result is empty but eligible gigs exist, its continuation represents the start of explicit expansion. Date-window expansion was rejected because it creates additional policy and boundary/overlap behavior without improving direct access to an old valid gig.

### Use keyset continuation and defensive client deduplication

Encode the final item ordering values in an opaque cursor and request a bounded next page. The server determines `hasMore` by reading one item beyond the page limit. A total server order plus keyset continuation avoids unstable offset pages during concurrent changes. The client appends by gig ID defensively so an overlapping or retried response cannot produce duplicate options.

### Keep picker state explicit and shared

Extend `QuickCaptureGigSelect` with load-more availability, loading, exhausted, and retryable-error inputs. Receipt and attachment hooks own the request lifecycle and candidate list because they retain their respective pending draft state. They preserve `selectedGigId` if it remains present, leave existing candidates rendered during loading, and do not call a save/move endpoint as part of expansion.

A native select remains the value control. The Load more gigs action is a separate accessible button rather than a select option, avoiding action-like option behavior across keyboard and mobile interaction.

### Validation scope

Backend integration tests will exercise page boundaries, stable ordering, visibility and cancelled filtering, continuation/exhaustion, and no-more-results behavior. Dialog-level loading, retry, and preservation behavior will be manually validated and documented in the applicable UAT journey; no frontend test runner is introduced.

## Risks / Trade-offs

- [A user with a large history may need several pages to reach a known old gig] → Results are proximity ordered and preserve the fast nearby default; search/filtering can be considered later if manual validation indicates a discoverability problem.
- [Initial cutoff ties can make the initial page larger than the configured count] → Generate the continuation from the last returned initial candidate rather than a nominal page size.
- [Gigs change while a dialog is open] → Keyset pagination yields deterministic page boundaries for the cursor ordering, and client ID deduplication prevents duplicate options; deleted or newly inaccessible gigs are naturally absent from future pages.
- [Duplicated candidate policy drifts between attachment-link UI and backend] → Route all initial and expanded candidate retrieval through the shared server-side contract.
- [Expansion failures could be mistaken for draft-save failures] → Store and render a distinct recoverable expansion error while retaining draft fields and existing candidate options.

## Migration Plan

Deploy the new read-only endpoint and frontend support together. Existing quick-capture create/update endpoints retain their response behavior, with additive continuation metadata where needed. No data migration is required. Rollback consists of reverting the frontend control and endpoint; no persisted state or draft data is changed by candidate paging.

## Open Questions

None.
