## ADDED Requirements

### Requirement: Selected set-list resource controls worksheet metadata
The system SHALL load worksheet metadata for the Google Sheet set-list resource selected by the user.

#### Scenario: User imports a non-primary set-list resource
- **WHEN** a gig has multiple Google Sheet set-list resources and the user opens import for a specific non-primary resource
- **THEN** the worksheet list SHALL be read from that selected resource's spreadsheet, not from the gig's primary or title-sorted set-list resource

#### Scenario: Selected resource is not a Google Sheet set-list
- **WHEN** a set-list import source request references a resource that is not a Google Sheet with set-list purpose
- **THEN** the API SHALL return a validation response explaining that the source resource must be a Google Sheet set-list

### Requirement: Shared Google connection state remains token-aligned
The system SHALL keep stored Google Drive, Google Sheets, and Google Calendar connection state aligned with the token material saved for the user.

#### Scenario: User connects Sheets after Drive
- **WHEN** a user already has Google Drive connected and starts the Google Sheets authorization flow
- **THEN** the authorization request SHALL include both the existing Drive scope and the required Sheets scope

#### Scenario: User connects Drive after Sheets
- **WHEN** a user already has Google Sheets connected and starts the Google Drive authorization flow
- **THEN** the authorization request SHALL include both the existing Sheets scope and the required Drive scope

#### Scenario: User connects Calendar after another Google service
- **WHEN** a user already has Google Drive or Google Sheets connected and starts the Google Calendar authorization flow
- **THEN** the authorization request SHALL include the existing connected Google service scopes and the required Calendar scope

#### Scenario: User connects another Google service after Calendar
- **WHEN** a user already has Google Calendar connected and starts the Google Drive or Google Sheets authorization flow
- **THEN** the authorization request SHALL include the existing Calendar scope and the required scope for the service being connected

#### Scenario: User disconnects one Google service
- **WHEN** a user disconnects Google Drive, Google Sheets, or Google Calendar while another Google service scope remains connected
- **THEN** the system SHALL remove only the disconnected service scope and keep the remaining Google connection active

#### Scenario: User disconnects final Google service
- **WHEN** a user disconnects the only remaining Google service scope
- **THEN** the system SHALL revoke the shared Google connection and clear stored token material

### Requirement: Set-list import returns actionable Google failure responses
The system SHALL convert expected Google connection and Sheets API failures during set-list source loading or preview into actionable API responses.

#### Scenario: Sheets connection requires reconnect
- **WHEN** a set-list source or preview request cannot obtain a valid Google Sheets access token because the connection is missing, expired, revoked, missing a refresh token, or missing the Sheets scope
- **THEN** the API SHALL return a conflict response instructing the client to reconnect Google Sheets

#### Scenario: Google Sheets metadata read fails
- **WHEN** Google rejects or fails a spreadsheet metadata read for a linked set-list resource
- **THEN** the API SHALL return a non-success response with a user-facing message that the linked Google Sheet could not be read

#### Scenario: Google Sheets worksheet values read fails
- **WHEN** Google rejects or fails a worksheet values read during set-list preview
- **THEN** the API SHALL return a non-success response with a user-facing message that the worksheet rows could not be read

#### Scenario: Worksheet metadata is empty
- **WHEN** Google Sheets metadata does not include any worksheets for the linked spreadsheet
- **THEN** the API SHALL return a non-success response explaining that no worksheets were found
