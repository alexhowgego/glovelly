## 1. Receipt Analysis Foundation

- [x] 1.1 Add receipt-analysis settings for Vertex project/location/model, supported media types, analysis size limit, timeout, rate limit, and enabled/configured state.
- [x] 1.2 Add `ReceiptAnalysis` persistence, status enum, attachment relationship, EF configuration, migration, and indexes for attachment history/latest-attempt lookup.
- [x] 1.3 Define transport/domain contracts for an attachment target, analysis result, field confidence, warnings, and safe failure status without adding fields to `GigExpense`.
- [x] 1.4 Add a domain receipt-analysis service interface and registration that keeps Vertex client construction narrowly shared with existing Vertex usage where practical.

## 2. Vertex Extraction And Validation

- [x] 2.1 Implement stored-attachment loading and analysis eligibility checks for the configured MIME allowlist and AI-specific size limit.
- [x] 2.2 Implement the Vertex multimodal inline-media request with minimal context, JSON-only structured output controls, prompt versioning, and cancellation-aware timeout.
- [x] 2.3 Implement strict response parsing and field-level validation for merchant, ISO transaction date, invariant decimal total, ISO currency, allowed category, confidence values, and bounded warnings.
- [x] 2.4 Persist successful and failed analysis attempts with provider/model/prompt provenance, safe errors, and bounded raw structured output if approved by retention policy.
- [x] 2.5 Add privacy-safe structured logging, per-user receipt-analysis rate limiting, and explicit failure classification without recording receipt or extraction content.

## 3. Attachment APIs

- [x] 3.1 Add authenticated attachment-level endpoints to retrieve the latest analysis and create a new analysis attempt using existing attachment visibility rules.
- [x] 3.2 Return stable API response shapes for successful suggestions, pending request state if applicable, and safe failures without exposing storage keys or raw provider content.
- [x] 3.3 Ensure analysis failure, retry, invalid response, unavailable configuration, unsupported media, and rate-limit responses leave attachments and `GigExpense` values unchanged.

## 4. Review Experience

- [x] 4.1 Add frontend API types and a shared receipt-analysis hook/modal for loading an attachment's latest analysis, requesting analysis, viewing warnings/confidence, and reanalysing.
- [x] 4.2 Add an Analyse receipt action to each existing expense attachment in the gig expenses panel and route it through the shared review experience.
- [x] 4.3 Add quick-receipt hand-off from its saved-attachment state to the same shared review experience.
- [x] 4.4 Implement explicit merchant/total application callbacks that prefill existing editable description/amount controls but do not save automatically.
- [x] 4.5 Display transaction date, currency, and category as clearly labelled review-only suggestions with no `GigExpense` persistence path.
- [x] 4.6 Add responsive and accessible visual states for idle, analysing, successful, failed, and low-confidence/warning results.

## 5. Verification And Operational Readiness

- [x] 5.1 Add backend tests for ownership scope, media/size limits, request shaping, valid and partially valid parsing, malformed/provider failures, provenance, retries, logging-safe behavior, and no automatic expense mutation.
- [x] 5.2 Add endpoint/integration tests for latest-result retrieval, analysis creation, safe failures, rate limits, and quick-capture attachment hand-off.
- [x] 5.3 Add frontend build/lint coverage and update relevant browser UAT documentation/tests for analysing both historical and quick-captured attachments.
- [x] 5.4 Update privacy documentation and deployment/configuration guidance for user-triggered Vertex receipt processing, IAM, logging limits, retention, and cost/usage monitoring.
- [x] 5.5 Evaluate a consented representative receipt set for extraction accuracy, failure modes, latency, and per-receipt cost; record whether asynchronous post-upload analysis is justified.
- [x] 5.6 Run `./verify.sh` and resolve all resulting failures.
