## Context

Set list chart matching currently has two useful phases: deterministic candidate retrieval and optional Gemini-backed contextual ranking. Deterministic candidate location is fast enough to run directly from the set list import modal, but Gemini-backed ranking can take longer than a minute for realistic set lists. Mobile browsers, especially iOS WebKit-based browsers, can abort long-running fetches with a generic `Load failed` even when Cloud Run and Vertex AI complete successfully.

The existing SignalR infrastructure already provides authenticated, user-scoped notifications through `/workspace-events`. It currently broadcasts coarse workspace changes only. The AI matching workflow needs a job lifecycle that can outlive the initiating HTTP request, preserve whole-set context for ranking quality, and still recover if SignalR is disconnected or suspended by the mobile browser.

## Goals / Non-Goals

**Goals:**

- Preserve the current whole-set Gemini ranking quality by continuing to rank the full supplied set list in one contextual pass.
- Replace the long-lived AI matching HTTP request with a short job-start request plus SignalR/polling completion flow.
- Persist enough job state that clients can recover results after refresh, SignalR reconnect, or mobile browser fetch failure.
- Keep deterministic candidate matching synchronous and immediately usable as the manual-review fallback.
- Provide safe diagnostics through request/job ids, timings, status, and sanitized failure messages.
- Keep user and workspace scoping equivalent to existing set list chart matching visibility checks.

**Non-Goals:**

- Changing the Gemini model or prompt strategy beyond what is needed to move execution into an async job.
- Splitting ranking into per-row or small batches that would reduce whole-set context.
- Introducing an external queue service for this slice.
- Saving AI-selected chart mappings automatically without user review/save confirmation.
- Logging prompts, row titles, auth tokens, Google credentials, or raw provider responses.

## Decisions

### Persist AI Matching Jobs In The Application Database

Create a `SetListChartMatchJob` model/table with job id, owner user id, gig id, status, input JSON, result JSON, safe error message, correlation id, and timestamps.

Rationale: SignalR is only a notification channel; mobile devices can sleep, disconnect, or refresh. Persisted job state lets the modal poll or refetch results by job id. It also gives staging diagnostics without depending on browser console logs.

Alternatives considered:

- In-memory job dictionary: simpler but loses jobs on instance restart/scale down and cannot support reliable polling.
- Fire-and-forget task with no job row: shortest implementation but still opaque and fragile on Cloud Run.
- Cloud Tasks/Pub/Sub: more durable, but adds new GCP infrastructure and operational surface beyond this change.

### Use A Short Start Endpoint And A Status Endpoint

Add `POST /gigs/{gigId}/setlist-imports/chart-matches/ai-jobs` to validate visibility, persist a pending job, enqueue processing, and return `202 Accepted` with `{ jobId, status }` quickly. Add `GET /gigs/{gigId}/setlist-imports/chart-matches/ai-jobs/{jobId}` to return status, result, or safe error details.

Rationale: iOS failures happen because the initiating request waits for the full Gemini call. Returning quickly removes the fragile long fetch while keeping the final result available by polling.

Alternatives considered:

- Reusing the existing preview endpoint with a `Prefer: respond-async` header: less explicit and harder for the frontend to reason about.
- SignalR hub method to start work: would couple command handling to the hub and make polling fallback less natural.

### Reuse SignalR With Job-Specific Workspace Events

Extend `WorkspaceEvent` with optional string metadata and publish events such as `scope: "setlist-chart-matching"`, `action: "started" | "completed" | "failed"`, `entityId: jobId`, and metadata containing `gigId` and `status`.

Rationale: The app already has authenticated user-group SignalR delivery. Keeping the same hub avoids another connection and lets the modal subscribe through the existing hook.

Alternatives considered:

- Dedicated hub event type: cleaner payload typing for this feature but requires parallel client plumbing.
- Polling only: simpler and reliable, but slower feedback and does not leverage already-built real-time infrastructure.

### Process Jobs With A Scoped Background Processor

Introduce a small in-process queue and hosted service that processes pending AI matching jobs by creating a scope, loading the persisted input, running `SetListChartMatcher.MatchAsync(..., useConfiguredRanker: true)`, storing results, and publishing SignalR completion/failure events.

Rationale: This keeps implementation local to the existing API process and allows job processing to continue after the start request returns. Persisted state protects against most client-side failures. Active SignalR/polling traffic keeps Cloud Run warm during the interactive flow.

Alternatives considered:

- Scheduled Cloud Run Job drainer: aligns with existing worker patterns, but is too slow/coarse for an interactive user waiting in the modal unless paired with additional trigger infrastructure.
- Running the work inline in the start endpoint: preserves current mobile failure mode.

### Keep Polling As The Source-Of-Truth Fallback

The frontend should listen for SignalR job events but also poll the status endpoint while a job is pending/running. SignalR only tells the client to fetch; the status endpoint returns the authoritative job state/result.

Rationale: Mobile browsers can suspend WebSocket/SignalR connections just as they can abort long fetches. Polling makes the workflow recoverable on reconnect, refresh, or temporary network loss.

Alternatives considered:

- Push full result payload over SignalR: avoids a follow-up fetch but makes large payload delivery depend on the fragile real-time connection and duplicates authorization/result handling.

### Keep Deterministic Matching Synchronous

`Import rows` continues to parse the Google Sheet and locate deterministic candidates immediately. The existing synchronous chart-match preview endpoint remains available for deterministic candidate retrieval and tests.

Rationale: Deterministic matching is fast, gives the user a useful manual-review fallback, and avoids making the basic import flow dependent on a background job.

## Risks / Trade-offs

- In-process background jobs can be interrupted by Cloud Run instance shutdown → persist job state, mark stale `Running` jobs as retryable/failed on startup or next enqueue, and keep polling fallback visible to users.
- SignalR delivery is best-effort on mobile → use SignalR only as a notifier and poll status until terminal state.
- Job input/result JSON can grow → store minimal matching input and match results only; do not store prompts or raw provider responses.
- Duplicate clicks could create duplicate jobs → disable the AI button while a job is active and optionally reject/start no-op when a running job exists for the same gig/user/request fingerprint.
- Background processing can fail after the modal closes → persist safe error state and allow status endpoint to expose the failure by job id.
- Manual SQL scripts can drift from EF model configuration → add focused tests for model persistence and include an idempotent manual PostgreSQL script.

## Migration Plan

1. Add the EF model/configuration and manual SQL script for the job table.
2. Deploy backend support while keeping the existing synchronous AI preview path temporarily available.
3. Update the frontend to use the async job path for `Ask AI to choose`.
4. Confirm staging logs show short start requests and terminal job events/status responses.
5. Remove or de-emphasize synchronous AI usage after the async flow is stable, while retaining deterministic preview.

Rollback: keep deterministic candidate location and manual review intact. If async AI jobs fail in staging, hide or disable the AI action by configuration and continue using deterministic candidates.

## Open Questions

- Should stale running jobs be retried automatically once, or marked failed with a user-visible retry action?
- What retention window should completed/failed AI matching jobs use before cleanup?
- Should same-user duplicate AI jobs for the same gig be deduplicated by input fingerprint, or is UI-level disablement sufficient for the first implementation?
