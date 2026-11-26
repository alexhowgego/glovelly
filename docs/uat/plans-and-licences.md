# Plan And Licence UAT Journeys

> **Automation:** Partially automated UAT: `EntitlementWorkspaceTests` covers Free reporting and licence-only navigation, inline conflicts, completion notifications, and desktop/mobile placement using intercepted commercial fixtures. Backend tests cover real PostgreSQL issuance, migration, assignment concurrency, and failure atomicity. New browser tests must run in Ubuntu CI; local compilation is not execution evidence.

## Free first issuance

1. Use an isolated Free user with zero current-month usage. Create, edit, preview, and download several Drafts: all remain watermarked and usage stays zero.
2. Issue one invoice. The existing single confirmation explains one unit, UK reset, and that issue alone does not send. Cancel once first and verify no state/usage/gig mutation.
3. Issue a second invoice. Create a third Draft: issue/email/Drive review explains exhaustion without another prompt or checkout link; existing drafts remain editable and downloadable.
4. Race two first issues for the last unit from separate tabs/instances: only one succeeds. Direct generic/status API paths must have the same limit.
5. Return an issued invoice to Draft for correction and issue again at exhausted allowance. It costs no additional unit. Cancelling or deleting a corrected invoice does not refund usage.

## Plan changes and expiry

1. Issue more than two invoices on Pro. Change to Free within the same UK month: usage remains above two and remaining is zero.
2. Grant expiring Pro and leave invoice reviews open across expiry. The plan display falls back to Free, and the server's current access wins. Do not reset usage, hide history, or delete documents.
3. Cross a UK month boundary (including a daylight-saving transition in backend time-controlled tests): the reset uses local calendar midnight, without rollover. Leave the page open and confirm its usage snapshot refreshes at reset.
4. Repeat manual grants/revocations and verify the same month's usage remains continuous.
5. Confirm migrated eligible existing users have permanent Founder Pro; bound-but-never-logged-in and unbound accounts remain Free. Later sign-in must not re-grant a Founder downgrade.

## Restricted reporting and retained access

1. On Free, open Invoices at desktop and mobile widths. The income card shows Solo/Pro required, not zero income, a health failure, or repeated error notifications.
2. The reconciled financial-year reporting filter is unavailable. Ordinary paid filters, payment recording, full history, PDFs, receipts, and existing exports remain usable.
3. Request the paid-income endpoint and MCP business-summary tool directly: expect feature restrictions and no aggregate. Raw owner-scoped MCP invoice listing/detail still works.
4. Expire/revoke access after a paid summary loaded. Refresh/return to the workspace: old totals/IDs must not remain presented as current reporting.

## Licence-only administration

1. Sign in with ManageLicences but not ManageUsers/ManagePermissions. Open Licences without loading the user-management directory; do not expose account/permission editing.
2. Check the three dashboard cards: Free accounts, Solo / Pro accounts, and paid grants expiring within 30 days. Counts cover all accounts, including inactive accounts, not just the current search/page. Verify loading/unavailable states are not fabricated zeros; saving a changed grant refreshes counts. Find a user in the compact directory; selecting a row opens the same overview frame used by Admin, not an immediate editor. Inspect effective plan, usage, expiry, and recent assignment history. Use the docked **Edit licence** action to open the shared Management Pane.
3. Verify one audit change with actor/target snapshots and old/new plan/expiry. No-op saves must not add misleading records.
4. Open the same user in two sessions. After one saves, the other's stale version must show an inline conflict. Reload/reselect before saving; do not silently overwrite.
5. Use **Save and close** for one completion notification and a refreshed, still-selected overview. Use **Save** to keep the Management Pane open with inline success and the refreshed assignment version; it must not produce a notification. Discard unsaved edits, decline the discard once, then confirm it; reopening must restore the saved assignment. At desktop and mobile widths, the plan combobox must have the exact accessible name **Plan** within the **Update licence** form, independent of its option text.
6. Check permanent/expiring grants, Free assignment, and revocation through the same editor controls as Admin. Single-page directories must not show redundant disabled paging controls. On desktop, the workspace fills the available height below the dashboard with the footer visible; the list, overview body, and editor own their scrolling and the action dock remains reachable. On mobile/short viewports, the panes stack in document flow without horizontal page overflow. Check Clients, Gigs, Invoices, and Admin retain the same behavior through the shared record workspace.
7. Revoke the manager's capability during editing: the next mutation is forbidden with no audit/assignment side effect. ManageUsers alone cannot assign licences.

## Failure and partial-success review

- Fail rendering, candidate storage, or database save/commit before first issuance commits. The old Draft pointer/bytes and allowance remain unchanged; no clean candidate may be read or delivered.
- Lose the acknowledgement after a real commit. Retain the committed pointer/usage/bytes; refresh before retry and never double-consume.
- Fail email/Drive after issuance: retain Issued state and usage; keep review/confirmation feedback inline, then retry delivery without another issue or stacked confirmation.
- Confirm failed renders cannot overwrite a newer document, and generic line edits participate in parent concurrency.
- Test notification/mobile/modal placement using the existing [invoice journeys](invoices.md), and preserve persistent-error coverage after dismissing a notification.

## Release evidence

Record real PostgreSQL CI and Ubuntu browser results plus staging visual review. The archived watermarking tasks remain pending until their specified CI/staging evidence exists. Do not manufacture or replace guide screenshots; review actual CI documentation-capture candidates.
