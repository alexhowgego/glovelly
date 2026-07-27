## Why

The dashboard currently shows the same generic summary cards regardless of whether a user is working with clients, gigs, or invoices. Users also cannot see cash income received in the current UK financial year, because invoices do not record the date on which payment was received.

This change makes the dashboard immediately useful in its current context while establishing a small, consistent cash-basis income foundation for future reporting.

## What Changes

- Replace the fixed dashboard summary with three stable card slots whose content is relevant to the selected Clients, Gigs, or Invoices workspace.
- Add a nullable invoice `PaidOn` date that is populated automatically when an invoice transitions to Paid and cleared when a paid invoice is reissued as a Draft.
- Define the current financial year as the UK period from 6 April through 5 April, using the Europe/London local date convention.
- Add a reusable, user-visible paid-income summary for the current financial year that includes only paid invoices whose `PaidOn` date is within that inclusive period.
- Show the paid-income total and applicable financial-year dates in the Invoices dashboard context, with a drill-down to the same contributing invoices.
- Provide consistent loading, zero/empty, and error states for dashboard cards, and add backend and Vitest coverage for the new behaviour.

## Capabilities

### New Capabilities
- `contextual-dashboard-cards`: Present a stable, context-specific dashboard card set for the Clients, Gigs, and Invoices workspaces.
- `paid-income-summary`: Record invoice payment dates and provide a UK-financial-year cash-income summary and reconciled invoice drill-down.

### Modified Capabilities
- None.

## Impact

- Frontend dashboard presentation and state coordination in `frontend/glovelly-web/src/App.tsx` and `src/components/AppShell.tsx`.
- Frontend invoice types, quick filtering, and pure Vitest summary/card-selection coverage.
- Invoice persistence model, API response shape, paid-status transition, reissue workflow, and user-scoped summary query/endpoints in `backend/Glovelly.Api`.
- Backend invoice integration tests and invoice/dashboard UAT documentation.
