## ADDED Requirements

### Requirement: Users can request receipt analysis for any visible expense attachment
The system SHALL allow an authenticated user to request analysis for any receipt attachment belonging to an expense visible to that user. The quick receipt capture flow SHALL invoke the same attachment-level analysis journey after it has saved an attachment.

#### Scenario: User analyses an existing expense receipt
- **WHEN** a user selects Analyse receipt for an attachment on a visible existing expense
- **THEN** the system SHALL analyse that attachment without requiring the attachment to have been created by quick receipt capture

#### Scenario: Quick capture hands off to attachment analysis
- **WHEN** quick receipt capture has successfully saved its receipt attachment and the user selects Analyse receipt
- **THEN** the system SHALL open the shared analysis journey for the saved gig, expense, and attachment identifiers

#### Scenario: User cannot analyse another user's attachment
- **WHEN** a user requests analysis for an attachment outside their visibility scope
- **THEN** the system SHALL not disclose or analyse the attachment

### Requirement: Receipt analysis preserves independent provenance
The system SHALL persist each receipt-analysis attempt independently from `GigExpense` and associate it with exactly one expense attachment. Each attempt SHALL retain its status, provider, model, prompt version, requested/completed timestamps, validated suggestions, confidence values, warnings, and safe failure information where applicable.

#### Scenario: Successful analysis is retained
- **WHEN** receipt analysis returns a valid response
- **THEN** the system SHALL persist a successful attachment-bound analysis attempt with its validated output and provenance

#### Scenario: Reanalysis preserves history
- **WHEN** a user analyses an attachment that already has an earlier analysis attempt
- **THEN** the system SHALL create a new attempt without overwriting the earlier attempt

#### Scenario: Failure is retained safely
- **WHEN** analysis cannot complete because of a provider, timeout, unsupported-media, missing-content, or invalid-response failure
- **THEN** the system SHALL persist a failed attempt with a safe user-facing failure message and no unvalidated suggestions

### Requirement: Receipt media is sent to Vertex AI within bounded scope
The system SHALL load the stored attachment through the attachment storage service and send only supported receipt media bytes, MIME type, and minimal extraction context to the configured Vertex AI model. The system SHALL enforce a receipt-analysis-specific file-size limit and MIME allowlist.

#### Scenario: Supported receipt is analysed inline
- **WHEN** a supported attachment is within the analysis size limit and Vertex receipt analysis is configured
- **THEN** the system SHALL send the attachment as inline multimodal content with a constrained extraction request

#### Scenario: Unsupported receipt is not sent to Vertex
- **WHEN** an attachment MIME type is unsupported for receipt analysis or its size exceeds the analysis limit
- **THEN** the system SHALL not send its content to Vertex AI and SHALL return a safe failed analysis result

#### Scenario: Vertex receipt analysis is unavailable
- **WHEN** a user requests analysis while receipt analysis is not configured or the provider is unavailable
- **THEN** the system SHALL leave the attachment and expense unchanged and SHALL report that analysis is unavailable

### Requirement: Receipt suggestions are constrained and deterministically validated
The system SHALL request JSON-only receipt suggestions for merchant, transaction date, total amount, currency, suggested category, per-field confidence, and warnings. The system SHALL validate all output before persisting or presenting it.

#### Scenario: Valid structured response is accepted
- **WHEN** Vertex returns a response with valid merchant, ISO date, invariant decimal total, ISO currency, allowed category, confidence values, and warnings
- **THEN** the system SHALL persist and present those values as suggestions

#### Scenario: Invalid individual field is isolated
- **WHEN** Vertex returns a response with one invalid field and other independently valid fields
- **THEN** the system SHALL omit the invalid field, retain the valid fields, and include a warning requiring review

#### Scenario: Malformed response is rejected
- **WHEN** Vertex returns empty, malformed, or structurally invalid output
- **THEN** the system SHALL not persist it as a successful analysis or present it as a suggestion

### Requirement: AI suggestions require explicit user application
The system SHALL visibly distinguish analysis suggestions from saved expense data and SHALL not automatically update any `GigExpense` value. The system SHALL allow the user to explicitly copy merchant and total suggestions into editable description and amount controls.

#### Scenario: User applies merchant and total suggestions
- **WHEN** a user explicitly applies valid merchant and total suggestions
- **THEN** the system SHALL populate the relevant editable expense controls without saving the expense automatically

#### Scenario: User saves applied suggestions
- **WHEN** a user saves the populated expense controls through the existing expense workflow
- **THEN** the system SHALL persist the chosen description and amount using normal expense validation and workflow rules

#### Scenario: User does not apply suggestions
- **WHEN** a user closes the analysis review without applying a suggestion
- **THEN** the system SHALL leave the saved expense values unchanged

### Requirement: Currency, category, and transaction date remain review-only
The system SHALL present suggested currency, category, and transaction date as analysis information only and SHALL not add or populate accounting-model fields for them in this change.

#### Scenario: Review-only fields are displayed
- **WHEN** a successful analysis includes currency, category, or transaction date
- **THEN** the system SHALL show those values with their confidence and any warnings as review-only suggestions

#### Scenario: Applying editable suggestions does not persist review-only fields
- **WHEN** a user applies merchant or total and saves the expense
- **THEN** the system SHALL not persist the analysis currency, category, or transaction date as `GigExpense` fields

### Requirement: Receipt analysis is privacy-conscious and observable
The system SHALL make analysis user-triggered, rate limit requests per user, apply a cancellation-aware timeout, and emit operational telemetry without sensitive receipt or extraction content. The production privacy disclosure SHALL describe optional Vertex AI receipt processing before the feature is enabled.

#### Scenario: Analysis activity is logged safely
- **WHEN** an analysis attempt completes or fails
- **THEN** operational logs SHALL contain only non-sensitive identifiers, model/configuration metadata, outcome, elapsed time, and failure classification

#### Scenario: Sensitive receipt content is excluded from logs
- **WHEN** the system logs an analysis attempt
- **THEN** it SHALL not log receipt bytes, filenames, prompts, raw model responses, merchant values, or total amounts

#### Scenario: User exceeds analysis rate limit
- **WHEN** a user exceeds the configured receipt-analysis request limit
- **THEN** the system SHALL reject the analysis request safely without changing the attachment or expense
