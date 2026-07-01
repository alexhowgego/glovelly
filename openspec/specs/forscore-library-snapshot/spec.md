# forScore Library Snapshot Specification

## Purpose

TBD - captures requirements for importing and exposing read-only forScore library snapshots.

## Requirements

### Requirement: User can import a forScore library snapshot
The system SHALL allow an authenticated user to upload a forScore `.4sb` library export as a read-only library snapshot import.

#### Scenario: Successful upload creates snapshot
- **WHEN** an authenticated user uploads a valid `.4sb` file exported by forScore
- **THEN** the system creates a library snapshot owned by that user and returns the import status, original file name, import timestamp, and chart count

#### Scenario: Unauthenticated upload is rejected
- **WHEN** a request uploads a `.4sb` file without an authenticated session
- **THEN** the system rejects the request without creating a snapshot

#### Scenario: Unsupported file is rejected
- **WHEN** an authenticated user uploads a file that cannot be parsed as a supported forScore `.4sb` export
- **THEN** the system rejects the import with a clear validation error and does not create an active snapshot

### Requirement: System extracts chart metadata from .4sb exports
The system SHALL parse the `.4sb` wrapper, decompress the contained gzip payload, parse the Apple binary property list, and extract chart records from score metadata keys.

#### Scenario: Chart fields are extracted
- **WHEN** the backup contains score metadata for a chart with title and file path
- **THEN** the imported chart record includes the raw title, file path, normalized title, and any supported optional metadata present in the backup

#### Scenario: Wrapper offset varies
- **WHEN** the gzip payload is present at a different offset within the `.4sb` wrapper
- **THEN** the parser locates the gzip payload by content rather than relying on a fixed byte offset

#### Scenario: Incomplete chart metadata is skipped with warning
- **WHEN** the backup contains score-like metadata without both a usable title and file path
- **THEN** the system skips that incomplete chart and records a non-fatal import warning

### Requirement: Latest successful snapshot becomes active
The system SHALL maintain one active forScore library snapshot per user, with the latest successful import becoming active.

#### Scenario: First import is active
- **WHEN** a user imports their first valid forScore library snapshot
- **THEN** that snapshot is marked active for the user

#### Scenario: New import supersedes previous active snapshot
- **WHEN** a user imports another valid forScore library snapshot
- **THEN** the new snapshot is marked active and the user's previous active snapshot is no longer active

#### Scenario: Failed import does not replace active snapshot
- **WHEN** a user with an active snapshot attempts to import an invalid `.4sb` file
- **THEN** the existing active snapshot remains active

### Requirement: Snapshot chart records are user-scoped
The system SHALL only expose imported forScore snapshots and chart records to the user who owns them.

#### Scenario: User lists own active snapshot
- **WHEN** an authenticated user requests their active forScore library snapshot
- **THEN** the system returns only that user's active snapshot metadata and chart records

#### Scenario: User cannot access another user's snapshot
- **WHEN** an authenticated user requests a snapshot owned by a different user
- **THEN** the system does not expose the other user's snapshot or chart records

### Requirement: Non-chart backup content is ignored
The system SHALL ignore forScore set lists, annotations, system settings, and embedded binary assets when importing a library snapshot.

#### Scenario: Existing forScore set lists are ignored
- **WHEN** the `.4sb` backup contains `&SET;` set list entries
- **THEN** the system does not create Glovelly set list imports or chart records from those entries

#### Scenario: Annotation and system metadata are ignored
- **WHEN** the `.4sb` backup contains page annotations, `&SYS;` settings, or embedded binary assets
- **THEN** the system does not store those values as part of the imported chart catalogue
