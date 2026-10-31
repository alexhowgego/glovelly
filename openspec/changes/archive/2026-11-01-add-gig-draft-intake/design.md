## Context

`CurrentIntake` currently has Unknown/Receipt/Resource intents and receipt-shaped evidence. `IntakeAnalysisService` sends non-URL sources to receipt analysis; URL handling is metadata-only. `IntakeWorkflowService` may automatically apply receipts according to user preference. `useUnifiedIntake` assumes successful application attaches to an existing gig.

`GigImportEndpoints` already provides draft editing, duplicate warnings, and explicit commit, but no production batch preparation endpoint. Reuse those review/commit safeguards. Set-list interpretation currently depends on an attached gig resource; leave that workflow unchanged.

## Goals / Non-goals

Goals: recognise gig information without format-based routing; prepare reviewable one-or-many draft batches; retain sources and evidence; make failures and interrupted requests recoverable; preserve existing receipts/resources.

Non-goals: live-gig application from intake, dedicated set-list routing, broad document ingestion, and automatic source attachment to imported gigs.

## Decisions

### 1. Separate source acquisition, intent analysis, extraction, and application

Flow:

```text
text / PDF / URL
       |
bounded source acquisition -> validated intent proposal + evidence
       |                              |
       |                       authoritative override
       |                              |
       +------------------------------+
                                      |
           +--------------------------+---------------------+
           |                          |                     |
        Receipt                    Resource               Gig(s)
  existing preferences       explicit attachment     validated extraction
                                                            |
                                                   prepare draft batch
                                                            |
                                                   review -> explicit commit
```

Use a general intent-analysis contract, not a fabricated receipt result for gig data. Keep receipt field extraction and application rules in their existing shared engine/services. Only a validated effective Receipt intent may reach automatic receipt application. Ambiguous/failed classification must not accidentally trigger that path.

Persist the user-selected override separately from the AI proposal and analysis attempts. Retry may update the proposal but must not replace the override. Override alone creates no business data. Switching treatment invalidates incompatible prepared extraction, not the source.

### 2. Support a bounded source matrix

- Pasted text/email: bounded original text, with stable paragraph/line references where feasible.
- PDFs: retain original bytes and use the provider's supported document input, including scanned documents where supported. Enforce configured byte/page/provider limits; unreadable or unsupported scans fail visibly, never as a successful empty import.
- Google Docs URLs: recognise canonical supported document URLs and read content using the authenticated user's Google connection and least-privilege supported access. Do not fetch arbitrary URL contents or use public-page scraping. Preserve document section/paragraph references where available.
- Other URLs, including Sheets: retain existing metadata-based resource behaviour; unsupported gig extraction is explained rather than silently guessed.

No upfront intent choice is required. Google document reads are source acquisition for AI analysis, not permission to attach or create gigs. The URL itself remains recoverable if access fails. Offer reconnect/retry, paste text, upload PDF, or explicit resource treatment. Use the Google Docs API with the `documents.readonly` scope: the user has explicitly chosen broad read-only Docs access to support pasted URLs over a file-picker-based `drive.file` flow. Explain the broader access during consent and request it independently of resource attachment.

Expose Docs status, connection and local disconnection in Services independently of Sheets. Distinguish missing permission, access failure and source size/empty failures without logging tokens, URLs or document contents.

### 3. One extraction capability produces one or many draft rows

Use a strict structured schema with bounded collections/strings, typed dates/times/amounts, confidence, warnings, and source references. Validate all output server-side before persistence. Treat source contents as untrusted data, not instructions.

For text and Google Docs snapshots, provide bounded server-numbered source sections and resolve model-cited section IDs against the retained snapshot. An invalid or missing citation does not discard unrelated proposals: clear the unverified claim, mark the affected row low confidence and add a visible warning. PDF page references are not independently verified and must be labelled accordingly. Malformed business fields remain rejected. Log only counts and safe failure categories, not source or model text.

Map validated results to existing `GigImportBatch`/`GigImportDraft` shapes. Do not invent missing years, dates, fees, clients, or destinations; represent uncertainty as missing values and warnings. Proposed existing-client links must pass authenticated visibility checks. No clients are created during preparation. Run existing duplicate detection at review and commit.

