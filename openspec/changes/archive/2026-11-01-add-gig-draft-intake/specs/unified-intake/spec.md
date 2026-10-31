## ADDED Requirements

### Requirement: Source format does not determine business intent
Unified analysis SHALL propose Receipt, Resource, Gig(s), or an uncertain outcome from supported source contents and metadata. PDF upload SHALL NOT imply gig intake. The system SHALL present the proposal and available confidence/evidence and SHALL allow an authoritative supported Treat as override without requiring an upfront intent choice.

#### Scenario: PDF is a receipt
- **WHEN** a user uploads a PDF classified as a validated receipt
- **THEN** the system SHALL retain existing receipt processing and automatic-application preferences rather than route it to gig imports because of its format

#### Scenario: PDF describes gigs
- **WHEN** a PDF is interpreted as information about one or more new gigs
- **THEN** the system SHALL offer staged gig preparation and SHALL NOT create live gigs

#### Scenario: Override survives retry
- **WHEN** a user overrides the proposed intent and retries analysis
- **THEN** the override SHALL remain authoritative and visible and incompatible stale results SHALL NOT be applied

#### Scenario: Analysis cannot suggest a treatment
- **WHEN** analysis fails or returns an unknown intent
- **THEN** no receipt treatment SHALL be preselected and the user SHALL be able to choose Gig import explicitly

#### Scenario: Google Doc could not be read
- **WHEN** a Google Doc URL remains unreadable
- **THEN** Gig import SHALL remain selectable and the system SHALL explain that document access or alternative source input is required before preparation

### Requirement: Google Docs content acquisition is authorised and bounded
The system SHALL recognise supported Google Docs URLs and acquire bounded document content using the authenticated user's authorised Google access for analysis. It SHALL NOT scrape arbitrary URLs. Other URLs, including Sheets, SHALL retain metadata-only resource handling in this change.

#### Scenario: Google Doc may describe gigs or a resource
- **WHEN** an accessible Google Doc URL is submitted
- **THEN** the system SHALL analyse its supported content and propose an intent without automatically creating a gig or resource relationship

#### Scenario: Google access is missing
- **WHEN** document content cannot be read because connection or permission is missing
- **THEN** the system SHALL retain the URL and offer reconnect/retry, alternate supported input, and explicit resource treatment without requiring content access to attach the URL

### Requirement: Prepared gig batches have a distinct success state
The system SHALL show a confirmed Draft batch prepared result with Review import and Done actions after durable preparation. It SHALL NOT label this as Attached to gig or automatically navigate, close intake, or commit gigs.

#### Scenario: User opens prepared import
- **WHEN** a user explicitly selects Review import
- **THEN** the system SHALL open the existing batch review workspace for the prepared batch

### Requirement: Existing receipt and set-list boundaries remain intact
Existing receipt preferences SHALL continue to govern automatic receipt application. Resources SHALL continue to require explicit confirmation. Dedicated set-list routing SHALL NOT be introduced; users SHALL attach URLs as resources and explicitly go to the gig to invoke the existing interpretation workflow.

#### Scenario: User attaches a set-list Sheet
- **WHEN** a user explicitly confirms a Sheet URL as a resource for a selected gig
- **THEN** the system SHALL preserve normal resource application, offer explicit Go to gig access, and SHALL NOT automatically interpret or create a set list

## MODIFIED Requirements

### Requirement: Analysis and application keep the unified upload dialog open
The system SHALL present progress, validated intent, confidence, structured detected facts, and owner-visible gig candidates where relevant within the unified upload dialog. Both automatic and explicit successful receipt or resource application SHALL display the same compact `Attached to [Gig]` state with Review attachment and Done actions. Successful gig draft preparation SHALL display a distinct prepared-batch state with Review import and Done. The system SHALL NOT automatically close upload, navigate away, or open review on success.

#### Scenario: Receipt is automatically applied
- **WHEN** an eligible receipt is saved automatically while upload is open
- **THEN** the dialog SHALL remain open and display its actual saved destination with Review attachment and Done

#### Scenario: User applies explicitly
- **WHEN** explicit receipt or resource application succeeds
- **THEN** upload SHALL display the same attached state and actions as automatic application

#### Scenario: User finishes without review
- **WHEN** a user selects Done or closes upload after attached state is confirmed
- **THEN** the saved attachment SHALL remain available in its gig without requiring review

#### Scenario: User prepares gig drafts
- **WHEN** gig draft preparation succeeds
- **THEN** upload SHALL remain open and display the prepared batch with Review import and Done

### Requirement: Current unapplied intake is private and recoverable
The system SHALL retain at most one current unapplied intake per authenticated user with source metadata and analysis state/result until replacement or explicit discard. It SHALL remove the private intake and transient source after establishing successful durable attachment application or batch preparation, including durable source provenance for a batch. It SHALL NOT expose history, management navigation, or expiry controls. Successful cleanup SHALL NOT erase the authoritative result from the open upload session.

#### Scenario: User closes before application
- **WHEN** a user closes upload before any application or preparation succeeds
- **THEN** the unapplied source SHALL remain recoverable on reopening

#### Scenario: Replacement or discard removes the source
- **WHEN** a user submits a replacement or explicitly discards an unapplied intake
- **THEN** the old private intake and transient source SHALL be removed without affecting previously saved attachments or batches

#### Scenario: Successful application clears private state
- **WHEN** application creates the destination record and source relationship
- **THEN** subsequent current-intake reads SHALL NOT return the completed intake
- **AND** the open upload dialog SHALL retain the authoritative application result for attached state and optional review

#### Scenario: Successful draft preparation clears private state
- **WHEN** a batch and its source provenance are durably prepared
- **THEN** subsequent current-intake reads SHALL NOT return the completed intake
- **AND** the prepared batch SHALL remain recoverable for review

### Requirement: Resources require explicit application within upload
The system SHALL require explicit user confirmation, a visible selected gig, and validated resource type, purpose, and title before creating a resource. It SHALL show original source and available analysis evidence within upload. URL submission SHALL validate HTTP(S); supported Google Docs MAY be read through authorised bounded acquisition for intent analysis, while other URLs SHALL use metadata without remote content retrieval.

#### Scenario: User confirms a resource
- **WHEN** a user completes resource fields and confirms application in upload
- **THEN** the system SHALL create the resource with selected purpose and original file or URL linkage and show attached state

#### Scenario: User overrides intent
- **WHEN** a user explicitly selects a supported treatment for uncertain or failed analysis
- **THEN** upload SHALL allow outcome-specific validation without creating business data solely from the override

#### Scenario: User leaves an unapplied resource
- **WHEN** a user closes upload without confirming resource application
- **THEN** no resource SHALL be created or changed and the current unapplied intake SHALL remain recoverable

#### Scenario: Google Sheet URL is submitted
- **WHEN** a recognised Google Sheet URL is submitted
- **THEN** the system SHALL infer its resource type without loading its contents or starting set-list interpretation

#### Scenario: Google Doc URL is submitted
- **WHEN** a recognised Google Doc URL is submitted
- **THEN** the system SHALL support authorised content analysis while retaining explicit resource treatment and confirmation
