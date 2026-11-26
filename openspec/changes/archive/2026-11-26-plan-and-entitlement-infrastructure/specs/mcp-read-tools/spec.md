## ADDED Requirements

### Requirement: Precomputed paid-income reporting respects local entitlements
The MCP business-summary aggregate SHALL require the same PaidIncomeSummary feature as the web reporting surface because it returns precomputed paid-income totals. A missing grant SHALL return an actionable feature-not-entitled tool error without a partial aggregate. Entitlement checks SHALL use the authenticated user's current local effective subscription. Ordinary owner-scoped invoice, gig, receipt, and contact listing/detail SHALL NOT become paid-only through this reporting gate.

#### Scenario: Free user requests business summary
- **WHEN** the authenticated user lacks PaidIncomeSummary and calls glovelly_get_business_summary
- **THEN** the tool SHALL return a feature restriction without the computed paid-income or other partial summary totals

#### Scenario: Paid user requests business summary
- **WHEN** the authenticated user has PaidIncomeSummary
- **THEN** the tool SHALL retain existing user scoping, arguments, and successful result semantics

#### Scenario: Free user retrieves raw invoice records
- **WHEN** a Free user calls ordinary invoice listing/detail tools
- **THEN** existing ownership checks SHALL apply without a reporting-feature denial or a new history window
