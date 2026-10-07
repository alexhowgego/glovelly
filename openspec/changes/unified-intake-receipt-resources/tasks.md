This checklist replaces the first uncommitted implementation's completed tasks. Existing foundations are candidates for reuse, not presumed compliant with the revised journey. Complete tasks only after verifying the replacement against the revised specifications.

## 1. Inventory And Contracts

- [x] 1.1 Inventory intake entry points, hooks/modals, endpoints, tests, and consumer dependencies; identify reusable domain services and superseded orchestration without reverting unrelated work.
- [x] 1.2 Define explicit source-selection/upload/review presentation states and upload lifecycle transitions, including recoverable analysis/application failures and close/reopen behavior.
- [x] 1.3 Replace frontend-specific fast-path eligibility contracts with analysis and authoritative application-result contracts shared by automatic and explicit saving; update TypeScript types.
- [x] 1.4 Define retry-safe application identity/result handling that prevents duplicates after overlapping requests or interrupted responses without retaining completed private source blobs or adding history.

## 2. Backend Foundation And Shared Application

- [x] 2.1 Reverify owner-scoped current intake, analysis attempts, original-source linkage, replacement/discard, and successful row/blob cleanup; reuse existing persistence/migrations where correct.
- [x] 2.2 Reverify source validation, MIME/size limits, URL metadata/type inference without content fetching, text limits, provider cancellation, rate limiting, validated evidence, and safe telemetry.
- [x] 2.3 Extract or reuse shared receipt/resource application operations with ownership, storage, event publication, and draft-only invoice refresh; keep endpoint orchestration concise.
- [x] 2.4 Implement backend proactive receipt saving from validated analysis using saved user preference and configured nearest-gig ranking/local clock; other candidates must not block application.
- [x] 2.5 Implement explicit receipt and resource application against the same retry-safe operation, preserving resource type/purpose/title validation and manual treatment after analysis failure.
- [x] 2.6 Preserve independent high-confidence merchant, total, and category initialization in both receipt modes and ordinary category editing without altering invoice wording/reimbursement.
- [x] 2.7 Return authoritative saved destination and attachment identifiers for attached state and review while clearing successful private intake; keep failures recoverable.

## 3. Rebuilt Presentation

- [x] 3.1 Make the sole global Add to Glovelly action open source type selection; enter unified upload before file acquisition or URL/text submission.
- [x] 3.2 Rebuild the workflow hook around explicit states and focused presentational components; derive busy/status behavior and avoid large coordination bodies in App.tsx.
- [x] 3.3 Implement stable upload acquisition, analysis progress, evidence, intent override, gig selection, read-only pagination, retry, and explicit application within the same dialog.
- [x] 3.4 Display one compact Attached to [Gig] state after either application mode with Review attachment and Done; remove automatic close/navigation/review transitions.
- [x] 3.5 Make Done/close after confirmed attachment preserve the saved item; retain unapplied source on earlier close and reopen clean acquisition after completed intake cleanup.
- [x] 3.6 Adapt saved receipt/resource review to open only on explicit Review attachment, using authoritative identifiers for source inspection, corrections, reassignment, and deletion without re-uploading.
- [x] 3.7 Align Settings label/select layout and retain focus/hover explanation for Automatic receipt application; persist/return active preferences and normalize legacy Very high confidence display to High confidence.
- [x] 3.8 Preserve existing responsive styling, progress, modal overlay placement, inline modal feedback, and notification policy in all three phases.

## 4. Remove Superseded Workflow

- [x] 4.1 Migrate consumers and remove retired acquisition hooks/components/actions and redundant application routes while retaining APIs used by saved-record correction.
- [x] 4.2 Remove fastPathGigId, branch-specific completion/review reasons, ambiguity gates, obsolete state flags, and consumer compatibility wrappers once replaced.
- [x] 4.3 Replace tests and UAT assumptions tied to the old visibly distinct fast path; retain meaningful domain, visibility, validation, and invoice regressions.

## 5. Verification And Documentation

- [x] 5.1 Add backend integration coverage for proactive nearest-gig application with multiple candidates, all preference/confidence outcomes, no-candidate handling, and explicit resource confirmation.
- [x] 5.2 Verify repeated/overlapping/interrupted-response application cannot duplicate saves, and verify source linkage and cleanup across success, replacement, discard, and recoverable failures.
- [x] 5.3 Verify owner visibility, unavailable/malformed/unsupported analysis, independent field confidence, ordinary category editing, reassignment, and draft versus finalized invoice behavior.
- [x] 5.4 Update matching UAT journeys for the source-selection/upload/optional-review sequence, stable attached state, mobile early finish, close/reopen, corrections, and failures; keep notification/persistent-error/placement coverage.
- [x] 5.5 Update applicable README, engineering/privacy and runtime/deployment documentation when retained-source handling or settings change; keep Google Docs/Sheets content analysis deferred.
- [x] 5.6 Run ./verify.sh and git diff --check, and compile the updated browser UAT suite without executing it locally.
- [ ] 5.7 Run browser UAT/documentation capture in the Ubuntu CI pipeline after deployment and review the results there (CI handoff; never run locally).

Local verification: 441 backend tests and 36 frontend tests passed; frontend lint/build, UAT compilation, strict OpenSpec validation, and migration-model consistency checks passed. Live browser execution and screenshot review remain the CI handoff above.
