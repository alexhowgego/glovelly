# UAT Browser Automation Specification

## Purpose
Define the browser-level UAT automation coverage for high-value authenticated Glovelly workflows that complement manual UAT and backend integration tests.

## Requirements

### Requirement: Cross-workspace browser navigation is automated
The UAT browser suite SHALL verify that cross-workspace shortcuts open the intended workspace and selected record without stale filters hiding the target, and SHALL verify that opening a gig from a generated invoice line hydrates an open gig editor from that target gig rather than a previously edited gig.

#### Scenario: User follows gig and invoice client shortcuts
- **WHEN** an authenticated UAT browser test opens a gig or invoice with a known client and activates the client shortcut
- **THEN** the Clients workspace SHALL open with that client selected and visible even if previous search filters would otherwise hide it

#### Scenario: User follows generated invoice line shortcuts
- **WHEN** an authenticated UAT browser test opens generated invoice lines linked to gigs and activates a line shortcut
- **THEN** the Gigs workspace SHALL open with the corresponding gig selected and visible

#### Scenario: Invoice-line shortcut replaces an open saved gig editor with the target gig
- **WHEN** an authenticated UAT browser test saves an invoice-relevant edit to one gig, regenerates its linked draft invoice, and follows a generated invoice line shortcut to a different linked gig without refreshing the browser
- **THEN** the second gig SHALL be selected and its editor fields and expenses SHALL reflect the second gig's persisted values rather than values from the first gig
- **AND THEN** saving an intended change to the second gig SHALL NOT alter the first gig or replace the second gig's unrelated fields and expenses with values from the first gig

#### Scenario: Manual adjustment lines are not gig shortcuts
- **WHEN** an authenticated UAT browser test opens invoice lines containing manual adjustments
- **THEN** manual adjustment lines SHALL NOT expose a gig navigation shortcut

### Requirement: Dirty editor guard paths are automated
The UAT browser suite SHALL verify that unsaved edits in high-value editors are not discarded without explicit user confirmation.

#### Scenario: User declines client editor navigation discard
- **WHEN** an authenticated UAT browser test edits a client field, attempts to select another client, and declines the discard prompt
- **THEN** the original client SHALL remain selected and the unsaved edit SHALL remain visible

#### Scenario: User accepts client editor navigation discard
- **WHEN** an authenticated UAT browser test edits a client field, attempts to select another client, and accepts the discard prompt
- **THEN** the other client SHALL become selected and the discarded edit SHALL NOT be applied to the original client

#### Scenario: User declines gig editor navigation discard
- **WHEN** an authenticated UAT browser test edits a gig field or unsaved gig expense draft, attempts to select another gig, and declines the discard prompt
- **THEN** the original gig SHALL remain selected and the unsaved edit SHALL remain visible

#### Scenario: User accepts gig editor navigation discard
- **WHEN** an authenticated UAT browser test edits a gig field or unsaved gig expense draft, attempts to select another gig, and accepts the discard prompt
- **THEN** the other gig SHALL become selected and the discarded edit SHALL NOT be applied to the original gig

### Requirement: Imported gig review modal flows are automated
The UAT browser suite SHALL verify deterministic imported-gig review behavior through the browser modal when staged import data can be created safely for the authenticated test user.

#### Scenario: User opens imported gigs from profile notification
- **WHEN** staged imported gig rows exist for the authenticated UAT user
- **THEN** the profile menu SHALL show imported-gig notification state and open the Imported gigs review modal from the profile menu

#### Scenario: User edits imported gig rows with autosave
- **WHEN** an authenticated UAT browser test edits a staged imported gig row and leaves the row without pressing a row-level save button
- **THEN** the edit SHALL persist after the modal or batch is reopened

#### Scenario: User accepts rejects and commits imported gig decisions
- **WHEN** an authenticated UAT browser test accepts at least one valid imported row, rejects at least one row, and commits decisions
- **THEN** accepted rows SHALL become real gigs, rejected rows SHALL be removed from the batch, and pending rows SHALL remain available for a later pass

