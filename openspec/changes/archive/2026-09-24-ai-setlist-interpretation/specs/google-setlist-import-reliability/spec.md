## ADDED Requirements

### Requirement: Set-list source reads support document interpretation
The system SHALL report actionable source-read failures for bounded worksheet-grid acquisition used by AI set-list interpretation.

#### Scenario: Google rejects grid acquisition
- **WHEN** Google rejects or fails a bounded worksheet-grid read for a linked set-list resource
- **THEN** the API SHALL return a non-success response with a user-facing message that the worksheet document could not be read for interpretation

#### Scenario: Worksheet selection remains resource-scoped
- **WHEN** a user starts interpretation for a selected Google Sheet set-list resource and worksheet
- **THEN** the system SHALL acquire the grid from that selected resource and worksheet rather than another set-list resource on the gig
