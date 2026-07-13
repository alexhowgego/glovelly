# Contextual Chart Ranking

## Purpose

Candidate retrieval and evidence-based ranking for matching set list song rows to forScore charts. Uses multiple evidence types (chart numbers, title similarity, catalogue context) and supports an optional/replaceable LLM provider for contextual ranking.

## Requirements

### Requirement: Chart candidate retrieval is high-recall and evidence-based
The system SHALL retrieve plausible forScore chart candidates for each set list song row using multiple evidence types before contextual ranking.

#### Scenario: Chart number produces candidates
- **WHEN** a set list song row includes a chart number such as `17`, `61-E`, or `104`
- **THEN** candidate retrieval includes forScore charts whose metadata, file path, title, keywords, or print number contain the same chart number

#### Scenario: Nearby chart numbers are included as weak candidates
- **WHEN** a set list song row includes a chart number and exact-number retrieval does not fully resolve the row
- **THEN** candidate retrieval includes nearby chart numbers using `number - 1` and `number + 1` evidence marked as weak

#### Scenario: Title variants produce candidates
- **WHEN** a set list song row title differs from a chart title only by punctuation, ampersand/and wording, compact form, token order, or descriptor text
- **THEN** candidate retrieval includes that chart and records the title-similarity evidence used

#### Scenario: Candidate retrieval does not require final certainty
- **WHEN** a chart is plausibly related but not safe to auto-select
- **THEN** candidate retrieval can still include it with weak or partial evidence for contextual ranking or human review

### Requirement: Whole-set context informs chart ranking
The system SHALL rank candidate charts using evidence from the entire set list, not only the individual row title.

#### Scenario: Duplicate title candidates are disambiguated by catalogue context
- **WHEN** multiple forScore charts have the same or similar title and other rows in the set list strongly indicate one band/client catalogue
- **THEN** ranking prefers the candidate consistent with the dominant catalogue context and explains that reason

#### Scenario: Chart number outranks title-only match
- **WHEN** one candidate matches a row's chart number and another candidate only matches the row title
- **THEN** ranking prefers the chart-number candidate unless other evidence creates a conflict requiring review

#### Scenario: Conflicting evidence requires review
- **WHEN** chart number, title, key, and catalogue context point to different candidates
- **THEN** ranking marks the item as needing review instead of silently selecting a chart

### Requirement: Contextual ranking is bounded to supplied candidates
The system SHALL ensure contextual ranking selects only from candidate chart IDs supplied by deterministic retrieval.

#### Scenario: Ranker cannot invent a chart
- **WHEN** a contextual ranker cannot find a suitable supplied candidate
- **THEN** it returns no match or needs-review status rather than a chart name or file path not present in the candidate list

#### Scenario: Ranker returns structured decisions
- **WHEN** contextual ranking completes
- **THEN** each row result includes status, confidence, selected candidate id when applicable, and a user-facing reason

### Requirement: LLM assistance is optional and replaceable
The system SHALL support a contextual ranker abstraction that can use deterministic fallback behavior or a configured GCP-hosted LLM provider.

#### Scenario: No LLM configuration uses deterministic fallback
- **WHEN** no LLM provider is configured
- **THEN** chart matching still returns deterministic ranked candidates and review states without failing the set list workflow

#### Scenario: LLM provider receives bounded prompt data
- **WHEN** an LLM provider is configured for contextual ranking
- **THEN** it receives only the set list rows, candidate chart IDs, candidate metadata, evidence, and ranking instructions needed to choose among supplied candidates

#### Scenario: Invalid LLM response falls back safely
- **WHEN** the LLM response is malformed, references unknown chart IDs, or omits required row decisions
- **THEN** the system ignores the invalid response and returns deterministic or needs-review results without saving invented mappings

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
