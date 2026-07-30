## Context

Glovelly quick receipt capture stores an attachment and an editable `GigExpense` draft, while the standard gig expenses panel lets users attach receipts to any existing expense. Neither path extracts receipt details. The application already calls Vertex AI Gemini through `PredictionServiceClient.GenerateContentAsync` for contextual set-list chart matching, using Application Default Credentials, a regional endpoint, and defensive JSON handling.

Receipt analysis handles sensitive business content and must never block attachment storage or overwrite accounting data. `GigExpense` has no currency or expense-category fields, so those suggestions have no authoritative destination in the current accounting model.

## Goals / Non-Goals

**Goals:**
- Make any persisted expense attachment an explicit, user-selected target for receipt analysis.
- Use Vertex AI multimodal generation to extract a narrow, validated suggestion set.
- Retain source receipt, analysis provenance, suggestions, warnings, and failures independently from accepted expense values.
- Give quick capture and the normal expense panel one shared analysis/review journey.
- Preserve a fully functional manual receipt workflow under every analysis failure condition.

**Non-Goals:**
- Automatic expense updates, automatic analysis after upload, or durable background queueing/retry.
- New canonical expense categories, expense currencies, tax/VAT fields, payment-method fields, line items, duplicate detection, or vendor learning.
- A cross-provider AI abstraction or a general AI-job framework.
- Replacing receipt storage, changing the quick-capture gig-matching rules, or resolving the existing unassigned-draft limitation.

## Decisions

### Attachment-level, on-demand analysis is the primary orchestration path

Expose analysis under the existing attachment resource, scoped by `{gigId, expenseId, attachmentId}` and protected by the same owner-visibility lookup as download/delete. The standard expense attachment row is the canonical entry point; after quick capture saves an attachment, its modal invokes that same target and review experience.

The analysis API provides a read of the latest attempt and an explicit request to create a new one. The POST completes synchronously within a configured short timeout and persists a success or safe failure result. A later retry is a new attempt, preserving provenance.

This avoids a second quick-capture-only workflow and avoids prematurely copying the set-list-specific background job infrastructure. A durable worker remains a future option if measured latency or adoption makes post-upload analysis worthwhile.

### A separate attempt entity preserves provenance and financial authority

Introduce `ReceiptAnalysis` with a foreign key to `ExpenseAttachment`; an attachment has many attempts. The record stores status, provider, model, prompt version, request/completion times, validated merchant/date/total/currency/category suggestions, per-field confidence, warnings, bounded raw structured output if retained, and a safe error message.

`GigExpense` is not extended and is never mutated by analysis. Merchant and total are copied only into client-side editable description/amount controls after an explicit user action, then persisted through the existing expense-save path. Date, currency, and category remain view-only analysis output.

This is preferred to nullable AI columns on `GigExpense` because it supports reanalysis after model/prompt changes, preserves failed attempts, and makes clear that model output is not an accepted business record.

### Send supported media inline for the proof

The analysis service opens the already-authorized attachment through `IExpenseAttachmentStore`, then supplies its bytes and MIME type as a Vertex multimodal part alongside a constrained text prompt. Start with JPEG, PNG, WebP, and PDF, plus a distinct configurable analysis size limit below the generic attachment limit. Receipt analysis uses Gemini 3.5 Flash for multimodal media while set-list matching remains on Gemini 3.1 Flash Lite.

Inline content is preferred over a `gs://` reference because it reuses the current authorization/storage seam and introduces no Vertex service-agent bucket access, cross-project, or location dependencies. It also matches the current GCS storage implementation, which downloads attachment content into memory. A storage URI can be reconsidered after the proof if large-document cost or memory pressure justifies its operational complexity.

### Use constrained output plus strict local validation

The prompt requests JSON only, ideally with the Vertex response MIME type and response schema controls supported by the chosen SDK/model. The service still treats provider output as untrusted.