Keep interpretation confidence distinct from per-row/field extraction confidence. Preserve source-backed evidence beyond free-form AI explanations. Low-confidence but structurally valid extraction remains editable and visibly uncertain; malformed output cannot become an accepted draft.

Supply the current Europe/London reference date to extraction. The prompt resolves missing years and relative weekdays with visible uncertainty. Validate cited explicit years and reject distant year guesses when the text contains no explicit year; retain the source for retry or manual correction. Never overwrite an explicit past year.

### 4. Preparation is a durable, idempotent handoff

Use an explicit Prepare gig drafts action after analysis/override. Persist a batch and source provenance under a user-scoped intake identity with a uniqueness guarantee. Repeated, concurrent, or interrupted requests recover the same batch. A completed intake identity cannot be repurposed for a second treatment; replacing the source requires a new identity.

Store original text/PDF or a bounded Google Doc snapshot, the original URL where applicable, source fingerprint, acquisition metadata, and validated extraction evidence in durable batch-owned provenance. Do not depend on the transient current intake or a later mutable remote document for review. Source inspection must use authenticated ownership checks and existing safe storage/download conventions.

Establish durable provenance and batch persistence before removing temporary intake state. Avoid holding a database transaction open over provider calls; stage storage safely and clean failed orphan writes without deleting recoverable originals. Recovery must work after persistence succeeds but response/event/cleanup fails.

### 5. Keep long-running work recoverable

Use existing background-job conventions for potentially long PDF/Google Doc analysis and extraction, with bounded execution, user-scoped job reads, correlation to intake identity/fingerprint and treatment, and safe recovery after refresh. Reject stale results after source replacement, discard, or override. Reuse existing machinery where practical rather than duplicating queue infrastructure.

Provider unavailability, malformed output, and access failure leave input intact. Manual recovery allows the user to prepare/edit an uncommitted draft with source provenance without requiring a successful AI result. Zero extracted gigs is an explicit no-gigs result, not a misleading successful batch.

### 6. Represent attachment and draft outcomes separately in the frontend

Extend intake types/state with analysis progress, effective intent/override, preparation progress, and a durable Draft batch prepared result. Do not model a batch as an attachment or require selecting an existing gig for gig preparation.

Show source, proposal, evidence, and Treat as controls in the existing unified presentation. Draft success offers Review import and Done and does not automatically navigate or close. Review import explicitly opens the existing batch workspace. Resource success retains the existing flow and offers explicit Go to gig access to the saved destination so users can invoke normal set-list interpretation. Never start interpretation automatically.

Retain inline validation/progress/still-open-modal failures; use the existing notification wrapper only where policy calls for terminal feedback outside the initiating UI. Preserve modal layering and mobile controls.

## Risks / Trade-offs

- Additional Google read permissions may require reconnect/consent and provider verification. Keep ordinary resource attachment usable without document-read permission.
- Google Docs content retrieval deliberately changes the existing metadata-only unified-intake specification; update that requirement explicitly while keeping Sheets and other URLs metadata-only.
- Durable source snapshots require ownership, cleanup and deletion semantics. Batch deletion must clean batch-owned artifacts according to existing retention conventions; source references must not expose storage keys or tokens.
- Classification adds cost/latency. Reuse acquired content and validated analysis where possible; preserve receipt budget enforcement and ensure provider invocations are bounded.
- Broadening upload validation must not weaken receipt-specific validation or accept arbitrary files as supported gig documents.

## Migration Plan

Add compatible enum values without renumbering existing persisted values; migrate override, general analysis/job state, source provenance, and idempotent batch linkage as required by the final model. Preserve existing intake/receipt JSON consumers during coordinated frontend/backend rollout. Keep old receipt analysis records readable.

If new runtime settings are introduced, update appsettings, staging and production workflow bindings, README, and engineering documentation together. Update API proxy/service-worker bypass only if introducing a new prefix. No set-list model migration is intended.

## Verification

Integration tests must prove preparation never creates live gigs/clients/expenses/resources/set lists, commit still requires existing explicit review actions, sources survive all recoverable failures, and cross-user/stale requests cannot expose or modify results. Test receipt preferences and explicit resource attachment as regressions. Add frontend tests and UAT journeys for source ambiguity, override/retry, batch handoff, Google access failure, scans, and mobile feedback. Run browser UAT only in Ubuntu CI.
