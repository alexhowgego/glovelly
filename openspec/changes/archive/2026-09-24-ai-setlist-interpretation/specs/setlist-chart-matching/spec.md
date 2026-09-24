## ADDED Requirements

### Requirement: Chart matching follows reviewed logical set-list items
The system SHALL locate forScore candidates only for valid Song items in an interpreted or manually authored review draft, after source interpretation is complete.

#### Scenario: Interpretation precedes candidate lookup
- **WHEN** a user starts a new Google Sheet set-list import
- **THEN** the system does not request forScore candidates until a valid interpreted draft or a manual draft exists for review

#### Scenario: Non-song interpreted item is not matched
- **WHEN** an interpreted item is a separator, transition, or comment
- **THEN** the system does not send it to forScore candidate lookup or AI chart selection

### Requirement: Chart-match results use stable item identity
The system SHALL correlate candidate and AI chart-matching results to stable set-list item identity rather than only to source row number.

#### Scenario: Multi-row evidence does not collide
- **WHEN** multiple interpreted items cite overlapping or multiple source rows
- **THEN** chart candidates and selected chart results apply only to the matching stable item and do not overwrite another item's result
