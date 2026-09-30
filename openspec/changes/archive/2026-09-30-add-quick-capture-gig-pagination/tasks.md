## 1. Candidate Paging Contract

- [x] 1.1 Replace `FindCandidatesAsync` with one common candidate-query method that supports no-cursor initial nearby results and cursor-based expansion while preserving the initial cutoff-tie policy.
- [x] 1.2 Add an authenticated, read-only paged quick-capture candidate endpoint backed by the common method, with opaque keyset continuation, page exhaustion metadata, visibility enforcement, and cancelled-gig exclusion.
- [x] 1.3 Add compatible initial-candidate continuation metadata to quick receipt and attachment responses, and replace attachment-link's client-side candidate calculation with the shared server contract.
- [x] 1.4 Update frontend API types and add a shared client abstraction for candidate-page requests.

## 2. Quick-Capture Dialogs

- [x] 2.1 Extend `QuickCaptureGigSelect` with a separate accessible Load more gigs action and clear loading, recoverable-error, and exhausted states.
- [x] 2.2 Add candidate-page loading and ID-deduplicating append behavior to the quick receipt hook while preserving draft fields and selected gig.
- [x] 2.3 Add equivalent candidate-page loading and preservation behavior to quick attachment file and link capture flows.
- [x] 2.4 Wire modal props and styles so existing candidates remain available while a page loads and no draft mutation occurs until the user confirms an existing save action.

## 3. Validation

- [x] 3.1 Add backend integration tests for page continuation, stable total ordering, initial-result continuation, deduplication boundaries, visibility/cancelled filtering, and exhausted results.
- [x] 3.2 Run backend integration tests and frontend lint/build checks.
- [x] 3.3 Update the relevant UAT journey with manual validation for receipt and attachment expansion, data/selection preservation, loading/retry behavior, no-more-results state, and historical-gig access.
