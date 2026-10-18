## Purpose

Provide complete inline record details in compact workspace panes while preserving readable long content and existing actions and editors.

## Requirements

### Requirement: Workspace-specific compact overviews
Clients, Gigs, Invoices, and Admin Users SHALL show standard record details directly in the selected-record pane, using the available space rather than permanent read-only detail dialogs. Layout SHALL target fitting normal records with editors closed or open, while unusually long records MAY scroll in the middle detail region. Labels and values SHALL be left-aligned in a structured facts layout; the group SHALL use the pane's middle space, with a top-anchored header and a bottom dock only for relevant collection/task controls. Empty selection and constrained viewports SHALL remain accessible.

#### Scenario: Browsing a gig with extensive associated content
- **WHEN** a user selects a gig containing long notes and many resources and expenses
- **THEN** the pane shows complete booking facts and notes inline, with scrolling available for exceptionally long notes
- **AND** resources and expenses remain separate dialogs, with their counts/totals shown in docked controls

#### Scenario: Client and Admin overviews preserve operational facts
- **WHEN** a user selects a client or Admin user
- **THEN** the pane shows complete contact/billing or role/access/enrolment/account metadata respectively
- **AND** reading a full address or account reference does not require opening a redundant read-only dialog
- **AND** existing monthly invoicing, settings, and access-editing actions remain reachable

#### Scenario: Invoice overview retains workflow essentials
- **WHEN** a user selects an invoice
- **THEN** client, status, dates, total, document readiness, full description, and issue/delivery metadata are available inline
- **AND** long values remain readable through detail-region scrolling without a separate read-only workflow dialog

### Requirement: Long standard details remain readable inline
Variable-length names, notes, descriptions, locations, addresses, and metadata SHALL remain readable in full without expanding the desktop document indefinitely. The middle detail region SHALL support scrolling for anomalous content; text SHALL NOT be silently clamped or reduced to tooltip-only access. Changing the selected record SHALL reset that region's scroll position. Compact layout SHALL NOT make critical workflow state or warnings inaccessible.

#### Scenario: Long text is fully readable on demand
- **WHEN** record text exceeds the usual amount of available detail space
- **THEN** the user can scroll to read the complete content without editing it or losing selection

#### Scenario: Workflow prerequisites need attention
- **WHEN** seller-profile configuration or document readiness prevents an invoice operation
- **THEN** the overview conveys the blocking state and provides access to the existing explanation or corrective action

### Requirement: Existing actions and editors remain authoritative
The overview SHALL preserve all existing record actions, restrictions, and confirmation behavior. Less frequent actions MAY be grouped in an accessible menu but SHALL remain discoverable and keyboard-operable. Record editing, invoice line items, adjustments, and invoice description editing SHALL continue in the existing independently scrollable editor areas.

#### Scenario: Invoice lines remain in the editor
- **WHEN** the user chooses Line items
- **THEN** the existing line-items editor opens with its current editing and gig-link navigation behavior intact
- **AND** the overview remains fitted at supported desktop sizes

#### Scenario: Compact actions retain eligibility rules
- **WHEN** an invoice is not eligible for download, delivery, or deletion, or a gig cannot be deleted
- **THEN** the corresponding action retains its existing restriction and explanation regardless of its compact placement
