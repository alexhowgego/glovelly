## ADDED Requirements

### Requirement: Plans resolve product entitlements locally
The system SHALL define persisted Free, Solo, and Pro plans with stable codes and resolve effective product features/limits from local subscription state without contacting a payment provider. Free SHALL allow two first-issued invoices per Europe/London calendar month and SHALL NOT grant PaidIncomeSummary. Solo and Pro SHALL initially allow unlimited first issuance and grant PaidIncomeSummary. Administrative capabilities SHALL NOT imply a product plan.

#### Scenario: Default new user
- **WHEN** an authenticated user has no effective paid subscription
- **THEN** the resolver SHALL return Free with the two-invoice limit and no PaidIncomeSummary grant

#### Scenario: Paid manual assignment
- **WHEN** a user has an effective Solo or Pro assignment
- **THEN** the resolver SHALL return unlimited issuance and PaidIncomeSummary without a billing-provider request

#### Scenario: Super Administrator has no product grant
- **WHEN** a Super Administrator has no effective paid subscription
- **THEN** their effective product plan SHALL be Free despite administrative capabilities

### Requirement: Grants can expire without resetting usage
The system SHALL support immediate permanent or expiring manual assignments. At an assignment's exclusive end instant, or on cancellation/revocation, effective access SHALL fall back to Free without deleting data, resetting usage, changing administrative permissions, or requiring a scheduled expiry job. Future scheduled grants and overlapping active assignments SHALL NOT be supported in this change.

#### Scenario: Expiring Pro grant reaches its end
- **WHEN** the authoritative current instant equals or exceeds the Pro grant's end
- **THEN** effective access SHALL be Free and current-month usage SHALL remain unchanged

#### Scenario: Permanent Founder grant
- **WHEN** a user has a permanent Pro grant with reason Founder
- **THEN** it SHALL remain effective until an authorized explicit change rather than expiring automatically

#### Scenario: Expiry is observed during an existing session
- **WHEN** a grant expires while the user remains signed in
- **THEN** subsequent protected operations SHALL use Free entitlements rather than stale session/UI claims

### Requirement: Founder rollout is one-time and precisely scoped
Migration SHALL grant permanent Manual Pro with reason Founder only to users who, at rollout, have both a bound non-empty Google identity and populated LastLoginUtc. It SHALL record an auditable migration source. Eligibility SHALL NOT require administrative privileges or active account status, and granting Pro SHALL NOT activate an inactive account. Later sign-in and repeated startup SHALL NOT trigger this migration again.

#### Scenario: Existing signed-in user
- **WHEN** a rollout user has a bound Google identity and non-null LastLoginUtc
- **THEN** they SHALL receive permanent Pro with a Founder audit record

#### Scenario: Identity or login is missing
- **WHEN** either the Google identity is unbound or LastLoginUtc is null
- **THEN** the user SHALL remain Free unless separately granted a plan

#### Scenario: Ineligible user signs in after rollout
- **WHEN** an account first binds/signs in after the one-time migration
- **THEN** it SHALL NOT automatically become a Founder

#### Scenario: Later administrative downgrade survives restart
- **WHEN** a Founder is explicitly moved to Free and the application restarts
- **THEN** the migration SHALL NOT restore their former Pro grant

### Requirement: History and existing exports remain plan-independent
All plans SHALL retain authorized access to underlying business records, complete history, existing document downloads/exports, and paid-status recording. No history window, data deletion, or comprehensive new export product SHALL be introduced by entitlement enforcement.

#### Scenario: Paid grant expires with old records
- **WHEN** a user's grant expires and they have old invoices, gigs, receipts, and previously issued documents
- **THEN** ordinary authorized retrieval and existing downloads/exports SHALL remain available

#### Scenario: Reporting is restricted
- **WHEN** Free denies the PaidIncomeSummary convenience
- **THEN** underlying paid-invoice records and their ordinary filters SHALL remain accessible under existing ownership rules
