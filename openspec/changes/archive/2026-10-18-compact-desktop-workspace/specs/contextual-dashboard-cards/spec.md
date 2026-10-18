## ADDED Requirements

### Requirement: Persistent compact summary strip
The authenticated application SHALL display its dashboard summaries in a compact shared strip across all authenticated views without introducing a separate overview screen. Clients, Gigs, and Invoices SHALL retain their existing three context-specific summaries, calculations, refresh behavior, and distinct loading, empty, and error states. Admin SHALL display Users with access, Active accounts, and Administrators derived from the complete loaded user directory, independent of directory search and unrelated workspace data. Admin summaries SHALL distinguish loading and unavailable data from successful zero counts. Summary cards SHALL be informational without action buttons. The strip SHALL remain present when views change and SHALL wrap responsively without hiding summary information; it is not required to remain sticky when a naturally scrolling page is scrolled.

#### Scenario: Main record sections retain contextual summaries
- **WHEN** a user switches between Clients, Gigs, and Invoices
- **THEN** the compact strip displays that workspace's three existing contextual summaries without action buttons

#### Scenario: Admin displays its own summaries
- **WHEN** a user opens Admin after visiting another workspace
- **THEN** the strip displays total users, active accounts, and administrators from the user directory rather than the previous workspace's information
- **AND** searching the directory does not change those totals

#### Scenario: Admin directory is unavailable
- **WHEN** the user directory is loading or fails to load
- **THEN** the Admin strip displays loading or unavailable states rather than zero counts or unrelated metrics

#### Scenario: Compact presentation preserves unavailable states
- **WHEN** a summary is loading or its data request fails
- **THEN** the strip distinguishes that state from a successful zero or empty result

#### Scenario: Narrow presentation retains summary information
- **WHEN** the summary strip is displayed on a narrow viewport
- **THEN** its summaries can wrap without losing their labels, values, or states

## MODIFIED Requirements

### Requirement: Contextual-card refresh and actionability
The system SHALL refresh contextual card values when the underlying workspace data changes. Summary cards SHALL be informational, without embedded navigation or filter action buttons. Invoice filtering SHALL remain available through the invoice workspace's existing filter chips.

#### Scenario: Invoice payment updates current-financial-year income
- **WHEN** an invoice is marked Paid and its `PaidOn` date falls in the current financial year
- **THEN** the Invoices dashboard income card SHALL refresh to include that invoice

#### Scenario: Invoice filtering remains available without summary actions
- **WHEN** a user selects the Outstanding, Overdue, or Income this financial year filter chip in Invoices
- **THEN** the invoice workspace SHALL display the corresponding filtered records without requiring a summary action
