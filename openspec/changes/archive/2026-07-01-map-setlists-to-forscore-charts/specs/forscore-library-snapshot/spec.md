## MODIFIED Requirements

### Requirement: Latest successful snapshot becomes active
The system SHALL maintain one active forScore library snapshot per user, with the latest successful import becoming active, and SHALL assess mapped set lists that may need chart-link review after the active snapshot changes.

#### Scenario: First import is active
- **WHEN** a user imports their first valid forScore library snapshot
- **THEN** that snapshot is marked active for the user

#### Scenario: New import supersedes previous active snapshot
- **WHEN** a user imports another valid forScore library snapshot
- **THEN** the new snapshot is marked active and the user's previous active snapshot is no longer active

#### Scenario: Failed import does not replace active snapshot
- **WHEN** a user with an active snapshot attempts to import an invalid `.4sb` file
- **THEN** the existing active snapshot remains active

#### Scenario: New import assesses affected mapped set lists
- **WHEN** a user imports a new valid forScore library snapshot and has active set lists for Draft or Confirmed future or undated gigs with chart mappings from a previous snapshot
- **THEN** the system checks those mapped set list items against the new active snapshot and reports how many set lists and items need review

#### Scenario: Exact file path matches are relinked
- **WHEN** a mapped set list item points to a chart from the previous snapshot and the new active snapshot contains a chart with the same file path
- **THEN** the system updates the item to reference the matching chart in the new snapshot without requiring user review

#### Scenario: Ambiguous or missing rematches require review
- **WHEN** a mapped set list item cannot be safely relinked to exactly one chart in the new active snapshot by file path
- **THEN** the system keeps the prior copied chart identity visible and marks the item as needing review or missing from the latest library

#### Scenario: Snapshot import is not blocked by mapping drift
- **WHEN** a new valid forScore library snapshot would cause saved set list mappings to need review
- **THEN** the system still completes the snapshot import and guides the user to the affected set lists
