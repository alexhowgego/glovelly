## Why

Gemini-backed set list chart matching currently runs as a single long-lived HTTP request, which can take over a minute and is unreliable on iPhone/iPad even when the backend completes successfully. The workflow needs to preserve whole-set contextual ranking quality while avoiding mobile browser fetch timeouts and giving users reliable progress/completion feedback.

## What Changes

- Start AI chart matching as a short HTTP request that returns a server-side job id instead of waiting for Gemini to complete.
- Run the existing whole-set contextual ranking operation asynchronously so Gemini still receives the full set list context in one ranking pass.
- Store AI matching job status and results server-side so clients can fetch results after SignalR notification or polling fallback.
- Publish user-scoped SignalR events when AI matching jobs start, complete, or fail.
- Update the set list modal to display long-running AI progress, apply completed results, and recover via polling if SignalR is unavailable or mobile browsers suspend the connection.
- Return safe diagnostics and correlation ids for failed jobs without logging prompts, row titles, auth data, or provider secrets.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `contextual-chart-ranking`: AI-backed ranking must support an asynchronous, mobile-safe job lifecycle while preserving whole-set context.
- `setlist-chart-matching`: set list chart matching review must surface asynchronous AI job progress/results and keep deterministic candidates available for manual review.

## Impact

- Backend API: new endpoints for starting and checking AI chart matching jobs; existing synchronous preview endpoint remains for deterministic matching.
- Backend data: new persisted job entity/table and manual PostgreSQL SQL script for job state/result storage.
- Backend services: background job processing service/queue, SignalR job notifications, job scoping/cleanup, and safer cancellation/error logging.
- Frontend: set list modal starts jobs, listens for SignalR job events, polls as fallback, and applies completed AI results.
- Operations: Cloud Run stays on the same app process; job state survives transient client disconnects and gives staging diagnostics for mobile failures.
