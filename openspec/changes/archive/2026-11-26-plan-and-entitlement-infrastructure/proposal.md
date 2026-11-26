## Why

Glovelly needs a provider-independent commercial model before introducing payment processing: users must have explicit plans, licence administrators must be able to grant early access, and first issuance must enforce an allowance without restricting safe draft experimentation. This implements GitHub issue [#192](https://github.com/alexhowgego/glovelly/issues/192), building on capability-based administration and the archived draft-invoice PDF watermarking prerequisite.

## What Changes

- Add locally stored Free, Solo, and Pro plans, per-user subscriptions/manual assignments, effective entitlement resolution, and immutable usage/audit records. Founder/Tester describe assignment reasons, not separate copies of plans.
- Give Free two first-issued invoices per Europe/London calendar month, with no rollover. Solo and Pro initially have unlimited issuance; commercial differences beyond the agreed reporting gate remain provisional.
- Track first issuance on every plan. Plan changes, expiry, cancellation, deletion, reissue, and delivery retries do not reset or refund usage. Expired/revoked paid access falls back to Free.
- Permit permanent or expiring manual grants through a standalone licence workspace/API protected by `ManageLicences`, with audited changes and no automatic administrative-permission changes.
- Run a one-time migration assigning permanent Pro with reason `Founder` only to users with both a bound Google identity and populated `LastLoginUtc`; other users default to Free.
- Enforce first issuance through a shared transactional backend flow, including concurrency, all API paths, immutable first-issue identity, and private generation-specific PDF publication.
- **BREAKING:** Replace writable invoice-entity contracts with controlled requests: new invoices must begin as Draft, lifecycle/PDF/commercial audit fields are server-owned, and attempts to inject them are rejected. Existing consumers and fixtures move to the shared issuance flow.
- **BREAKING:** Gate the paid-income summary and its reconciled drill-down behind the PaidIncomeSummary entitlement (Solo/Pro). Apply the same gate to the existing MCP business-summary aggregate, while preserving owner-scoped raw record access.
- Add current-plan/usage/reset information, clear allowance/feature restriction states, and accurate first-issue messaging within existing confirmations and email/Drive reviews, without adding prompts or payment links.
- Preserve all business history, paid-status recording, authorized document downloads, existing exports, and retrieval of underlying records on every tier; no history window or new general-account export system in this change.
- Do not add Stripe, checkout, webhooks, final pricing, tenants/multi-user billing, purchased credit balances, or speculative future feature gates.

## Capabilities

### New Capabilities

- `plans-and-subscriptions`: Local plan catalogue, effective per-user subscription resolution, optional grant expiry, Free fallback, and one-time Founder rollout.
- `invoice-issuance-allowances`: Continuous first-issue usage, UK-calendar-month allowance enforcement, authoritative write boundaries, concurrency, and safe clean-PDF publication.
- `licence-administration`: Capability-separated manual assignment and audit UI/API, including standalone access for licence-only administrators.
- `self-service-entitlements`: User-visible plan, usage, reset and upgrade-required information integrated into existing invoice workflows.

### Modified Capabilities

- `paid-income-summary`: Require the reporting entitlement for the aggregate/drill-down while retaining unchanged payment-date and inclusion calculations.
- `draft-invoice-pdf-watermarking`: Explain actual first-issue allowance impact within the existing single confirmation and combined delivery flow.
- `capability-based-administration`: Make licence-only navigation explicit while keeping licensing independent of administrative authority and user management.
- `mcp-read-tools`: Gate precomputed paid-income/business-summary reporting consistently without gating ordinary user-scoped record retrieval.

## Impact

- Backend EF models/configurations/migrations, user rollout, entitlement/query services, invoice workflow/write requests/PDF storage and publication, paid-income and MCP query surfaces, and licence endpoints.
- Frontend session/workspace entitlement loading, shared invoice confirmation/delivery flows, paid-income dashboard locked states, standalone licensing workspace, API types, Vite proxy/service-worker prefix handling, and notifications.
- PostgreSQL concurrency/migration/failure tests, frontend unit tests, Ubuntu browser UAT, and user/operator documentation. The watermarking prerequisite's pending CI/staging checks remain release prerequisites rather than assumed successes.
- Plan values live in a versioned database catalogue, not new environment settings. No payment-provider dependency is required; any implementation-time runtime setting must update all repository-required deployment and documentation surfaces.
