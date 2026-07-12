## ADDED Requirements

### Requirement: Set list review supports asynchronous AI chart matching
The system SHALL let users ask AI to choose chart matches without relying on a long-running browser HTTP request.

#### Scenario: User starts AI matching from reviewed candidates
- **WHEN** a user has imported set list rows with deterministic chart candidates and selects "Ask AI to choose"
- **THEN** the UI starts an asynchronous AI matching job and shows progress without blocking on the AI provider response request

#### Scenario: User starts AI matching before candidates exist
- **WHEN** a user selects "Ask AI to choose" before deterministic candidates have been located
- **THEN** the UI first locates deterministic candidates, then starts the asynchronous AI matching job in the same user flow

#### Scenario: AI matching completion applies results
- **WHEN** an asynchronous AI matching job completes successfully for the active set list review
- **THEN** the UI fetches the completed result, applies selected chart ids and candidate details to the corresponding rows, and preserves manual review/save behavior

### Requirement: Set list AI matching remains usable when real-time delivery is unavailable
The system SHALL use SignalR as a progress/completion notifier and polling as the authoritative fallback for asynchronous AI matching jobs.

#### Scenario: SignalR completion prompts result fetch
- **WHEN** the UI receives a SignalR completion event for the active AI matching job
- **THEN** it fetches the job status/result endpoint and applies the returned results rather than trusting the SignalR payload as the result source

#### Scenario: Polling recovers missing SignalR event
- **WHEN** SignalR is disconnected, suspended, or misses a job completion event
- **THEN** the UI continues polling the job status endpoint until the job reaches completed or failed state

#### Scenario: Mobile browser abort does not lose result
- **WHEN** a mobile browser aborts, refreshes, or backgrounds the page after starting an AI matching job
- **THEN** the user can recover the job status/result through the persisted job endpoint when the modal or page becomes active again

### Requirement: Deterministic candidates remain the manual fallback
The system SHALL keep deterministic candidate results available while asynchronous AI matching is pending or failed.

#### Scenario: AI matching is pending
- **WHEN** an AI matching job is running
- **THEN** the set list review continues to show deterministic candidate selections and rows needing attention

#### Scenario: AI matching fails
- **WHEN** an AI matching job fails or cannot be recovered
- **THEN** the user can continue reviewing and saving deterministic/manual chart selections without losing imported rows or candidate dropdowns