The response is a fixed object with nullable merchant, transaction date, total amount, currency, suggested category, per-field confidence, and warnings. Validation accepts only ISO dates, invariant decimal amounts represented as strings, recognised uppercase ISO currency codes, a small supplied category enum, bounded strings/lists, and known confidence values. Invalid individual fields become warnings while valid independent fields remain reviewable; invalid whole responses produce a failed attempt with no suggestions.

The contextual prompt contains only the receipt, upload date, optional default currency, and the allowed category values. It excludes user identity, client, gig title, and unrelated workspace data.

### One shared review UI adapts application to its caller

A receipt-analysis hook/component owns loading the latest attempt, starting analysis, rendering status, and presenting suggestions. It receives an attachment target and an `onApply` callback. The normal expense panel callback opens/prefills the existing expense editor; the quick-receipt callback fills its amount and description controls. The review UI labels every value as a suggestion, exposes confidence and warnings, and offers reanalysis without silently saving anything.

This is preferred to embedding analysis state in the broad `Gig` response or creating a special quick-capture form. A narrow attachment-analysis GET retrieves persistent results on demand, and POST returns the newly created attempt.

### Privacy, observability, and availability are bounded by default

Analysis is user-triggered. Request logs contain only IDs, configured model, elapsed time, outcome, failure class, and a coarse byte-size measure; they never contain receipt bytes, filename, prompt content, raw model text, merchant, or amount. Provider request/response logging remains disabled unless separately designed with retention and access controls.

The endpoint applies a per-user rate limit and a short cancellation-aware timeout. Unsupported media, absent Vertex configuration, quota/provider failures, empty/malformed/safety-blocked responses, or missing blob content are persisted as safe analysis failures and leave the receipt and expense unchanged. The privacy policy will explicitly describe this optional Vertex AI processing before the feature is enabled in production.

## Risks / Trade-offs

- [Gemini extraction is incorrect or ambiguous] → Persist confidence/warnings, use conservative validation, require explicit user application, and assess quality against consented representative receipts.
- [A receipt contains sensitive data] → Send minimal context only, provide clear user-triggered disclosure, avoid sensitive logs, and document provider processing.
- [Inline content increases request memory and latency] → Enforce a lower analysis size limit and supported MIME allowlist; measure latency before considering GCS URI input.
- [Model output changes over time] → Record provider, model, prompt version, timestamps, and each attempt independently.
- [A provider outage looks like an empty extraction] → Persist a distinct safe failure status/message and instrument failure categories rather than returning an empty suggestion silently.
- [Existing expense editing has no category/currency fields] → Present them as review-only analysis information and defer accounting-model expansion.
- [Analysis retry costs can grow] → Make retries explicit, rate limit per user, and capture model usage/latency metrics for the spike cost model.

## Migration Plan

1. Deploy the `ReceiptAnalysis` schema and indexes before exposing UI actions; existing attachments require no backfill.
2. Deploy service/API/UI with receipt analysis disabled unless Vertex receipt settings are configured; manual attachment and quick-capture flows continue unchanged.
3. Enable for a controlled set of users with Vertex IAM, the configured model, rate limits, and privacy-policy update in place.
4. Evaluate consented sample and live opt-in outcomes for extraction accuracy, latency, failure rate, and cost before considering asynchronous analysis.
5. Roll back by disabling receipt-analysis configuration or hiding its UI action. Existing analysis records remain isolated from financial records and can be retained/deleted under the agreed policy without data rollback.

## Open Questions

- Which exact Gemini model/version and regional availability will be approved for receipt media, and what are its current supported MIME limits?
- What analysis file-size limit and per-user rate limit meet the desired mobile experience and budget after the representative-receipt evaluation?
- Does the initial retention policy require bounded raw structured responses, or are validated fields, warnings, and provenance sufficient?
- What in-product disclosure wording and privacy-policy retention detail are required before production enablement?
