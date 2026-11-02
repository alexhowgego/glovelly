## Purpose

Define staged gig draft preparation from intake sources.

## Requirements

### Requirement: Date interpretation uses an explicit reference date
The system SHALL supply the current Europe/London date to gig extraction. It SHALL retain explicit source years, flag inferred dates for review, and reject an unsupported distant year guess when the source text specifies neither a year nor a relative year. Ambiguous next-weekday language SHALL carry a visible warning.

#### Scenario: Yearless future gig
- **WHEN** an offer contains a day and month without a year
- **THEN** extraction SHALL use the reference date to propose the next occurrence and SHALL warn that the date needs confirmation

#### Scenario: Distant year is invented
- **WHEN** a yearless text offer produces a date more than one year after the reference date
- **THEN** the system SHALL reject the invalid extraction and retain the source for retry or manual review

#### Scenario: Explicit past year
- **WHEN** a gig offer explicitly cites a past year
- **THEN** the system SHALL preserve that year rather than silently replacing it with a future year

### Requirement: Gig information produces staged import drafts only
The system SHALL support extracting one or more proposed gigs from pasted text/email, uploaded PDFs, and authorised Google Docs content. Gig intake SHALL prepare existing editable import batches and SHALL NOT create live gigs, clients, expenses, gig attachments, or set lists. Live gig creation SHALL remain behind existing explicit import commit safeguards.

#### Scenario: Booking email contains one gig
- **WHEN** a user confirms preparation from a valid single-gig extraction
- **THEN** the system SHALL prepare a batch with one editable draft and no live gig

#### Scenario: Tour bible contains multiple dates
- **WHEN** a user confirms preparation from a valid multi-gig extraction
- **THEN** the system SHALL prepare one batch containing the proposed draft rows with source references and warnings

### Requirement: Extraction is validated and uncertainty is reviewable
The system SHALL validate structured extraction before persistence and present confidence, source evidence, missing values, conflicts, and duplicate warnings for review. It SHALL NOT fabricate missing facts or accept malformed results as validated proposals.

#### Scenario: Valid but low-confidence result
- **WHEN** extraction is structurally valid but a date or fee is uncertain
- **THEN** the draft SHALL expose uncertainty and remain editable without silently supplying an invented value

#### Scenario: Provider returns malformed output
- **WHEN** extraction fails schema or domain validation
- **THEN** the system SHALL retain the source, reject the invalid proposals, and offer retry or manual draft recovery

#### Scenario: Source section citation cannot be verified
- **WHEN** one or more structurally valid draft rows cite missing or unknown source sections
- **THEN** the system SHALL retain those rows as low-confidence drafts with an explicit unverified-source warning and SHALL NOT present the invalid citation as verified evidence
- **AND** other valid rows SHALL remain available for review

#### Scenario: Text citation identifies a source section
- **WHEN** a draft cites a server-provided section identifier
- **THEN** the system SHALL resolve it against the retained source snapshot and display the corresponding source excerpt

#### Scenario: PDF page citation is not independently verified
- **WHEN** a draft cites a syntactically valid PDF page
- **THEN** the system SHALL label the reference as not independently verified and prompt the user to inspect the source

#### Scenario: No gigs are found
- **WHEN** validated analysis finds no gig proposals
- **THEN** the system SHALL explain that result and offer override or manual recovery rather than report a successful extracted batch

### Requirement: Draft preparation retains durable source provenance
The system SHALL preserve original text/PDF or a bounded acquired Google Doc snapshot and its original URL with user-owned batch provenance before cleaning transient intake state. Review SHALL allow authenticated inspection of retained source and evidence without attaching the source to a live gig.

#### Scenario: Remote document changes after preparation
- **WHEN** a user reviews a prepared batch after its Google Doc changes
- **THEN** review SHALL retain the source snapshot used for the proposals and the original document link

### Requirement: Preparation and recovery are idempotent and user scoped
The system SHALL identify prepared outcomes by user and intake identity, prevent duplicate batch creation, and recover successful preparation after response loss. Replaced, discarded, or superseded analysis SHALL NOT prepare a batch from stale results.

#### Scenario: Response is interrupted
- **WHEN** preparation succeeds but the response is lost and the user retries
- **THEN** the system SHALL return the original batch rather than create another batch

#### Scenario: Another user requests the result
- **WHEN** a user requests another user's intake, analysis job, batch provenance, or preparation outcome
- **THEN** the system SHALL deny access without exposing the private source

### Requirement: AI failure retains a usable manual path
The system SHALL preserve source input during loading and recoverable failure and allow manual preparation/editing of uncommitted drafts without a functioning AI provider.

#### Scenario: AI provider is unavailable
- **WHEN** gig analysis cannot run because the provider is unavailable
- **THEN** the user SHALL retain the source and be able to retry or prepare an editable manual draft without creating live business records

#### Scenario: PDF cannot be read
- **WHEN** a scan is unreadable or exceeds supported limits
- **THEN** the system SHALL explain the limitation and preserve retry, replacement, or manual recovery rather than silently succeed with empty results
