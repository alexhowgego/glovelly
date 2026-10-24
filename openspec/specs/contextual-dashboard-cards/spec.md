## Purpose

Provide dashboard cards tailored to the active workspace.

## Requirements

### Requirement: Context-specific dashboard cards
The system SHALL render exactly three dashboard cards for the active Clients, Gigs, or Invoices workspace. Each workspace SHALL display a context-relevant card set while retaining the shared card layout.

#### Scenario: Selecting the Gigs workspace updates the dashboard cards
- **WHEN** the user selects the Gigs workspace
- **THEN** the dashboard SHALL show cards for upcoming gigs, gigs requiring confirmation or still in Draft, and completed uninvoiced gigs

#### Scenario: Selecting the Invoices workspace updates the dashboard cards
- **WHEN** the user selects the Invoices workspace
- **THEN** the dashboard SHALL show cards for outstanding balance, overdue invoices, and income received in the current financial year

#### Scenario: Selecting the Clients workspace updates the dashboard cards
- **WHEN** the user selects the Clients workspace
- **THEN** the dashboard SHALL show cards for active clients, clients with outstanding invoices, and the most frequently used client

### Requirement: Contextual-card refresh and actionability
The system SHALL refresh contextual card values when the underlying workspace data changes. Summary cards SHALL be informational, without embedded navigation or filter action buttons. Invoice filtering SHALL remain available through the invoice workspace's existing filter chips.

#### Scenario: Invoice payment updates current-financial-year income
- **WHEN** an invoice is marked Paid and its `PaidOn` date falls in the current financial year
- **THEN** the Invoices dashboard income card SHALL refresh to include that invoice

#### Scenario: Invoice filtering remains available without summary actions
- **WHEN** a user selects the Outstanding, Overdue, or Income this financial year filter chip in Invoices
- **THEN** the invoice workspace SHALL display the corresponding filtered records without requiring a summary action

### Requirement: Dashboard card states and responsive layout
The system SHALL distinguish loading, zero-or-empty, and error states for dashboard cards. The three-card dashboard layout SHALL remain usable at mobile and desktop viewport widths without changing the number of card slots by context.

#### Scenario: Income summary loading does not appear as zero income
- **WHEN** the Invoices workspace is selected and the paid-income summary is still loading
- **THEN** the income card SHALL show a loading state rather than a zero monetary value

#### Scenario: No qualifying income is distinct from an unavailable summary
- **WHEN** the paid-income summary succeeds with no contributing invoices
- **THEN** the income card SHALL show a zero monetary value and the financial-year period

#### Scenario: Income summary failure is visible
- **WHEN** the paid-income summary request fails
- **THEN** the income card SHALL show an error or unavailable state rather than a zero monetary value

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
