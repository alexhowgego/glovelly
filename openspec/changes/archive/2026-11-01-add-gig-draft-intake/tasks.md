## 1. Confirm integration boundaries

- [x] 1.1 Review existing unified-intake and gig-import implementations and specifications; confirm draft-success, transient-cleanup, failure/idempotency, and URL handling preserve receipt guarantees.
- [x] 1.2 Confirm Google document access and existing import review boundaries against the narrowed #284 scope; do not change set-list interpretation scope.

## 2. Model general intent and durable draft outcomes

- [x] 2.1 Add compatible Gig(s) intent, separate persistent override, general analysis evidence, and intake-correlated preparation/job state; retain legacy receipt analysis readability.
- [x] 2.2 Add user-owned batch source provenance and unique intake-to-batch linkage with required EF configuration/migration and storage lifecycle.
- [x] 2.3 Define backend/frontend analysis and outcome contracts distinguishing attachments from draft batches, including recovery identifiers and confidence/evidence.

## 3. Acquire and interpret supported sources

- [x] 3.1 Implement/test bounded pasted-text and PDF inputs, retaining originals and handling supported scans, unreadable input, and limits explicitly.
- [x] 3.2 Inspect existing Google permissions/APIs, choose least-privilege authenticated Docs acquisition, and implement canonical URL validation, bounded snapshot acquisition, reconnect and resource fallback.
- [x] 3.3 Implement schema-constrained multi-intent analysis without format-based routing; ensure only validated effective Receipt results reach existing automatic receipt logic.
- [x] 3.4 Implement validated one-or-many gig extraction into existing draft fields with missing-fact warnings, source evidence and visible confidence; do not create clients or fabricate facts.
- [x] 3.5 Integrate recoverable long-running processing with existing job conventions, owner checks, cancellation/limits and rejection of stale results after replacement or override.

## 4. Prepare drafts and recover safely

- [x] 4.1 Implement an explicit preparation command reusing existing batch/draft models and duplicate review safeguards without calling live-gig creation.
- [x] 4.2 Persist durable source/evidence before transient cleanup; implement unique preparation, concurrent replay protection and recovery after response/event/cleanup failure.
- [x] 4.3 Add authenticated source inspection and lifecycle cleanup without exposing tokens/storage internals or attaching source artifacts to eventual gigs.
- [x] 4.4 Provide manual editable-draft preparation with retained source when AI is unavailable, invalid or yields no useful proposals.

## 5. Extend the unified frontend journey

- [x] 5.1 Extend intake state/types and UI for general intent evidence, persistent Treat as override, supported source errors and recoverable processing; preserve receipt/resource interactions.
- [x] 5.2 Add explicit Prepare gig drafts and Draft batch prepared states with Review import/Done, with no existing-gig requirement and no automatic close/navigation/commit.
- [x] 5.3 Connect explicit Review import to the existing batch workspace and recovery to the same durable batch after refresh or response loss.
- [x] 5.4 Provide explicit Go to gig from resource success for normal downstream set-list interpretation without starting it in intake.
- [x] 5.5 Preserve notification policy, source inspection, accessible focus, mobile placement, and session-expiry handling; update proxy/service-worker bypass only if a new API prefix is added.

## 6. Prove safety and document the revised scope

- [x] 6.1 Add synthetic text, PDF and Google Doc fixtures covering one/many gigs, receipt PDFs, resource documents, conflicts, missing facts, low confidence, malformed output and unreadable scans.
- [x] 6.2 Add backend tests for unavailable AI/manual fallback, Google permission failure, retained source, override/retry, stale jobs, concurrent preparation, lost responses and cross-user isolation.
- [x] 6.3 Assert preparation creates no live gigs/clients/expenses/resources/set lists and retains existing explicit import commit and duplicate safeguards.
- [x] 6.4 Add receipt automatic-preference and explicit-resource regressions, plus frontend tests for ambiguity, draft handoff, input preservation and recovery.
- [x] 6.5 Update matching UAT journeys, task-led guide and engineering reference; document supported formats, Google access and deferred dedicated set-list routing. If settings change, update appsettings, both deployment environments and README together.
- [x] 6.6 Run backend tests serially, frontend tests and `./verify.sh`; run browser UAT only through Ubuntu CI and validate the completed OpenSpec change. Local verification and OpenSpec validation passed; browser UAT completed separately as confirmed by the user.
