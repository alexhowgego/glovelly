## Purpose

Define how quick-capture dialogs select and progressively load eligible gigs.

## Requirements

### Requirement: Quick-capture candidate expansion is eligible and paged
The system SHALL provide an authenticated, read-only quick-capture candidate query that progressively returns gigs visible to the current user and excludes cancelled gigs. Each response SHALL contain candidates in a stable order of absolute distance from today, gig date, title, and gig ID, and SHALL indicate whether another page is available using continuation metadata.

#### Scenario: Initial nearby candidates can be expanded
- **WHEN** a quick-capture dialog has displayed its initial nearby candidates and eligible gigs remain outside that result
- **THEN** the system SHALL provide a continuation that returns the next eligible gigs without repeating the initial candidates

#### Scenario: Historical gigs remain eligible
- **WHEN** a visible, non-cancelled gig is far in the past and the user requests enough candidate pages
- **THEN** the system SHALL return that gig in the ordered candidate results

#### Scenario: Inaccessible and cancelled gigs are excluded
- **WHEN** the candidate query is requested by a user
- **THEN** the system SHALL not return gigs outside that user's visibility or gigs with cancelled status

#### Scenario: Candidate pages are exhausted
- **WHEN** a candidate response contains the final eligible results
- **THEN** the system SHALL indicate that no further page is available

### Requirement: Initial quick-capture selection behavior remains unchanged
The system SHALL retain the existing nearby-candidate range, candidate-count and cutoff-tie handling, automatic-selection behavior, and ambiguity warning for the initial quick receipt and attachment capture attempt. Explicit candidate expansion SHALL NOT change the initial selection or warning outcome.

#### Scenario: Initial ambiguity is preserved
- **WHEN** a quick-capture attempt has multiple nearby candidates that trigger the existing ambiguity behavior
- **THEN** the system SHALL retain that behavior before the user requests additional candidates

#### Scenario: Loading candidates does not mutate a draft
- **WHEN** a user requests another candidate page
- **THEN** the system SHALL NOT upload, create, move, save, or otherwise modify the receipt or attachment draft

### Requirement: Quick-capture dialogs load additional gigs safely
The quick receipt and quick attachment dialogs, including attachment link capture, SHALL present a Load more gigs action whenever eligible candidate results remain. The dialogs SHALL append loaded candidates without duplicates, preserve the selected gig when possible, and retain all pending capture fields during candidate loading or failure.

#### Scenario: User loads another page
- **WHEN** a user activates Load more gigs while another candidate page exists
- **THEN** the dialog SHALL keep existing candidates visible, disable the action while the request is pending, and append the new candidates when it succeeds

#### Scenario: Candidate expansion fails
- **WHEN** loading another candidate page fails
- **THEN** the dialog SHALL show a recoverable error, retain the pending capture data and existing candidate options, and allow another load attempt

#### Scenario: No candidates remain
- **WHEN** the current candidate response indicates no further eligible gigs
- **THEN** the dialog SHALL hide or clearly disable Load more gigs

#### Scenario: Initial nearby candidates are empty
- **WHEN** a quick-capture dialog has no initial nearby candidate but eligible gigs remain
- **THEN** the dialog SHALL still present Load more gigs without resetting the pending capture
