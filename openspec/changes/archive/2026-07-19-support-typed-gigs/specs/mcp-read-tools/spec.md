## MODIFIED Requirements

### Requirement: MCP shall expose read-only gig listing
The system SHALL provide an authenticated read-only MCP tool named `glovelly_list_gigs` that lists gigs visible to the MCP user and supports filtering by contact, status, date range, invoicing state, and gig type.

#### Scenario: List visible gigs by date range
- **WHEN** an authenticated MCP user calls `glovelly_list_gigs` with `fromDate` and `toDate`
- **THEN** the response includes only visible gigs whose dates fall within the inclusive range, ordered predictably, with gig ID, title, date, venue, contact summary, status, type, fee, invoice state, and currency

#### Scenario: Filter visible gigs by type
- **WHEN** an authenticated MCP user calls `glovelly_list_gigs` with a supported gig type filter
- **THEN** the response includes only visible gigs whose type matches the requested type

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
- **THEN** the response has `found` set to true and includes gig details, type, contact summary, invoice summary when linked, expense summaries, resource summaries, and currency

#### Scenario: Hidden or missing gig is not returned
- **WHEN** an authenticated MCP user calls `glovelly_get_gig` with a missing gig ID or a gig ID outside their visible scope
- **THEN** the response has `found` set to false and does not include gig details

### Requirement: MCP shall expose read-only uninvoiced gig listing
The system SHALL provide an authenticated read-only MCP tool named `glovelly_list_uninvoiced_gigs` that lists visible gigs that are not linked to an invoice and supports the same gig type filter as `glovelly_list_gigs`.

#### Scenario: List uninvoiced gigs
- **WHEN** an authenticated MCP user calls `glovelly_list_uninvoiced_gigs`
- **THEN** the response includes only visible gigs without an invoice link and includes gig type, total uninvoiced fees, and currency

#### Scenario: Filter uninvoiced gigs by contact and date
- **WHEN** an authenticated MCP user calls `glovelly_list_uninvoiced_gigs` with contact and date filters
- **THEN** the response applies the filters before returning gig summaries and totals

#### Scenario: Filter uninvoiced gigs by type
- **WHEN** an authenticated MCP user calls `glovelly_list_uninvoiced_gigs` with a supported gig type filter
- **THEN** the response includes only visible uninvoiced gigs whose type matches the requested type

## ADDED Requirements

### Requirement: MCP staged gig import drafts shall include gig type
The system SHALL allow MCP staged gig import draft tools to accept, store, and return a proposed gig type while preserving staged-write behavior.

#### Scenario: Add typed gig import draft through MCP
- **WHEN** an authenticated MCP user calls `glovelly_add_gig_import_draft` with a supported gig type
- **THEN** the created draft stores and returns that type without creating a real gig

#### Scenario: Add bulk typed gig import drafts through MCP
- **WHEN** an authenticated MCP user calls `glovelly_add_gig_import_drafts` with draft rows that include supported gig types
- **THEN** each created draft stores and returns its requested type in the per-row result

#### Scenario: MCP draft type defaults
- **WHEN** an MCP staged gig import draft request omits gig type
- **THEN** the created draft stores and returns `Performance` as the proposed gig type

#### Scenario: MCP draft rejects invalid type
- **WHEN** an MCP staged gig import draft request supplies an unsupported gig type
- **THEN** the draft is not created and the result includes validation feedback for the type field
