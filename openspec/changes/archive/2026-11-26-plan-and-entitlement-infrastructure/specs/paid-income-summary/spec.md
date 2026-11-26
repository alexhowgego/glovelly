## MODIFIED Requirements

### Requirement: UK financial-year paid-income summary
The system SHALL provide a user-scoped current-financial-year paid-income summary only when the user's effective local entitlements grant PaidIncomeSummary. The financial year SHALL begin on 6 April and end on 5 April inclusive, based on the current Europe/London date. The summary SHALL include the financial-year boundaries, the total of contributing invoice values, and the IDs of contributing invoices. A missing feature grant SHALL produce a structured feature restriction without returning the aggregate.

#### Scenario: Current financial year starts on 6 April
- **WHEN** an entitled user's current Europe/London date is 6 April 2026
- **THEN** the summary period SHALL run from 6 April 2026 through 5 April 2027 inclusive

#### Scenario: Current financial year before 6 April began in the prior calendar year
- **WHEN** an entitled user's current Europe/London date is 5 April 2027
- **THEN** the summary period SHALL run from 6 April 2026 through 5 April 2027 inclusive

#### Scenario: Free user requests the summary
- **WHEN** a user's effective plan lacks PaidIncomeSummary
- **THEN** the endpoint SHALL return a feature-not-entitled response rather than totals, contributing IDs, zero-income success, or an authentication-expired response

### Requirement: Reconciled paid-income drill-down
For users with PaidIncomeSummary, the system SHALL let a user open the invoice workspace from the current-financial-year income card with a filter that displays exactly the invoice IDs supplied by the paid-income summary. For users without that feature, the card/drill-down SHALL show a plan restriction and SHALL NOT reconstruct the denied aggregate/filter from raw records as a frontend fallback. Ordinary invoice visibility and paid-status filters SHALL remain available.

#### Scenario: Income-card drill-down reconciles with the summary
- **WHEN** an entitled user selects the current-financial-year income card
- **THEN** the invoice workspace SHALL display only the contributing invoices from the same summary response

#### Scenario: Entitlement is lost after loading a summary
- **WHEN** a user's paid reporting grant expires or is revoked and the workspace refreshes access
- **THEN** the restricted drill-down SHALL no longer expose retained summary results as current
- **AND** ordinary paid-invoice/history access SHALL remain available
