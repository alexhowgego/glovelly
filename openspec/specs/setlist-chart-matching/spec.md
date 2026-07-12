# Set List Chart Matching Specification

## Purpose

TBD - captures requirements for matching Glovelly set list song items to charts in the authenticated user's forScore library.

## Requirements

### Requirement: Set list song items can be matched to forScore charts
The system SHALL match set list song items against charts in the authenticated user's active forScore library snapshot.

#### Scenario: Song row receives chart candidates
- **WHEN** an authenticated user previews or reviews a set list containing a song item and has an active forScore library snapshot
- **THEN** the system returns the best matching forScore chart candidates for that item with match status and confidence

#### Scenario: Non-song row is not matched
- **WHEN** a set list item is a separator or comment
- **THEN** the system marks the item as not applicable for chart matching and does not return chart candidates

#### Scenario: User without active library can still review set list
- **WHEN** an authenticated user previews or reviews a set list without an active forScore library snapshot
- **THEN** the system returns the set list rows without chart candidates and indicates that no active library is available for matching

### Requirement: New set list imports include chart match review
The system SHALL include forScore chart match status in the Google Sheet set list import review before the import is saved.

#### Scenario: Preview shows automatic match status
- **WHEN** a user previews a Google Sheet set list and the active forScore library contains matching charts
- **THEN** each song row shows whether it is matched, ambiguous, unmatched, or needs review before save

#### Scenario: User confirms chart before saving import
- **WHEN** a user selects a forScore chart candidate for a song row during import review and saves the import
- **THEN** the saved set list item stores the selected chart mapping and copied chart title and file path

#### Scenario: User leaves item unmapped
- **WHEN** a user clears or declines chart selection for a song row during import review and saves the import
- **THEN** the saved set list item remains included according to the set list review choice but has no confirmed forScore chart mapping

### Requirement: Existing set lists can be matched retroactively
The system SHALL allow users to generate and save forScore chart mappings for an existing active gig set list without replacing the set list import.

#### Scenario: Existing active set list is matched
- **WHEN** a user opens an existing active gig set list and requests forScore matching
- **THEN** the system compares the saved song items with the active forScore library and returns match suggestions without re-reading the Google Sheet source

#### Scenario: Retroactive mapping preserves set list rows
- **WHEN** a user saves chart mappings for an existing active set list
- **THEN** the system updates only chart mapping fields and keeps the set list row order, inclusion flags, titles, notes, and source metadata intact unless the user explicitly edited those fields

### Requirement: Confirmed mappings preserve chart identity
The system SHALL persist enough chart identity on each mapped set list item to explain the mapping after the source library snapshot is replaced.

#### Scenario: Mapping stores snapshot-local chart reference
- **WHEN** a user confirms a chart mapping for a set list item
- **THEN** the system stores the selected chart id, the chart's library snapshot id, and copied chart title and file path on the set list item

#### Scenario: Mapping from older snapshot remains explainable
- **WHEN** a set list item is linked to a chart from a library snapshot that is no longer active
- **THEN** the system can still show the copied chart title and file path for the prior mapping

### Requirement: Users can fix mappings that need review
The system SHALL guide users to resolve ambiguous, missing, or older-library chart mappings through manual chart selection or clearing the mapping.

#### Scenario: Ambiguous match requires choice
- **WHEN** multiple plausible forScore charts match a set list song item
- **THEN** the system marks the item as needing review and lets the user choose one candidate or search/select another chart

#### Scenario: Missing chart can remain unmapped
- **WHEN** no chart in the active forScore library matches a set list song item
- **THEN** the system marks the item as missing from the latest library and lets the user leave it unmapped

#### Scenario: User changes selected chart
- **WHEN** a user selects a different forScore chart for an already mapped item
- **THEN** the system replaces the stored chart mapping and copied chart identity with the newly selected chart

### Requirement: Chart mappings are user-scoped
The system SHALL only match and persist set list chart mappings using forScore library snapshots visible to the authenticated user.

#### Scenario: User cannot map to another user's chart
- **WHEN** an authenticated user attempts to save a set list item mapping to a forScore chart owned by a different user
- **THEN** the system rejects the mapping without exposing the other user's chart data

#### Scenario: User only receives own candidates
- **WHEN** an authenticated user requests chart match candidates for a set list item
- **THEN** the system returns candidates only from that user's active forScore library snapshot

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
