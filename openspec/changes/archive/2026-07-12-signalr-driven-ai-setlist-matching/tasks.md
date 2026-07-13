## 1. Job Persistence Model

- [x] 1.1 Add `SetListChartMatchJob` model and status enum for pending, running, completed, failed, and cancelled states.
- [x] 1.2 Add `DbSet`, EF configuration, indexes, ownership relationships, JSON column configuration, and visibility-safe constraints for job rows.
- [x] 1.3 Add an idempotent manual PostgreSQL SQL script for the job table and indexes.
- [x] 1.4 Add model/persistence tests proving job rows store minimal input JSON, result JSON, status transitions, owner id, gig id, correlation id, and timestamps.

## 2. Backend Job API

- [x] 2.1 Add minimal request/response DTOs for starting AI chart matching jobs and reading job status/result.
- [x] 2.2 Add `POST /gigs/{gigId}/setlist-imports/chart-matches/ai-jobs` that validates gig visibility, validates request rows, stores a pending job, enqueues processing, and returns quickly with `202 Accepted`.
- [x] 2.3 Add `GET /gigs/{gigId}/setlist-imports/chart-matches/ai-jobs/{jobId}` that returns only same-user job state, completed results, or safe failure details.
- [x] 2.4 Add endpoint tests for job creation, fast accepted response, completed status shape, cross-user rejection, missing gig/job handling, and safe failed-job diagnostics.

## 3. Background Processing And Ranking

- [x] 3.1 Add an in-process AI chart matching work queue and hosted service that processes persisted pending jobs using scoped services.
- [x] 3.2 Implement job claiming and status transitions so jobs move pending -> running -> completed/failed with started/completed timestamps and safe error messages.
- [x] 3.3 Run `SetListChartMatcher.MatchAsync(..., useConfiguredRanker: true)` from the job processor using the full stored set list input in one whole-set ranking request.
- [x] 3.4 Persist completed `SetListChartMatchResult` JSON without saving chart mappings automatically to set list items.
- [x] 3.5 Handle cancellation, stale running jobs, provider exceptions, and invalid stored input with safe failure states and structured logs.
- [x] 3.6 Add processor tests proving whole-set input is passed to the ranker, deterministic fallback results can complete the job, failures are persisted safely, and stale running jobs are recoverable or failed.

## 4. SignalR Notifications And Polling Contract

- [x] 4.1 Extend `WorkspaceEvent` and frontend `WorkspaceEvent` typing with optional metadata for job-specific values.
- [x] 4.2 Publish user-scoped `setlist-chart-matching` workspace events when jobs start, complete, or fail, including job id, gig id, and status metadata.
- [x] 4.3 Ensure SignalR notifications never include prompts, row titles, provider responses, auth data, or full match result payloads.
- [x] 4.4 Add tests or focused service coverage proving job events are published to the owning user and not to unrelated users.

## 5. Frontend Set List Modal Integration

- [x] 5.1 Update `SetListImportModal` so `Ask AI to choose` starts an AI matching job instead of calling the long-running preview endpoint.
- [x] 5.2 Track active job id/status in modal state and show progress while preserving deterministic candidate dropdowns and manual review affordances.
- [x] 5.3 Listen for `setlist-chart-matching` workspace events and fetch the authoritative job status/result when the active job changes.
- [x] 5.4 Add polling fallback while a job is pending/running, including recovery on reconnect, focus, visibility change, or missed SignalR events.
- [x] 5.5 Apply completed job results to current rows by source row number without overwriting unrelated user edits made after the job started.
- [x] 5.6 Display failed-job messages with safe details and correlation id while keeping deterministic/manual candidate review usable.

## 6. Verification And Documentation

- [x] 6.1 Update frontend types and any UAT documentation for the asynchronous AI matching flow.
- [x] 6.2 Run `dotnet test glovelly.sln -m:1 --filter FullyQualifiedName~SetListImportEndpointsTests` and any new job processor tests.
- [x] 6.3 Run `npm --prefix frontend/glovelly-web run lint` and `npm --prefix frontend/glovelly-web run build`.
- [x] 6.4 Manually verify in staging that mobile `Ask AI to choose` returns a job quickly, receives or polls completion, and applies whole-set AI results without `Load failed`.
