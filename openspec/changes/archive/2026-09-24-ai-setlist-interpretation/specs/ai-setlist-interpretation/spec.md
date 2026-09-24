## ADDED Requirements

### Requirement: Set-list interpretation uses a bounded source grid
The system SHALL acquire the selected linked Google Sheet worksheet as a bounded, coordinate-preserving grid containing source order, interior empty cells, and formatted display values before automatic set-list extraction.

#### Scenario: Sparse layout retains cell positions
- **WHEN** a selected worksheet has song headings and component songs in different columns with empty cells between them
- **THEN** the interpretation input preserves each displayed cell's coordinate and the empty positions within the bounded used region

#### Scenario: Formatted values are supplied
- **WHEN** a worksheet contains a duration, date, or other formatted numeric cell
- **THEN** the interpretation input uses its displayed formatted value rather than its underlying serial value

#### Scenario: Worksheet exceeds interpretation bounds
- **WHEN** the worksheet exceeds configured row, column, or source-payload limits
- **THEN** the system rejects automatic interpretation with an actionable response and does not submit an unbounded prompt

### Requirement: AI-only interpretation creates a reviewable logical draft
The system SHALL use a configured AI interpreter as the only automatic extraction path for a selected worksheet and SHALL produce an ordered draft of supported logical set-list items rather than impose a predetermined worksheet layout.

#### Scenario: Irregular set list is interpreted
- **WHEN** a worksheet uses a sparse or non-tabular layout for ordinary songs, medleys, personnel, and rehearsal notes
- **THEN** the interpreted draft identifies supported songs, section markers, transitions, and comments in source order without requiring fixed title, pad, or key columns

#### Scenario: Metadata is not promoted to a song
- **WHEN** an occupied cell contains only a vocalist, instrument assignment, duration, rehearsal note, pad number, or musical key
- **THEN** the interpreter does not return that cell value as a Song title

#### Scenario: Uncertain content is omitted
- **WHEN** the interpreter cannot support a logical item from the source grid
- **THEN** it omits that item rather than inventing a title, number, key, or notes value

#### Scenario: Excluded section is represented separately
- **WHEN** the source worksheet establishes a section as spare or otherwise not to be played
- **THEN** the draft represents that section and its supported items as excluded by default

### Requirement: Interpretation output is schema-constrained and validated
The system SHALL treat AI interpretation output as untrusted and SHALL validate its structure and source evidence before exposing a draft for review.

#### Scenario: Valid item cites source evidence
- **WHEN** the interpreter returns a Song, Separator, Transition, or Comment
- **THEN** the returned item includes supported source-row and source-cell evidence, a confidence value, and a server-assigned stable draft item identity

#### Scenario: Invalid response is rejected
- **WHEN** AI output is malformed, contains unsupported kinds, invalid enum values, out-of-bounds cells, mismatched evidence values, invalid ordering, or invalid field lengths
- **THEN** the interpretation job fails without returning a partially accepted draft

#### Scenario: Multiple source rows are retained
- **WHEN** an interpreted item is supported by more than one source row or cell
- **THEN** the draft preserves all cited evidence while retaining one primary source row for compatibility and display

### Requirement: Interpretation is recoverable without deterministic parsing
The system SHALL run AI interpretation as a persisted asynchronous job and SHALL provide retry and retained-source-backed manual authoring when automatic interpretation cannot produce a valid draft.

#### Scenario: Interpretation completes after browser interruption
- **WHEN** a user backgrounds, refreshes, or disconnects after starting interpretation
- **THEN** the user can later retrieve the owner-scoped job status, source grid, and validated draft

#### Scenario: Interpretation fails
- **WHEN** the AI provider is unavailable or the response fails validation
- **THEN** the user sees a safe failure message and can retry the job or start an empty manual draft while the owner-scoped job retains the source grid for retry and audit

#### Scenario: Manual authoring does not parse rows
- **WHEN** a user starts manual authoring from a failed or unavailable interpretation
- **THEN** the system presents editable set-list item controls without applying deterministic row parsing heuristics

### Requirement: Source evidence remains traceable
The system SHALL persist raw source grid data only in owner-scoped interpretation job state and SHALL persist item-level source evidence with saved set-list items.

#### Scenario: Raw grid is protected
- **WHEN** another authenticated user requests an interpretation job or its source grid
- **THEN** the system does not expose the job state, result, or raw grid

#### Scenario: Saved item retains evidence
- **WHEN** a user saves an interpreted or manually authored set-list draft
- **THEN** each saved item retains its primary source row and available cited source evidence for later review
