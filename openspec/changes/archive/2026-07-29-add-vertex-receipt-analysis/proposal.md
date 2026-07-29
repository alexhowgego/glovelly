## Why

Receipt capture already preserves the evidence and creates an editable expense draft, but users must manually transcribe routine receipt details. Glovelly already has a configured Vertex AI/Gemini integration pattern, so a constrained, review-first receipt analysis can test whether multimodal extraction saves effort without making AI output part of the accounting record automatically.

## What Changes

- Add an on-demand Vertex AI receipt analysis action for any existing expense attachment, including attachments created through quick receipt capture.
- Persist attachment-bound analysis attempts, validated suggestions, provider/model/prompt provenance, warnings, and safe failure state separately from `GigExpense`.
- Present a shared review experience that clearly distinguishes AI suggestions from saved expense data and requires explicit user action before copying merchant and total into editable expense fields.
- Surface transaction date, currency, and suggested category as review-only analysis output; do not expand the expense/accounting data model in this change.
- Enforce supported analysis media types, an AI-specific size limit, deterministic structured-output validation, rate limiting, privacy-conscious logging, and graceful manual-workflow fallback.

## Capabilities

### New Capabilities
- `vertex-receipt-analysis`: Analyze a stored expense receipt with Vertex AI and expose validated, reviewable suggestions without automatically changing financial records.

### Modified Capabilities

- None.

## Impact

- Backend: attachment-level APIs, EF Core persistence/migration, attachment blob reads, Vertex AI request construction, settings, validation, logging, and integration tests.
- Frontend: attachment actions in the gig expenses panel, shared receipt-analysis review state/UI, quick-receipt hand-off, API types, and styling.
- Operations and policy: Vertex AI configuration/permissions, provider usage monitoring, and privacy-policy disclosure for user-triggered AI receipt analysis.
