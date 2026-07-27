## ADDED Requirements

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
- **THEN** the dashboard SHALL show cards for active clients, clients with outstanding invoices, and recently added clients

### Requirement: Contextual-card refresh and actionability
The system SHALL refresh contextual card values when the underlying workspace data changes. A card that represents a filtered record set SHALL provide navigation to its relevant workspace and filter where that navigation is useful.

#### Scenario: Invoice payment updates current-financial-year income
- **WHEN** an invoice is marked Paid and its `PaidOn` date falls in the current financial year
- **THEN** the Invoices dashboard income card SHALL refresh to include that invoice

#### Scenario: Invoice card navigates to its relevant records
- **WHEN** a user selects a filterable Invoices dashboard card
- **THEN** the application SHALL open the invoice workspace with the card's relevant invoice filter applied

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
