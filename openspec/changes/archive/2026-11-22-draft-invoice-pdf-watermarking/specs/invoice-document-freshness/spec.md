## MODIFIED Requirements

### Requirement: Invoice PDFs represent current invoice data
The system SHALL treat an invoice PDF as a derived representation of a specific PDF-relevant invoice-data revision and SHALL expose whether that representation is current, missing, regenerating, or failed. Current readiness SHALL also require compatibility between stored watermark provenance and the invoice lifecycle state: Draft and never-issued Cancelled documents MUST be known to be watermarked; Issued, Paid, Overdue, and previously issued Cancelled documents MUST NOT be known to contain a draft watermark. Legacy documents eligible for clean output with unknown provenance SHALL retain existing revision/readability eligibility.

#### Scenario: Existing current document is reported
- **WHEN** an invoice has a readable PDF representing its current invoice-data revision with compatible watermark provenance
- **THEN** the system reports the document as current and includes its available PDF metadata

#### Scenario: Document is not current after invoice data changes
- **WHEN** a successful manual adjustment changes an invoice total or line item
- **THEN** the system SHALL not report the previous PDF as current

#### Scenario: Draft has unknown or incompatible watermark provenance
- **WHEN** a draft PDF has unknown provenance or is recorded as unwatermarked, even if its data revisions match
- **THEN** the system SHALL report the document unavailable and SHALL NOT serve its bytes as current

#### Scenario: Non-draft has a watermarked PDF
- **WHEN** an invoice eligible for clean output refers to a PDF recorded as watermarked
- **THEN** the system SHALL report it unavailable until a compatible replacement is generated

#### Scenario: Legacy issued document remains available
- **WHEN** an existing PDF eligible for clean output has unknown watermark provenance but satisfies existing revision/readability checks
- **THEN** it SHALL remain eligible for normal document actions without mandatory regeneration

### Requirement: Non-current invoice PDFs are blocked at delivery boundaries
The system SHALL refuse to download, email, or publish an invoice PDF unless it is current for the invoice's latest PDF-relevant data revision and its watermark provenance is compatible with the current lifecycle state.

#### Scenario: Download is blocked for a stale or regenerating document
- **WHEN** a user requests a PDF download for an invoice whose document is missing, regenerating, stale, or failed
- **THEN** the system SHALL return an actionable unavailable-document response and SHALL not return PDF bytes

#### Scenario: Email delivery is blocked for a stale or regenerating document
- **WHEN** a user requests email delivery for an invoice whose document is missing, regenerating, stale, or failed
- **THEN** the system SHALL reject delivery before sending email and SHALL not update delivery history as successful

#### Scenario: Publishing is blocked for a stale or regenerating document
- **WHEN** a user requests Google Drive publishing for an invoice whose document is missing, regenerating, stale, or failed
- **THEN** the system SHALL reject publishing and SHALL not upload the non-current document

#### Scenario: Watermark provenance is incompatible
- **WHEN** download, email preparation/delivery, or Drive publishing is requested for a document whose watermark provenance is incompatible with the invoice state
- **THEN** the system SHALL block the action before returning, sending, or publishing bytes and SHALL provide actionable recovery feedback

## ADDED Requirements

### Requirement: Legacy drafts require explicit watermark recovery
At rollout the system SHALL invalidate pre-watermark Draft and never-issued Cancelled documents and expose an explicit regeneration recovery path. Invalidation and recovery SHALL preserve business data and lifecycle/delivery audit history. Passive reads SHALL NOT repair documents.

#### Scenario: Migration encounters an existing draft
- **WHEN** the watermark migration processes an existing Draft invoice
- **THEN** its old PDF SHALL become unavailable with an actionable regeneration explanation while invoice content and lifecycle/delivery history remain unchanged

#### Scenario: User explicitly regenerates a legacy draft
- **WHEN** the user requests regeneration of an invalidated draft and generation succeeds
- **THEN** the latest invoice content SHALL have a current watermarked PDF with recorded provenance and no additional issue, reissue, or delivery event

#### Scenario: Legacy draft recovery fails
- **WHEN** rendering or storing the regenerated draft fails
- **THEN** the old unwatermarked document SHALL remain unavailable and the user SHALL retain an explicit retry path

#### Scenario: Migration encounters a never-issued cancelled invoice
- **WHEN** the watermark migration processes a Cancelled invoice without a first-issue timestamp
- **THEN** its old PDF SHALL become unavailable until explicit regeneration produces a watermarked replacement without issuing the invoice

#### Scenario: User merely views an invalidated draft
- **WHEN** invoice metadata, thumbnail, full preview, or PDF read is requested for an invalidated draft
- **THEN** the system SHALL report unavailability without generating or mutating the document
