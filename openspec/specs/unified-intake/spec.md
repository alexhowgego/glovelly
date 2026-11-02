## Purpose

Define unified source intake, validated receipt and resource application, and optional saved-attachment review.

## Requirements

### Requirement: One source-selection and upload journey accepts files, URLs, and text
The system SHALL expose one authenticated `+` Add to Glovelly action leading to a source type selection screen, then a unified upload dialog for photo/file, HTTP(S) URL, or pasted text. The system SHALL retain submitted input during submission, analysis, and recoverable action. The global action SHALL NOT directly open a file picker or bypass source selection.

#### Scenario: User selects a file source
- **WHEN** a user activates Add to Glovelly and chooses photo/file
- **THEN** the system SHALL enter the unified upload dialog and provide file acquisition there

#### Scenario: User selects URL or text
- **WHEN** a user chooses URL or pasted text from source selection and submits valid input in upload
- **THEN** the system SHALL accept the source and begin analysis in the same upload presentation

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

### Requirement: Proactive receipt application uses the nearest eligible gig
The system SHALL proactively create an editable receipt draft and original source attachment in upload when validated receipt confidence meets the authenticated user's automatic application preference and an eligible initial nearby candidate exists. It SHALL select the nearest candidate under existing configured ranking regardless of other nearby candidates. Manual only SHALL disable proactive application; High confidence SHALL accept High and Medium confidence SHALL accept Medium or High. The default SHALL be High confidence and legacy Very high confidence SHALL behave as High confidence.

#### Scenario: Multiple gigs are nearby
- **WHEN** an eligible receipt meets the user's preference and multiple visible non-cancelled nearby gigs exist
- **THEN** the system SHALL proactively attach the receipt to the first candidate in existing nearest-gig order without requiring confirmation because of other candidates

#### Scenario: User disables automatic application
- **WHEN** a user with Manual only submits a receipt
- **THEN** upload SHALL retain the source and require explicit receipt application before creating business data

#### Scenario: Receipt confidence does not meet preference
- **WHEN** validated receipt confidence is below the selected level
- **THEN** upload SHALL retain the source and support explicit application rather than automatically saving

#### Scenario: No nearby candidate exists
- **WHEN** receipt analysis succeeds but the initial nearby candidate list is empty
- **THEN** upload SHALL allow the user to load eligible gigs and select a destination before explicit application

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

### Requirement: Failed analysis and application are recoverable without duplicate saves
The system SHALL validate analysis before use and preserve the source on provider, unsupported-input, timeout, malformed-response, or application failure. It SHALL support retry and explicit supported treatment in upload. Repeated or overlapping application requests for the same intake SHALL NOT create duplicate expenses, resources, or attachments. Attached state SHALL appear only after confirmed persistence.

#### Scenario: Analysis cannot produce a valid result
- **WHEN** the provider is unavailable or returns unsupported, malformed, or invalid output
- **THEN** upload SHALL show a safe recoverable failure and offer retry or explicit treatment without applying unvalidated suggestions

#### Scenario: Application fails
- **WHEN** saving a proposed attachment fails
- **THEN** the system SHALL keep the source recoverable and SHALL NOT display Attached to gig

#### Scenario: Application response is interrupted
- **WHEN** a successful application request is repeated after its response was interrupted
- **THEN** the system SHALL recover or identify the original saved result without creating a second attachment or business record

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

### Requirement: Review attachment is an optional saved-record phase
The system SHALL open a Review attachment dialog only on explicit selection from attached state, using the saved destination and attachment identifiers. Receipt and resource review SHALL support existing source inspection, correction, reassignment, and deletion workflows without resubmitting the intake source or creating a second attachment. Review SHALL NOT be required to retain a successfully applied item.

#### Scenario: User reviews an attached receipt or resource
- **WHEN** a user selects Review attachment in attached state
- **THEN** the system SHALL open correction for that saved item without uploading or applying it again

#### Scenario: Review closes without edits
- **WHEN** a user leaves review without saving changes
- **THEN** the saved attachment and destination SHALL remain unchanged

### Requirement: Both receipt application modes preserve field and invoice behavior
Automatic and explicit receipt application SHALL independently initialize editable description and category from validated high-confidence merchant and category suggestions. Amount SHALL initialize from a high-confidence total only when currency is also high-confidence GBP; foreign or unknown currency SHALL leave amount for explicit GBP entry without blocking attachment. Other suggestions SHALL remain reviewable evidence. The system SHALL preserve receipt validation, source linkage, reassignment, and draft-only invoice refresh without changing finalized invoice content.

#### Scenario: Receipt is applied with high-confidence fields
- **WHEN** either application mode saves a receipt with independently high-confidence merchant, total, or category suggestions
- **THEN** the corresponding saved editable fields SHALL be initialized regardless of whether overall confidence allowed proactive application, with amount subject to confirmed GBP currency

#### Scenario: Receipt currency is foreign or unknown
- **WHEN** a receipt has a high-confidence total but no high-confidence GBP currency
- **THEN** the attachment SHALL still be saved when otherwise eligible, with total retained as evidence and expense amount left for explicit GBP entry

#### Scenario: Cleanup or event publication fails after saving
- **WHEN** transient cleanup or publication fails after a receipt is committed
- **THEN** financial refresh SHALL still be attempted independently and any pending/failed invoice refresh SHALL remain durable and visible on recovery

#### Scenario: User replaces a source after interrupted submission
- **WHEN** a user chooses a different source after a response failure
- **THEN** it SHALL receive a new intake identity and SHALL NOT silently recover the earlier source as the replacement

#### Scenario: User corrects or reassigns a receipt
- **WHEN** saved receipt fields or destination are changed through review
- **THEN** the system SHALL use existing validation and refresh only affected draft invoices

### Requirement: Expense categories are optional and editable for every expense
The system SHALL allow users to select, change, or clear a nullable category on all expenses regardless of provenance. Allowed categories SHALL be Travel, Meals, Accommodation, Equipment, and Other. Category SHALL NOT change invoice wording or reimbursement behavior.

#### Scenario: User categorizes an expense
- **WHEN** a user edits an ordinary expense or an applied receipt
- **THEN** the category SHALL be editable and clearable through ordinary expense controls

#### Scenario: Category is used on an invoiced expense
- **WHEN** a categorized expense is included in an invoice
- **THEN** its generated wording and reimbursement behavior SHALL remain unchanged

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
