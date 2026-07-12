## ADDED Requirements

### Requirement: AI ranking runs as an asynchronous whole-set job
The system SHALL support running configured AI contextual chart ranking as an asynchronous job that preserves whole-set set list context.

#### Scenario: Starting AI ranking returns quickly
- **WHEN** an authenticated user starts AI chart ranking for a set list with included song rows and candidate charts
- **THEN** the system creates a user-scoped ranking job and returns a job id without waiting for the configured AI provider to complete

#### Scenario: AI ranking preserves whole-set context
- **WHEN** the asynchronous ranking job invokes the configured AI ranker
- **THEN** the ranker receives the full supplied set list context and candidate sets in one ranking request rather than isolated row batches

#### Scenario: Deterministic fallback remains available
- **WHEN** no AI provider is configured or the AI provider fails during asynchronous job processing
- **THEN** the job completes with deterministic fallback or needs-review results using only supplied candidate chart ids

### Requirement: AI ranking job state is recoverable
The system SHALL persist AI ranking job input, status, result, safe failure details, and timestamps so clients can recover after disconnects or refreshes.

#### Scenario: Job status can be checked after client disconnect
- **WHEN** a client starts an AI ranking job and then disconnects before completion
- **THEN** the client can later request the job status and receive pending, running, completed, or failed state scoped to the same authenticated user

#### Scenario: Completed job exposes structured ranking results
- **WHEN** an asynchronous AI ranking job completes successfully
- **THEN** the status response includes structured set list chart match results with status, confidence, selected candidate id when applicable, candidates, and user-facing reasons

#### Scenario: Failed job exposes safe diagnostics
- **WHEN** an asynchronous AI ranking job fails
- **THEN** the status response includes a safe error message and correlation id without exposing prompts, row titles, provider responses, credentials, or auth data

### Requirement: AI ranking job notifications are user-scoped
The system SHALL notify only the owning authenticated user when asynchronous AI ranking jobs change state.

#### Scenario: Completion notification is sent to owner
- **WHEN** an AI ranking job completes or fails
- **THEN** the system publishes a SignalR workspace event to the owning user's group containing the job id, gig id, and terminal status

#### Scenario: Other users cannot observe job state
- **WHEN** another authenticated user is connected to SignalR or requests a job status endpoint
- **THEN** the system does not expose the job event, status, input, result, or safe error details
