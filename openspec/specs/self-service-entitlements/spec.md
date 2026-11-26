## Purpose

Expose current product access and usage with actionable, accurate allowance and restricted-reporting messaging.

## Requirements

### Requirement: Users can inspect effective access and current usage
The system SHALL expose a user-scoped local entitlement snapshot with effective/assigned plan, expiry/fallback state, feature availability, usage, nullable unlimited limit, nonnegative remaining allowance, and Europe/London month/reset instants. It SHALL NOT expose other users' records, administrative notes, or provider secrets.

#### Scenario: Free account summary
- **WHEN** a Free user inspects their access with one first-issued invoice this month
- **THEN** the snapshot SHALL show used one, limit two, remaining one, and the next UK-calendar reset

#### Scenario: Unlimited plan summary
- **WHEN** Solo or Pro inspects access
- **THEN** the snapshot SHALL identify unlimited issuance without inventing a purchased or finite credit balance

#### Scenario: Paid grant has expired
- **WHEN** a user inspects access after their grant ends
- **THEN** it SHALL distinguish the expired assignment from effective Free and preserve current usage

### Requirement: Existing confirmations explain actual allowance impact
The existing single issuance dialog and email/Drive review SHALL explain one-unit consumption only for genuinely first-issued metered invoices. Subsequent issue/correction SHALL explain that no additional unit is used. No additional confirmation, checkout, or unsupported payment link SHALL be introduced.

#### Scenario: Metered first issue
- **WHEN** a Free user confirms a new first issue
- **THEN** the existing confirmation SHALL explain the allowance impact using current usage/reset information

#### Scenario: Correction at exhausted allowance
- **WHEN** an already-issued invoice is returned to Draft for correction and issued again
- **THEN** the existing confirmation SHALL not describe another charge or block the correction solely because monthly allowance is exhausted

#### Scenario: Grant changes while review is open
- **WHEN** plan access changes or expires before the user confirms issuance
- **THEN** the authoritative backend decision SHALL prevail and the still-open UI SHALL display actionable refreshed entitlement feedback without attempting delivery on failure

### Requirement: Restricted reporting is not an error or zero balance
Free reporting restrictions SHALL have a distinct, compact Solo/Pro-required state in dashboard/drill-down UI rather than zero income, health failure, or repeated error notifications. The system SHALL NOT calculate the denied aggregate client-side as a fallback. Raw record access SHALL remain available.

#### Scenario: Free dashboard loads
- **WHEN** the paid-income card is rendered for Free
- **THEN** it SHALL explain the plan requirement without displaying a fabricated zero total or loading indefinitely

#### Scenario: Restricted summary request
- **WHEN** a user lacking PaidIncomeSummary calls a protected reporting surface
- **THEN** it SHALL return `feature_not_entitled` rather than an authentication-expired response or paid aggregate

### Requirement: Plan-change messaging reflects manual operation
Limit-reached and reporting-restricted states SHALL explain reset or licence-administrator plan-change options without pretending that paid checkout exists. Successful issuance and manual assignment SHALL refresh entitlement state; expiry/revocation SHALL be observed on protected requests and relevant workspace refresh.

#### Scenario: Allowance exhausted
- **WHEN** Free first issuance is denied
- **THEN** the UI SHALL show the reset time and manual plan-change guidance while retaining the user's draft and existing modal context