### Requirement: Browser upload and quick capture journeys are automated
The UAT browser suite SHALL verify representative receipt and gig attachment upload/download/delete journeys through the browser.

#### Scenario: User manages an expense receipt in the browser
- **WHEN** an authenticated UAT browser test uploads a small receipt file to a gig expense, downloads it, and deletes it
- **THEN** receipt metadata SHALL appear and disappear in the browser as expected, the download SHALL be non-empty, and the expense reimbursement state SHALL remain unchanged

#### Scenario: User manages a file-only gig attachment in the browser
- **WHEN** an authenticated UAT browser test creates a file-only gig attachment, uploads a small file, downloads it, and deletes the uploaded file
- **THEN** the attachment SHALL remain scoped to the selected gig, the download SHALL be non-empty, and deleting the file SHALL NOT delete the attachment shell

#### Scenario: User quick-adds a gig attachment in a mobile-sized viewport
- **WHEN** an authenticated UAT browser test uses the floating quick attachment action in a mobile-sized viewport
- **THEN** the quick attachment modal SHALL allow the matched gig to be reviewed or changed and the saved attachment SHALL appear on the target gig

### Requirement: Expense statement variants are automated
The UAT browser suite SHALL extend expense statement coverage beyond the main preview/download path to include deterministic selection and projection rules.

#### Scenario: Reimbursed expenses are excluded by default
- **WHEN** an authenticated UAT browser test opens an expense statement for gigs containing claimable and reimbursed expenses
- **THEN** reimbursed expenses SHALL be visually distinct and excluded from the statement total by default

#### Scenario: User includes reimbursed expenses in statement preview
- **WHEN** an authenticated UAT browser test selects a reimbursed expense in the expense statement modal and previews or downloads the statement
- **THEN** the selected reimbursed expense SHALL be included in the generated statement output

#### Scenario: Mixed-client expense statement selection is blocked
- **WHEN** an authenticated UAT browser test tries to select gigs from different clients for an expense statement
- **THEN** the browser SHALL prevent the mixed-client selection or generation path and explain the constraint

#### Scenario: Expense statements do not mutate invoice linkage
- **WHEN** an authenticated UAT browser test generates or previews an expense statement for an invoiced gig
- **THEN** existing invoice links SHALL remain unchanged and no invoice SHALL be created by the expense statement workflow

### Requirement: Selected invoice prompt choices are automated
The UAT browser suite SHALL verify representative browser prompt choices for invoice and linked-gig state transitions.

#### Scenario: User accepts linked gig completion after issuing invoice
- **WHEN** an authenticated UAT browser test issues a draft invoice linked to a non-cancelled gig and accepts the linked-gig completion prompt
- **THEN** the invoice SHALL become issued and the linked gig SHALL become completed

#### Scenario: User declines linked gig completion after issuing invoice
- **WHEN** an authenticated UAT browser test issues a draft invoice linked to a non-cancelled gig and declines the linked-gig completion prompt
- **THEN** the invoice SHALL become issued and the linked gig status SHALL remain unchanged

#### Scenario: User declines linked draft regeneration after invoice-relevant gig edit
- **WHEN** an authenticated UAT browser test edits an invoice-relevant field on a gig linked to a draft invoice and declines regeneration
- **THEN** the gig edit SHALL persist and the existing draft invoice lines SHALL remain unchanged

### Requirement: UAT documentation reflects browser automation coverage
The project SHALL keep manual UAT documentation aligned with browser-level UAT coverage added by this change.

#### Scenario: Automation status is updated for newly covered journeys
- **WHEN** a Playwright UAT test is added or expanded for a documented UAT journey
- **THEN** the nearest `docs/uat` automation status line SHALL name the test and accurately describe any remaining manual variants

#### Scenario: External service checks remain marked manual when not automated
- **WHEN** a documented UAT journey still requires real OAuth, external service configuration, asynchronous worker verification, real delivery, or human visual judgement
- **THEN** the documentation SHALL continue to mark that portion as manual or environment/manual UAT
