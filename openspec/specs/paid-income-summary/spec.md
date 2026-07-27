## Purpose

Provide a user-scoped summary of income received in the current UK financial year.

## Requirements

### Requirement: Invoice payment-date recording
The system SHALL persist a nullable `PaidOn` date for each invoice. When an invoice transitions from a non-Paid status to Paid, the system SHALL set `PaidOn` to the current date in the Europe/London timezone. When reissuing an invoice changes it to Draft, the system SHALL clear `PaidOn`.

#### Scenario: Marking an invoice as paid records a UK-local payment date
- **WHEN** a visible issued or overdue invoice is transitioned to Paid
- **THEN** the invoice response SHALL contain a `PaidOn` date equal to the current Europe/London date

#### Scenario: Reissuing a paid invoice clears its payment date
- **WHEN** a paid invoice with a `PaidOn` value is reissued
- **THEN** the resulting Draft invoice SHALL have a null `PaidOn` value

### Requirement: UK financial-year paid-income summary
The system SHALL provide a user-scoped current-financial-year paid-income summary. The financial year SHALL begin on 6 April and end on 5 April inclusive, based on the current Europe/London date. The summary SHALL include the financial-year boundaries, the total of contributing invoice values, and the IDs of contributing invoices.

#### Scenario: Current financial year starts on 6 April
- **WHEN** the current Europe/London date is 6 April 2026
- **THEN** the summary period SHALL run from 6 April 2026 through 5 April 2027 inclusive

#### Scenario: Current financial year before 6 April began in the prior calendar year
- **WHEN** the current Europe/London date is 5 April 2027
- **THEN** the summary period SHALL run from 6 April 2026 through 5 April 2027 inclusive

### Requirement: Paid-income inclusion rules
The paid-income summary SHALL include an invoice only when it is visible to the authenticated user, has status Paid, has a non-null `PaidOn` date, and that date falls within the summary's inclusive financial-year range. Draft, Issued, Overdue, Cancelled, and invoices without a qualifying date SHALL not contribute.

#### Scenario: A paid invoice received in the financial year contributes
- **WHEN** a visible Paid invoice has a `PaidOn` date within the current financial year
- **THEN** its total and ID SHALL be included in the paid-income summary

#### Scenario: A paid invoice received outside the financial year is excluded
- **WHEN** a visible Paid invoice has a `PaidOn` date outside the current financial year
- **THEN** its total and ID SHALL not be included in the paid-income summary

#### Scenario: An unpaid invoice is excluded regardless of its dates
- **WHEN** a visible invoice is Draft, Issued, Overdue, or Cancelled and has dates within the current financial year
- **THEN** its total and ID SHALL not be included in the paid-income summary

### Requirement: Reconciled paid-income drill-down
The system SHALL let a user open the invoice workspace from the current-financial-year income card with a filter that displays exactly the invoice IDs supplied by the paid-income summary.

#### Scenario: Income-card drill-down reconciles with the summary
- **WHEN** a user selects the current-financial-year income card
- **THEN** the invoice workspace SHALL display only the contributing invoices from the same summary response
