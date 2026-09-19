## ADDED Requirements

### Requirement: Generated-line changes regenerate draft invoice PDFs
The system SHALL treat a successful generated-line refresh for a draft invoice as PDF-relevant data change. It SHALL persist the updated lines and advance the document revision before generating a replacement PDF, and SHALL report the document as current only when that replacement represents the latest revision.

#### Scenario: Draft generated lines refresh successfully
- **WHEN** a draft invoice's generated lines are refreshed from changed linked gig data and PDF generation succeeds
- **THEN** the invoice SHALL report a current PDF revision whose content represents the refreshed lines and total

### Requirement: Failed generated-line PDF regeneration is recoverable and safe
The system SHALL preserve successfully rebuilt draft generated lines when their replacement PDF cannot be rendered or stored, SHALL mark the affected document unavailable, and SHALL retain the existing retry and delivery-boundary blocking behavior.

#### Scenario: Generated-line PDF regeneration fails
- **WHEN** a draft invoice's generated lines are persisted but replacement PDF generation fails
- **THEN** the invoice SHALL report a failed or unavailable document, the previous PDF SHALL NOT be treated as current, and download, email delivery, and publishing SHALL be blocked until regeneration succeeds
