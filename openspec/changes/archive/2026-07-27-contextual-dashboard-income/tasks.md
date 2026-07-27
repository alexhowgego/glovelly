## 1. Paid-Income Foundation

- [x] 1.1 Add nullable `PaidOn` invoice persistence and API contract support, including the required additive PostgreSQL schema evolution.
- [x] 1.2 Add a reusable UK financial-year helper that resolves Europe/London dates and inclusive 6 April to 5 April boundaries.
- [x] 1.3 Set `PaidOn` when an invoice enters Paid and clear it when reissue returns an invoice to Draft.
- [x] 1.4 Add a user-scoped invoice paid-income summary endpoint that returns period boundaries, total, and contributing invoice IDs from one shared inclusion query.
- [x] 1.5 Add backend integration tests for PaidOn lifecycle, UK financial-year boundaries, inclusion/exclusion, owner visibility, and total-to-ID reconciliation.

## 2. Invoice Drill-Down State

- [x] 2.1 Extend frontend invoice types and loading/error state for the paid-income summary response.
- [x] 2.2 Add the `income-this-financial-year` invoice quick filter backed by contributing invoice IDs.
- [x] 2.3 Refresh the paid-income summary after invoice payment-status and reissue changes, and navigate from the income card into its reconciled filter.

## 3. Contextual Dashboard Cards

- [x] 3.1 Extract pure dashboard-card view-data selection for Clients, Gigs, and Invoices, retaining exactly three card slots per context.
- [x] 3.2 Implement Gigs cards for upcoming work, Draft/awaiting-confirmation work, and completed uninvoiced work.
- [x] 3.3 Implement Invoices cards for outstanding balance, overdue invoices, and received income in the current financial year.
- [x] 3.4 Implement Clients cards for active clients, clients with outstanding invoices, and recently added clients.
- [x] 3.5 Update `AppShell` card rendering, actions, and responsive styles to distinguish loading, zero/empty, and error states consistently.

## 4. Verification And Documentation

- [x] 4.1 Add Vitest coverage for all context card sets, card values, and dashboard state distinctions using pure view-data helpers.
- [x] 4.2 Add or update invoice and dashboard UAT journeys for marking an invoice paid, financial-year income, and the reconciled drill-down.
- [x] 4.3 Run `dotnet test glovelly.sln -m:1`, `npm --prefix frontend/glovelly-web run test`, `npm --prefix frontend/glovelly-web run lint`, and `npm --prefix frontend/glovelly-web run build`.
