## Purpose

Ensure invoice document actions use a PDF that represents the current invoice data.

## Requirements

### Requirement: Invoice PDFs represent current invoice data
The system SHALL treat an invoice PDF as a derived representation of a specific PDF-relevant invoice-data revision and SHALL expose whether that representation is current, missing, regenerating, or failed.

#### Scenario: Existing current document is reported
- **WHEN** an invoice has a readable PDF representing its current invoice-data revision
- **THEN** the system reports the document as current and includes its available PDF metadata

#### Scenario: Document is not current after invoice data changes
- **WHEN** a successful manual adjustment changes an invoice total or line item
- **THEN** the system SHALL not report the previous PDF as current

### Requirement: Manual adjustment changes regenerate the invoice PDF
The system SHALL regenerate the invoice PDF from the updated invoice data after persisting or removing a manual adjustment and SHALL replace the invoice's current document representation only with output for the latest invoice-data revision.

#### Scenario: Adjustment produces a current replacement PDF
- **WHEN** a user submits a valid manual adjustment and PDF regeneration succeeds
- **THEN** the adjustment response identifies the updated invoice and a current PDF whose content reflects the updated lines and total

#### Scenario: Repeated adjustments retain only the latest representation
- **WHEN** a user applies more than one manual adjustment to an invoice
- **THEN** the current PDF SHALL represent the invoice after the latest adjustment and SHALL retain the same invoice number and delivery history

#### Scenario: Removing an adjustment produces a current replacement PDF
- **WHEN** a user removes a manual adjustment from an invoice and PDF regeneration succeeds
- **THEN** the adjustment removal response identifies a current PDF whose content and total no longer include the removed adjustment

### Requirement: Failed PDF regeneration is recoverable and safe
The system SHALL preserve a successfully saved adjustment when its PDF regeneration fails, SHALL mark the document as failed or unavailable, and SHALL provide a transient inline regeneration retry that does not repeat the adjustment or alter invoice lifecycle audit fields.

#### Scenario: Regeneration fails after a valid adjustment
- **WHEN** a valid adjustment is persisted but PDF rendering or storage fails
- **THEN** the system reports that the adjustment succeeded but the document is unavailable and SHALL not present the previous PDF as current

#### Scenario: User retries a failed regeneration from its failure state
- **WHEN** a user selects the inline retry offered for an invoice with a failed or unavailable document and rendering succeeds
- **THEN** the system SHALL create a current PDF from the latest invoice data without adding another adjustment or delivery record

### Requirement: Non-current invoice PDFs are blocked at delivery boundaries
The system SHALL refuse to download, email, or publish an invoice PDF unless it is current for the invoice's latest PDF-relevant data revision.

#### Scenario: Download is blocked for a stale or regenerating document
- **WHEN** a user requests a PDF download for an invoice whose document is missing, regenerating, stale, or failed
- **THEN** the system SHALL return an actionable unavailable-document response and SHALL not return PDF bytes

#### Scenario: Email delivery is blocked for a stale or regenerating document
- **WHEN** a user requests email delivery for an invoice whose document is missing, regenerating, stale, or failed
- **THEN** the system SHALL reject delivery before sending email and SHALL not update delivery history as successful

#### Scenario: Publishing is blocked for a stale or regenerating document
- **WHEN** a user requests Google Drive publishing for an invoice whose document is missing, regenerating, stale, or failed
- **THEN** the system SHALL reject publishing and SHALL not upload the non-current document

### Requirement: Document state is understandable in the invoice workspace
The system SHALL show the current document state in the invoice workspace and SHALL disable or explain unavailable document actions while a document is missing, regenerating, stale, or failed.

#### Scenario: Regeneration completes with the adjustment
- **WHEN** an adjustment response contains a current regenerated document
- **THEN** the workspace SHALL confirm that the adjustment and PDF regeneration completed

#### Scenario: Regeneration requires recovery
- **WHEN** an adjustment response contains a failed or unavailable document state
- **THEN** the workspace SHALL state that the adjustment was saved, explain that the PDF is unavailable, and offer an inline regeneration retry without adding a persistent invoice-toolbar action

#### Scenario: Drive publishing explains unavailable documents
- **WHEN** an invoice document is missing, regenerating, stale, or failed
- **THEN** the workspace SHALL disable Drive publishing and explain the document state using the same visible readiness feedback as the other document actions
