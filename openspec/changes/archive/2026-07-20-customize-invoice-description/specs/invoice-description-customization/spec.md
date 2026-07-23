## ADDED Requirements

### Requirement: Draft invoice description can be updated independently
The system SHALL provide an authenticated, owner-scoped API operation that updates only the document-level description of a Draft invoice. The operation MUST trim the submitted value, reject blank descriptions, persist the updated description, and return the updated invoice including its line items.

#### Scenario: Draft description is saved
- **WHEN** an authenticated user submits a non-empty description for an invoice they can access that is in Draft status
- **THEN** the system persists the trimmed description and returns the updated invoice with its line items

#### Scenario: Blank description is rejected
- **WHEN** an authenticated user submits a description containing only whitespace
- **THEN** the system returns a validation error and does not alter the invoice description

#### Scenario: Non-draft description update is rejected
- **WHEN** an authenticated user attempts to update the description of an invoice whose status is not Draft
- **THEN** the system returns a validation error and does not alter the invoice description

#### Scenario: Inaccessible invoice cannot be updated
- **WHEN** an authenticated user submits a description update for an invoice outside their visibility scope
- **THEN** the system does not disclose or alter that invoice

### Requirement: Line items pane supports draft description editing
The Line items pane SHALL show the selected invoice's document-level description in an editable labelled field when the invoice is Draft. The user MUST be able to save the field through the dedicated description operation, and the UI MUST replace the selected invoice with the returned invoice on success.

#### Scenario: User saves a draft description
- **WHEN** a user edits the Description field in the Line items pane for a Draft invoice and saves it
- **THEN** the UI sends only the description update, displays the saved description, and reports a successful save

#### Scenario: Non-draft description is read-only
- **WHEN** a user opens the Line items pane for an invoice that is not Draft
- **THEN** the document-level description is shown without an editable field or save action

### Requirement: Regenerated draft PDF retains custom description
The system SHALL render a Draft invoice's persisted document-level description when redrafting its PDF.

#### Scenario: Redraft after description customization
- **WHEN** a user saves a custom description on a Draft invoice and redrafts that invoice
- **THEN** the regenerated PDF uses the saved custom description
