## ADDED Requirements

### Requirement: One source-selection and upload journey accepts files, URLs, and text
The system SHALL expose one authenticated `+` Add to Glovelly action leading to a source type selection screen, then a unified upload dialog for photo/file, HTTP(S) URL, or pasted text. The system SHALL retain submitted input during submission, analysis, and recoverable action. The global action SHALL NOT directly open a file picker or bypass source selection.

#### Scenario: User selects a file source
- **WHEN** a user activates Add to Glovelly and chooses photo/file
- **THEN** the system SHALL enter the unified upload dialog and provide file acquisition there

#### Scenario: User selects URL or text
- **WHEN** a user chooses URL or pasted text from source selection and submits valid input in upload
- **THEN** the system SHALL accept the source and begin analysis in the same upload presentation

### Requirement: Analysis and application keep the unified upload dialog open
The system SHALL present progress, validated intent, confidence, structured detected facts, and owner-visible gig candidates within the unified upload dialog. Both automatic and explicit successful application SHALL display the same compact `Attached to [Gig]` state with Review attachment and Done actions. The system SHALL NOT automatically close upload, navigate away, or open review on successful application.

#### Scenario: Receipt is automatically applied
- **WHEN** an eligible receipt is saved automatically while upload is open
- **THEN** the dialog SHALL remain open and display its actual saved destination with Review attachment and Done

#### Scenario: User applies explicitly
- **WHEN** explicit receipt or resource application succeeds
- **THEN** upload SHALL display the same attached state and actions as automatic application

#### Scenario: User finishes without review
- **WHEN** a user selects Done or closes upload after attached state is confirmed
- **THEN** the saved attachment SHALL remain available in its gig without requiring review

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
The system SHALL retain at most one current unapplied intake per authenticated user with source metadata and analysis state/result until replacement or explicit discard. It SHALL remove the private intake and transient source after establishing successful durable application. It SHALL NOT expose history, management navigation, or expiry controls. Successful cleanup SHALL NOT erase attached state from the open upload session.

#### Scenario: User closes before application
- **WHEN** a user closes upload before any application succeeds
- **THEN** the unapplied source SHALL remain recoverable on reopening

#### Scenario: Replacement or discard removes the source
- **WHEN** a user submits a replacement or explicitly discards an unapplied intake
- **THEN** the old private intake and transient source SHALL be removed without affecting previously saved attachments

#### Scenario: Successful application clears private state
- **WHEN** application creates the destination record and source relationship
- **THEN** subsequent current-intake reads SHALL NOT return the completed intake
- **AND** the open upload dialog SHALL retain the authoritative application result for attached state and optional review

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
The system SHALL require explicit user confirmation, a visible selected gig, and validated resource type, purpose, and title before creating a resource. It SHALL show original source and available analysis evidence within upload. URL analysis SHALL validate HTTP(S) and infer supported resource types from metadata without retrieving remote document contents.

#### Scenario: User confirms a resource
- **WHEN** a user completes resource fields and confirms application in upload
- **THEN** the system SHALL create the resource with the selected purpose and original file or URL linkage and show attached state

#### Scenario: User overrides intent
- **WHEN** a user explicitly treats uncertain or failed analysis as a supported receipt or resource
- **THEN** upload SHALL allow destination and outcome-specific validation before application without creating data solely from the override

#### Scenario: User leaves an unapplied resource
- **WHEN** a user closes upload without confirming resource application
- **THEN** the system SHALL NOT create or change a resource and SHALL retain the current unapplied intake

#### Scenario: Google document URL is submitted
- **WHEN** a recognized Google Doc or Sheet URL is submitted
- **THEN** the system SHALL infer its type without loading its contents

### Requirement: Review attachment is an optional saved-record phase
The system SHALL open a Review attachment dialog only on explicit selection from attached state, using the saved destination and attachment identifiers. Receipt and resource review SHALL support existing source inspection, correction, reassignment, and deletion workflows without resubmitting the intake source or creating a second attachment. Review SHALL NOT be required to retain a successfully applied item.

#### Scenario: User reviews an attached receipt or resource
- **WHEN** a user selects Review attachment in attached state
- **THEN** the system SHALL open correction for that saved item without uploading or applying it again

#### Scenario: Review closes without edits
- **WHEN** a user leaves review without saving changes
- **THEN** the saved attachment and destination SHALL remain unchanged

### Requirement: Both receipt application modes preserve field and invoice behavior
Automatic and explicit receipt application SHALL independently initialize editable description, amount, and category from their respective validated high-confidence merchant, total, and category suggestions. Other suggestions SHALL remain reviewable evidence. The system SHALL preserve receipt validation, source linkage, reassignment, and draft-only invoice refresh without changing finalized invoice content.

#### Scenario: Receipt is applied with high-confidence fields
- **WHEN** either application mode saves a receipt with independently high-confidence merchant, total, or category suggestions
- **THEN** the corresponding saved editable fields SHALL be initialized regardless of whether overall confidence allowed proactive application

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
