## ADDED Requirements

### Requirement: MCP shall expose read-only gig listing
The system SHALL provide an authenticated read-only MCP tool named `glovelly_list_gigs` that lists gigs visible to the MCP user and supports filtering by contact, status, date range, and invoicing state.

#### Scenario: List visible gigs by date range
- **WHEN** an authenticated MCP user calls `glovelly_list_gigs` with `fromDate` and `toDate`
- **THEN** the response includes only visible gigs whose dates fall within the inclusive range, ordered predictably, with gig ID, title, date, venue, contact summary, status, fee, invoice state, and currency

#### Scenario: Ambiguous contact query does not guess
- **WHEN** an authenticated MCP user calls `glovelly_list_gigs` with a `contactQuery` that matches multiple visible contacts
- **THEN** the response is marked ambiguous, includes the matching contacts, and does not return guessed gig results

#### Scenario: Unmatched contact query returns no gigs
- **WHEN** an authenticated MCP user calls `glovelly_list_gigs` with a `contactQuery` that matches no visible contacts
- **THEN** the response is not ambiguous and contains an empty gig list

### Requirement: MCP shall expose read-only gig detail
The system SHALL provide an authenticated read-only MCP tool named `glovelly_get_gig` that fetches details for one visible gig without modifying the gig or related records.

#### Scenario: Fetch visible gig detail
- **WHEN** an authenticated MCP user calls `glovelly_get_gig` with the ID of a visible gig
- **THEN** the response has `found` set to true and includes gig details, contact summary, invoice summary when linked, expense summaries, resource summaries, and currency

#### Scenario: Hidden or missing gig is not returned
- **WHEN** an authenticated MCP user calls `glovelly_get_gig` with a missing gig ID or a gig ID outside their visible scope
- **THEN** the response has `found` set to false and does not include gig details

### Requirement: MCP shall expose read-only uninvoiced gig listing
The system SHALL provide an authenticated read-only MCP tool named `glovelly_list_uninvoiced_gigs` that lists visible gigs that are not linked to an invoice.

#### Scenario: List uninvoiced gigs
- **WHEN** an authenticated MCP user calls `glovelly_list_uninvoiced_gigs`
- **THEN** the response includes only visible gigs without an invoice link and includes total uninvoiced fees and currency

#### Scenario: Filter uninvoiced gigs by contact and date
- **WHEN** an authenticated MCP user calls `glovelly_list_uninvoiced_gigs` with contact and date filters
- **THEN** the response applies the filters before returning gig summaries and totals

### Requirement: MCP shall expose read-only contact detail
The system SHALL provide an authenticated read-only MCP tool named `glovelly_get_contact` that fetches details for one visible contact.

#### Scenario: Fetch visible contact detail
- **WHEN** an authenticated MCP user calls `glovelly_get_contact` with the ID of a visible contact
- **THEN** the response has `found` set to true and includes contact ID, name, email, billing address, mileage settings, invoice filename and subject patterns, and summary counts for related visible gigs and invoices

#### Scenario: Hidden or missing contact is not returned
- **WHEN** an authenticated MCP user calls `glovelly_get_contact` with a missing contact ID or a contact ID outside their visible scope
- **THEN** the response has `found` set to false and does not include contact details

### Requirement: MCP shall expose read-only gig resources
The system SHALL provide an authenticated read-only MCP tool named `glovelly_list_gig_resources` that lists metadata for resources attached to a visible gig.

#### Scenario: List resource metadata for visible gig
- **WHEN** an authenticated MCP user calls `glovelly_list_gig_resources` with the ID of a visible gig
- **THEN** the response has `found` set to true and includes each resource's ID, type, purpose, title, URL when present, notes, primary flag, timestamps, and attachment metadata

#### Scenario: Resource listing does not return file contents
- **WHEN** an authenticated MCP user calls `glovelly_list_gig_resources`
- **THEN** the response includes attachment metadata only and does not include attachment bytes or generated file downloads

#### Scenario: Hidden or missing gig resources are not returned
- **WHEN** an authenticated MCP user calls `glovelly_list_gig_resources` with a missing gig ID or a gig ID outside their visible scope
- **THEN** the response has `found` set to false and returns no resources

### Requirement: MCP shall expose read-only active gig setlist
The system SHALL provide an authenticated read-only MCP tool named `glovelly_get_gig_setlist` that returns the active setlist import already stored for a visible gig.

#### Scenario: Fetch active setlist
- **WHEN** an authenticated MCP user calls `glovelly_get_gig_setlist` with the ID of a visible gig that has an active setlist import
- **THEN** the response has `found` set to true and includes import metadata plus ordered setlist items with section, pad number, key, title, kind, include flag, notes, and confidence

#### Scenario: No active setlist is represented clearly
- **WHEN** an authenticated MCP user calls `glovelly_get_gig_setlist` with the ID of a visible gig that has no active setlist import
- **THEN** the response has `found` set to true, `hasActiveSetlist` set to false, and no setlist items

#### Scenario: Setlist tool does not fetch Google Sheets
- **WHEN** an authenticated MCP user calls `glovelly_get_gig_setlist`
- **THEN** the system reads only Glovelly's stored setlist import data and does not call Google Sheets or other external APIs

### Requirement: MCP shall expose read-only expense statement preview
The system SHALL provide an authenticated read-only MCP tool named `glovelly_preview_expense_statement` that returns a structured expense statement projection for visible client, gig, and expense records.

#### Scenario: Preview expense statement
- **WHEN** an authenticated MCP user calls `glovelly_preview_expense_statement` with a visible contact ID and optional gig or expense IDs
- **THEN** the response includes the statement date, contact summary, included gigs, included expenses, total, expense count, receipt attachment count, and currency-equivalent money values without generating a PDF

#### Scenario: Invalid statement request returns validation feedback
- **WHEN** an authenticated MCP user calls `glovelly_preview_expense_statement` with missing, hidden, or inconsistent client, gig, or expense IDs
- **THEN** the response indicates validation errors rather than returning hidden data or throwing an unstructured MCP failure

#### Scenario: Preview has no delivery side effects
- **WHEN** an authenticated MCP user calls `glovelly_preview_expense_statement`
- **THEN** the system does not generate a PDF, send email, publish to Google Drive, mutate reimbursement status, or write workspace events

### Requirement: MCP read tools shall preserve user scope and documentation contracts
The system SHALL register all new tools in the MCP tool catalog with input and output schemas, read-only safety metadata, public documentation, and tests consistent with existing MCP conventions.

#### Scenario: Tool catalog lists new read-only tools
- **WHEN** an authenticated MCP user calls `tools/list`
- **THEN** the new tools are present with read-only safety metadata and agent-facing schemas

#### Scenario: New tools preserve visibility boundaries
- **WHEN** an authenticated MCP user calls any new read-only tool
- **THEN** all returned clients, gigs, invoices, expenses, resources, setlists, and statement data are scoped to records visible to that user

#### Scenario: Contract artifacts stay in sync
- **WHEN** the MCP catalog changes for these tools
- **THEN** the checked-in MCP tool snapshot, generated MCP documentation, and MCP capability manifest are updated consistently
