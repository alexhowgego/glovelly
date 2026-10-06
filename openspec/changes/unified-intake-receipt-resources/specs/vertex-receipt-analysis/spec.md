## MODIFIED Requirements

### Requirement: Users can request receipt analysis for any visible expense attachment
The system SHALL allow an authenticated user to request analysis for any receipt attachment belonging to an expense visible to that user. The unified intake journey SHALL initiate the same validated receipt-analysis capability immediately after accepting a source, before an expense attachment exists.

#### Scenario: User analyses an existing expense receipt
- **WHEN** a user selects Analyse receipt for an attachment on a visible existing expense
- **THEN** the system SHALL analyse that attachment without requiring the attachment to have been created by unified intake

#### Scenario: Unified intake initiates analysis before application
- **WHEN** a user submits a source through Add to Glovelly
- **THEN** the system SHALL initiate receipt-capable analysis for the retained intake source before creating an expense or attachment relationship

#### Scenario: User cannot analyse another user's source
- **WHEN** a user requests analysis for an intake source or attachment outside their visibility scope
- **THEN** the system SHALL not disclose or analyse that source

### Requirement: Receipt analysis preserves independent provenance
The system SHALL persist each receipt-analysis attempt independently from `GigExpense` and associate it with exactly one expense attachment or current intake. Each attempt SHALL retain its status, provider, model, prompt version, requested/completed timestamps, validated suggestions, confidence values, warnings, and safe failure information where applicable.

#### Scenario: Successful intake analysis is retained
- **WHEN** receipt-capable analysis returns a valid response for a current intake
- **THEN** the system SHALL persist a successful intake-bound analysis attempt with its validated output and provenance

#### Scenario: Reanalysis preserves history
- **WHEN** a user retries analysis for an intake source or attachment that already has an earlier analysis attempt
- **THEN** the system SHALL create a new attempt without overwriting the earlier attempt

#### Scenario: Failure is retained safely
- **WHEN** analysis cannot complete because of a provider, timeout, unsupported-media, missing-content, or invalid-response failure
- **THEN** the system SHALL persist a failed attempt with a safe user-facing failure message and no unvalidated suggestions

### Requirement: AI suggestions require explicit user application
The system SHALL visibly distinguish analysis suggestions from saved expense data and SHALL allow explicit copying into editable expense controls. Existing-attachment analysis SHALL NOT save expense values merely by running analysis. Unified receipt application, whether proactive under the user's preference or explicitly confirmed, SHALL initialize only independently validated high-confidence merchant, total, and category fields. Application SHALL remain editable, reassignable, and deletable, with attached state in the open unified upload dialog and optional saved-attachment review.

#### Scenario: User applies merchant and total suggestions
- **WHEN** a user explicitly applies valid merchant and total suggestions
- **THEN** the system SHALL populate the relevant editable expense controls without saving the expense automatically

#### Scenario: Intake receipt is attached and populated
- **WHEN** automatic or explicit unified receipt application succeeds
- **THEN** the system SHALL create an editable receipt with its original source and independently high-confidence merchant, total, and category values and show attached state without automatically opening review

#### Scenario: User saves applied suggestions
- **WHEN** a user saves populated expense controls in saved-attachment review
- **THEN** the system SHALL persist the chosen values using normal validation and draft-only invoice refresh rules

#### Scenario: User does not apply suggestions
- **WHEN** a user closes the analysis review without applying a suggestion
- **THEN** the system SHALL leave the saved expense values unchanged

### Requirement: Currency, category, and transaction date remain review-only
The system SHALL present currency and transaction date as review-only evidence without adding expense accounting fields for them. Category SHALL be an optional editable expense field constrained to Travel, Meals, Accommodation, Equipment, or Other. Unified receipt application SHALL initialize category only from independently validated high-confidence evidence; existing-attachment analysis SHALL allow explicit category application alongside merchant and total.

#### Scenario: Review-only fields are displayed
- **WHEN** analysis includes currency or transaction date
- **THEN** the system SHALL display them as evidence without persisting new expense fields

#### Scenario: Category is applied explicitly
- **WHEN** a user applies a valid category suggestion from existing-attachment analysis and saves
- **THEN** the system SHALL persist the optional category through ordinary expense validation without changing invoice wording or reimbursement behavior

#### Scenario: Receipt has low-confidence category evidence
- **WHEN** a unified receipt is applied but category evidence is below High confidence
- **THEN** the system SHALL leave category uninitialized and preserve the suggestion for optional review

### Requirement: Receipt analysis is privacy-conscious and observable
The system SHALL make attachment analysis user-triggered and intake analysis the direct result of a user's source submission, rate limit requests per user, apply a cancellation-aware timeout, and emit operational telemetry without sensitive receipt or extraction content. The production privacy disclosure SHALL describe Vertex AI receipt processing before the feature is enabled.

#### Scenario: Analysis activity is logged safely
- **WHEN** an analysis attempt completes or fails
- **THEN** operational logs SHALL contain only non-sensitive identifiers, model/configuration metadata, outcome, elapsed time, and failure classification

#### Scenario: Sensitive receipt content is excluded from logs
- **WHEN** the system logs an analysis attempt
- **THEN** it SHALL not log receipt bytes, filenames, prompts, raw model responses, merchant values, or total amounts

#### Scenario: User exceeds analysis rate limit
- **WHEN** a user exceeds the configured receipt-analysis request limit
- **THEN** the system SHALL reject the analysis request safely without changing an attachment, expense, or applied resource
