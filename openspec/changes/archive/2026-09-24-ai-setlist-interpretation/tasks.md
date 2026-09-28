## 1. Source Grid And Persistence

- [x] 1.1 Replace the values-only Google Sheets retrieval contract with bounded, coordinate-preserving formatted grid retrieval and actionable bound/read failures.
- [x] 1.2 Add interpretation job persistence, status lifecycle, owner scoping, grid/result serialisation, source-evidence model fields, and the EF migration.
- [x] 1.3 Define and implement completed/failed interpretation-job source-grid retention and cleanup.
- [x] 1.4 Add stable draft-item identity and update set-list item DTOs/types while retaining the primary source row and persisted item-level evidence.

## 2. AI Interpretation Service

- [x] 2.1 Add the set-list interpretation abstraction, bounded prompt model, Vertex implementation, strict response schema, and configuration registration.
- [x] 2.2 Implement server-side interpretation validation for kinds, metadata, confidence, ordering, source rows/cells, source display values, and exclusion semantics.
- [x] 2.3 Add the queued interpretation worker/processor, owner-scoped start/status endpoints, safe failure handling, and workspace-event notification.
- [x] 2.4 Remove `ISetListSheetParser`, its implementation/registration, parser preview behavior, and parser-only tests.

## 3. Import Review And Chart Handoff

- [x] 3.1 Update the set-list import modal and API client types to start/poll/recover interpretation jobs and accurately label interpretation progress.
- [x] 3.2 Add retained-source-backed manual draft authoring with add/edit/reorder controls, retry behavior, and no deterministic parsing fallback.
- [x] 3.3 Update review/save flows to preserve source evidence and stable item identity for AI and manual drafts.
- [x] 3.4 Update deterministic candidate lookup and optional AI chart-matching request/result correlation to use stable item identity and only reviewed Song items.

## 4. Verification And Documentation

- [x] 4.1 Add sanitised coordinate-preserving grid fixtures for sparse, detailed, simple, and varied non-template layouts.
- [x] 4.2 Add interpretation service tests for valid extraction, false-positive prevention, medleys/transitions/spares, schema validation, and source-evidence bounds.
- [x] 4.3 Add endpoint/job integration tests for user scoping, grid acquisition failures/bounds, polling recovery, AI failure/retry, and manual-authoring recovery.
- [x] 4.4 Add chart-handoff tests proving no matching before valid draft creation and no identity collisions for multi-row evidence.
- [x] 4.5 Update `docs/uat/gig-external-resources.md` for AI interpretation, manual recovery, mobile job recovery, and downstream chart matching.
- [x] 4.6 Run the relevant backend tests, frontend lint/build, and `./verify.sh`.
