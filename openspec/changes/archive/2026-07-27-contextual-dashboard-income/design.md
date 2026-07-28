## Context

The authenticated application has three primary workspaces: Clients, Gigs, and Invoices. `AppShell` renders a fixed three-card dashboard summary above all of them, while `App.tsx` derives its values from client-side gig and invoice collections. The card grid already has three fixed columns and responsive single-column behaviour.

Invoices currently store status and a status-update timestamp but no payment date. The paid status transition is managed by the invoice endpoint, and reissuing an invoice returns it to Draft. The system has no user-configurable financial-year, timezone, currency, or locale settings. This slice establishes an explicit UK-only convention rather than introducing internationalized settings.

## Goals / Non-Goals

**Goals:**
- Keep exactly three dashboard card slots while changing their content for the selected Clients, Gigs, or Invoices workspace.
- Record a canonical invoice payment date when an invoice becomes Paid.
- Calculate current-financial-year income from user-visible paid invoices and make the card and drill-down reconcile exactly.
- Use Europe/London to determine the current date and an inclusive 6 April to 5 April financial year.
- Cover card selection through the existing pure Vitest setup and financial rules through backend integration tests.

**Non-Goals:**
- Partial payments, payment amounts, payment history, or manual payment-date editing.
- Per-user financial-year, timezone, currency, or locale configuration.
- A general reporting screen, URL-persisted dashboard context, or a browser-component test framework.
- Changing invoice status-transition rules other than clearing payment metadata on reissue.

## Decisions

### Persist `PaidOn` as a nullable `DateOnly`

Add a nullable `PaidOn` property to `Invoice`. When an invoice transitions to Paid, the backend sets it to the current date in `Europe/London`. When reissue changes a paid invoice to Draft, it clears `PaidOn`.

This is a stable cash-basis fact rather than an inference from `StatusUpdatedUtc`, which is an audit timestamp and cannot represent a user-received payment date. A separate payment ledger was considered, but is deferred because partial payments and corrections are explicitly out of scope.

### Make the UK financial year an application convention

A reusable financial-year helper resolves the current Europe/London date and returns the inclusive range 6 April through 5 April. The helper is used by the paid-income query and any frontend display/filter support so boundary rules remain explicit and consistent.

Per-user fiscal-year and timezone settings were considered but would introduce settings, persistence, validation, and migration scope without a current internationalization need.

### Use a dedicated, user-scoped invoice income summary

Expose an invoice-summary endpoint within the existing invoices API group. Its response includes the financial-year start and end dates, paid-income total, and the IDs of contributing invoices. The endpoint uses `WhereVisibleTo` and a reusable query predicate that includes only invoices that are Paid and have `PaidOn` within the inclusive range.

Returning contributing IDs lets the frontend drill-down filter the already loaded invoice list without reimplementing inclusion logic. It also makes reconciliation testable: the displayed total and visible drill-down members originate from the same server query. A client-only calculation was rejected because it duplicates business rules and requires every invoice solely for a dashboard total.

### Model dashboard cards as pure, context-selected view data

Move dashboard-card selection and ordinary card derivation into a pure frontend helper. It receives the active section and workspace data, and returns exactly three card view models. `AppShell` renders those cards consistently, including loading, empty/zero, and error presentation.

The fixed three-card grid is retained to avoid layout movement. Gigs cards focus on upcoming, draft/awaiting confirmation, and completed-uninvoiced work; Invoice cards focus on outstanding, overdue, and received-this-financial-year income; Client cards focus on active, outstanding, and recently added clients. "Recently added" is selected over "recent activity" because the client record already has a creation timestamp while activity has no single defined source.

### Drill down through a named invoice quick filter

Add an `income-this-financial-year` invoice quick filter backed by the summary response's contributing invoice IDs. Selecting the income card activates Invoices, applies that filter, and leaves the normal list sorting and selection behaviour intact.

URL/query filters were considered but are broader navigation infrastructure than this slice needs.

## Risks / Trade-offs

- [A payment is recorded on a different calendar day from the London date.] → This shortcut intentionally stamps the Paid transition; a later payment-management slice will support corrections.
- [A reissued paid invoice could remain counted as income.] → Clear `PaidOn` whenever reissue returns the invoice to Draft and test the result.
- [Dashboard and list totals diverge.] → Use a single server query for the total and contributing invoice IDs; test their membership and sum together.
- [Initial data loading produces misleading zeros.] → Render an explicit loading state until the relevant workspace data and, for income, the summary request have resolved; keep zero distinct from unavailable/error.
- [Existing PostgreSQL databases do not receive the new column.] → The application currently uses `EnsureCreated` rather than EF migrations; deployment must include the repository's established schema-evolution mechanism or a safe additive database migration before code that writes `PaidOn` is released.
- [The fixed dashboard header increases UI coupling in `App.tsx`.] → Keep the selection/derivation pure and presentational rendering in `AppShell`; do not expand workflow logic in either component.

## Migration Plan

1. Add the nullable `PaidOn` column as an additive schema change; existing invoices retain null and therefore do not contribute to paid-income summaries.
2. Deploy backend status/reissue behaviour and the income summary endpoint.
3. Deploy frontend invoice typing, contextual cards, and drill-down behaviour.
4. Roll back the frontend/backend application code if needed; the nullable column is safe to retain and no historic paid dates are fabricated.

## Open Questions

- None for this slice. The UK-only financial-year convention and automatic PaidOn stamping are deliberate temporary constraints.
