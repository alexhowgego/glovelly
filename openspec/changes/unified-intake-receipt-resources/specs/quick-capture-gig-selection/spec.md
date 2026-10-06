## MODIFIED Requirements

### Requirement: Initial quick-capture selection behavior remains unchanged
The system SHALL retain configured nearby range, local application clock, candidate count, cutoff ties, and existing nearest-gig ordering as the initial selection basis for unified intake. Eligible automatic receipt application SHALL use the first initial candidate; other candidates and existing informational ambiguity warnings SHALL NOT block application or trigger a separate journey. Explicit expansion SHALL NOT change the initial selection or warning outcome.

#### Scenario: Multiple initial candidates exist
- **WHEN** multiple nearby visible non-cancelled gigs exist and receipt confidence permits automatic application
- **THEN** the system SHALL select the nearest ranked gig and save within the open unified upload dialog

#### Scenario: Loading candidates does not mutate an item
- **WHEN** a user requests another candidate page
- **THEN** the system SHALL NOT upload, create, move, save, or otherwise modify a receipt or resource

### Requirement: Quick-capture dialogs load additional gigs safely
Unified upload and saved receipt/resource correction SHALL present Load more gigs whenever eligible candidate results remain. They SHALL append candidates without duplicates, preserve selection when possible, and retain pending fields during loading and failure.

#### Scenario: User loads another page
- **WHEN** a user activates Load more gigs while another page exists
- **THEN** existing candidates SHALL remain visible, the action SHALL be disabled while pending, and new candidates SHALL be appended on success

#### Scenario: Candidate expansion fails
- **WHEN** another candidate page fails to load
- **THEN** the dialog SHALL retain source, fields, selection, and options and show a recoverable error with retry

#### Scenario: No candidates remain
- **WHEN** the response reports no further eligible gigs
- **THEN** the dialog SHALL hide or clearly disable Load more gigs

#### Scenario: Initial nearby candidates are empty
- **WHEN** no initial nearby candidate exists but eligible gigs remain
- **THEN** upload SHALL offer Load more gigs without resetting the current intake
