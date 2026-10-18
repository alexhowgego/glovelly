# Pre-Merge Regression

## Purpose

Use this checklist before shipping changes that touch gigs, imported gigs, expenses, receipts, invoices, expense statements, delivery, seller profile, connected services, or admin workflows.

This page gives the tester a single path through the product. Some detailed journeys live on separate pages and are linked where they fit.

## Preconditions

- You can sign in to the environment being tested.
- The environment has at least one client. If it does not, create one during the client checks.
- You know whether email and Google Drive delivery are configured for this environment.
- For local testing, the app is running and the automated checks have already passed.

For local development, engineers usually run:

```bash
./verify.sh
./run-dev.sh
```

## Core Smoke Journey

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.SmokeTests` covers public smoke endpoints and sign-in entry point visibility; signed-in workspace navigation remains manual.

### Steps

1. Sign in.
2. Open Clients and confirm the client list loads.
3. Open Gigs and confirm existing gigs load.
4. Open Invoices and confirm existing invoices load.
5. Open user settings or seller profile and confirm the modal opens and closes.
6. Open the profile menu and confirm `Imported gigs` is present.
7. Refresh the page and confirm the same data returns without session issues.

### Expected Results

Navigation, session state, and core reads are healthy.

## Empty List Views

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.GigListVisibilityTests.EmptyListViewsRemainUsableAndGigFiltersCanBeCleared`; retain a desktop spot check when changing workspace layout.

### Steps

1. At a desktop viewport wider than 1180px, sign in to an account with no clients, gigs, or invoices.
2. Open Clients, Gigs, and Invoices in turn and confirm each list's controls and empty state are visible.
3. Create a future planned gig, select `Drafts` to show no results, then select `All`.

### Expected Results

The left list pane remains usable when no record is selected. Filter controls remain visible and can restore a filtered-out list.

## Compact Desktop Workspace

> **Automation:** `Glovelly.Uat.Tests.WorkspaceLayoutTests` covers desktop pane geometry, large and empty lists, filter recovery, editor toggles, keyboard focus, and constrained viewports. Admin layout uses an isolated browser response fixture: the staging UAT identity is not granted administrator permissions. Run browser tests in Ubuntu CI, not locally on macOS.

### Steps

1. At 1280×800 and 1440×900 CSS pixels, open Clients, Gigs, Invoices, and (as an administrator) Users.
2. Confirm compact branding, navigation, capture, profile access, and the informational summary strip are visible. The large slogan, mascot introduction, duplicate shell workspace title, summary action buttons, and separate Admin banner should be absent.
   Gordon's small brand icon should have rounded corners matching the controls. The outer enclosing frame should have no border, background, shadow, or extra padding; individual panels remain distinct, with a page gutter and quiet legal/build information beneath them.
3. With enough records to overflow, scroll each list to its final record. Search, filters, actions, and column headings should stay reachable without moving the document.
4. Select a record with long details, scroll its overview, open its editor, and scroll to the editor's final action. Neither pane should enlarge the page or make the list's records unreachable.
5. Search for an impossible term, then clear it. Repeat with no records and no selection. Controls and empty states must not collapse or truncate.
6. Tab through records and editor actions; confirm focused controls are revealed within the appropriate pane and focus rings remain visible.
7. Repeat at 390×844, 1280×600, and 200% browser zoom. Page scrolling must reach all content without horizontal document overflow. Open Settings and Seller profile and reach their final actions.
8. Check profile menus, capture dialogs, persistent error notifications, and mobile capture/return-to-top placement. Notifications must remain below modal overlays and must not block modal controls.
9. Apply an invoice filter chip, follow an invoice-line gig link, and use capture review's **Go to gig**. Confirm the intended result or target is revealed; desktop remains bounded and mobile scrolls naturally.

### Expected Results

Normal desktop record work fits the viewport with independent pane scrolling. Informational summary values and loading/error states remain available across views without action buttons. Small windows and enlarged text favor reachable content over forced containment. Settings remain naturally scrollable within their existing dialogs.

### Documentation Capture Review

The shared chrome and workspace geometry changed. Request the Ubuntu CI documentation-capture job for this change and review its `TestResults/documentation-captures/` candidates for client workspace, gig workspace, expenses/mileage, and invoice status. Check both light and dark product themes, including whether the bounded detail pane exposes the intended subject. Replace checked-in screenshots only after reviewing those real staging captures; never create substitutes locally. Record CI run and capture-review results before completing the OpenSpec validation tasks.

## Notification Feedback

> **Automation:** Partially automated UAT: invoice, gig, receipt, and attachment browser journeys assert terminal Sonner notifications. Manual checks cover dismissal, modal stacking, and visual placement.

### Steps

1. Complete a terminal action such as saving a client, uploading a receipt, or changing an invoice status.
2. Confirm a notification appears at the top of the viewport without needing to locate the initiating panel.
3. Trigger a safe failure, such as an unavailable mileage estimate or attachment download.
4. Confirm the error notification remains visible until dismissed using its accessible close control.
5. Open and close a modal after starting an applicable action, then confirm the terminal notification remains visible.
6. On a phone-sized viewport, confirm notifications do not cover the quick-capture controls or return-to-top button.

### Expected Results

Terminal success and information notifications auto-dismiss. Errors persist until dismissed. Form validation, progress, and durable configuration guidance remain in their original local context.

## Dashboard Summary

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.DashboardSummaryTests.DashboardShowsContextSpecificCardsAndInvoiceDrillDown`; manual balance edge-case spot checks remain useful when paid, cancelled, issued, and overdue invoices already exist together.

### Steps

1. Sign in with at least one draft, issued, or overdue invoice available.
2. Open Invoices and confirm the compact strip shows outstanding balance, overdue invoices, and income this financial year. Outstanding balance includes draft, issued, and overdue totals, but excludes paid and cancelled invoices.
3. Confirm no **View invoices** buttons remain in the summary strip. Use the invoice filter chips to select Outstanding, Overdue, and Income this financial year and confirm the matching records appear.
4. Open Gigs and confirm the strip shows upcoming gigs, awaiting confirmation, and completed uninvoiced work.
5. Open Clients and confirm active clients, clients with outstanding invoices, and most frequent client are shown.
6. Confirm three summary slots remain present during loading and errors, with unavailable values distinct from zero.
7. As an administrator, open Users and confirm the strip shows Users with access, Active accounts, and Administrators, with no separate Admin banner. Search the directory and confirm these totals do not change. Open Settings and close it without losing the shared header or summary state.

### Expected Results

The top-level dashboard surfaces actionable work without relying on stale search filters or manual workspace navigation.

## Connected Services

When changes touch Google Calendar, run the focused [Google Calendar](calendar.md) journey.

## Cross-Workspace Navigation Shortcuts

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.CrossWorkspaceNavigationTests.ClientAndInvoiceLineShortcutsOpenTargetsDespiteStaleFilters` and `InvoiceLineNavigationDoesNotReusePreviouslySavedGigEditorState`

### Steps

1. Open Gigs and select a gig with a known client.
2. Click the client name in the gig overview.
3. Confirm the app opens Clients with that client selected.
4. Open Invoices and select an invoice with a known client.
5. Click the client name in the invoice overview.
6. Confirm the app opens Clients with that client selected.
7. Open the same invoice line-items pane.
8. Click a generated line-item title for a performance fee, mileage, passenger mileage, or expense line.
9. Confirm the app opens Gigs with the corresponding gig selected.
10. Confirm manual adjustment lines are not shown as gig links.
11. For two gigs on one draft invoice, edit and regenerate the first gig, then use an invoice-line shortcut to open the second without refreshing the browser.
12. Confirm the second gig editor shows only the second gig's saved fields and expenses before saving an intended second-gig change.

### Expected Results

Cross-workspace shortcuts preserve the intended target record, clear stale search filters that would hide the target, hydrate an open gig editor from the target record, and leave unrelated records unchanged.

Workflow automation uses the browser's reduced-motion preference so decorative transitions and smooth scrolling cannot compete with pointer actions. Gig-save helpers wait for the editor to close after the response, and invoice-line shortcuts assert the Gigs workspace and exact destination heading rather than an unrelated action button. These checks must pass without forced clicks, sleeps or click retries.

Also check with normal motion on a narrow viewport: open/close client, gig and invoice editors promptly, and follow a gig link after opening invoice lines. Pending editor-scroll timers must be cancelled when the editor closes/unmounts. With reduced motion enabled, editor navigation should scroll immediately and expanding panes should not animate.

## Editor Navigation Regression Checks

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.EditorNavigationGuardTests` covers client and gig editor discard accept/decline paths; admin editor guard checks remain manual.

### Purpose

These checks guard against unsaved edits being discarded while moving between records.

### Clients

1. Open Clients, select a client, and click `Edit`.
2. Change a field without saving.
3. Select a different client in the list and decline the discard prompt.
4. Confirm the original client remains selected and the unsaved edit remains visible.
5. Select the different client again and accept the discard prompt.
6. Confirm the editor updates to the newly selected client.
7. Change a field again, click `New client`, and decline the discard prompt.
8. Click `New client` again and accept the discard prompt.

Expected result: unsaved client edits are never discarded without confirmation. Accepted navigation switches the editor to the selected client or a blank new-client form.

### Client Deletion

1. Open Clients and select a client with gigs or invoice history.
2. Confirm `Delete` is disabled and the helper text explains the blocking records.
3. Select a client with no gigs and no invoices.
4. Click `Delete`, decline the confirmation prompt, and confirm the client remains.
5. Click `Delete` again, accept the confirmation prompt, and confirm the client is removed from the list.

Expected result: clients cannot be deleted silently. Deletion is only available after explicit confirmation and only when the client has no gig or invoice records.

### Gigs

1. Open Gigs, select a gig, and click `Edit gig`.
2. Change a field or add an unsaved expense row.
3. Select a different gig in the list and decline the discard prompt.
4. Confirm the original gig remains selected and the unsaved edit remains visible.
5. Select the different gig again and accept the discard prompt.
6. Confirm the editor updates to the newly selected gig.
7. Change a field again, click `New gig`, and decline the discard prompt.
8. Click `New gig` again and accept the discard prompt.

Expected result: unsaved gig edits and unsaved expense draft fields are never discarded without confirmation. Accepted navigation switches the editor to the selected gig or a blank new-gig form.

### Gig History And Selection

1. Create a future planned gig, a past completed uninvoiced gig, a past completed invoiced gig, a past draft, and a past cancelled gig.
2. Confirm `Work queue` is selected by default and includes the future planned gig, past completed uninvoiced gig, and past draft, but not the past completed invoiced or cancelled gigs.
3. Confirm `Upcoming` includes non-cancelled gigs dated today or later, `Uninvoiced` includes the historical completed uninvoiced gig, and `Drafts` and `Completed` include matching historical records.
4. Select `All` and confirm every stored gig, including the historical cancelled gig, appears.
5. Select a gig, then change filters or sort order while it remains visible; confirm it stays selected.
6. Hide or delete the selected gig and confirm the first remaining visible gig is selected, or the empty state is shown.
7. Follow an invoice-line link to a historical gig while it is hidden; confirm the workspace selects `All`, clears incompatible filters, and explains the changed view.

Expected result: every named gig view describes its complete result set without a separate historical-gig control. The selected gig always belongs to the rendered list. Explicit navigation reveals its target through `All`, while passive list changes preserve a visible selection or select the first visible fallback.

### Admin

1. Open Admin as an administrator, select a user, and click `Edit access`.
2. Change a field without saving.
3. Select a different user in the list and decline the discard prompt.
4. Confirm the original user remains selected and the unsaved edit remains visible.
5. Select the different user again and accept the discard prompt.
6. Confirm the editor updates to the newly selected user.
7. Change a field again, click `Add user`, and decline the discard prompt.
8. Click `Add user` again and accept the discard prompt.

Expected result: unsaved admin access edits are never discarded without confirmation. Accepted navigation switches the editor to the selected user or a blank add-user form.

## Gig To Invoice Journey

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.CoreInvoiceWorkflowTests.AuthenticatedUserCanCreatePreviewAndSendInvoiceToConfiguredRecipient`

### Steps

1. Create a client or choose an existing client.
2. Confirm the seller profile has a postcode and country if mileage estimation is configured in this environment.
3. Create a gig with fee, date, venue, status `Planned`, and at least one expense.
4. Save the gig.
5. Edit the saved gig, enable `I was driving for this gig`, and click `Estimate mileage` if Google Routes is configured.
6. Confirm the travel miles field is filled, or record that mileage estimation was skipped because the environment is not configured.
7. Save the gig.
8. Generate an invoice from that gig.
9. Open the invoice.
10. Confirm invoice lines include the performance fee, chargeable expenses, and mileage when travel miles were saved.
11. Download the PDF.
12. Return to the gig.
13. Confirm the gig shows as invoiced and links back to the invoice.

### Expected Results

Generating an invoice links the gig, creates expected lines, includes saved mileage when applicable, and produces a downloadable PDF.

## Editing An Invoiced Gig

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.InvoicePromptChoiceTests.DecliningLinkedDraftRegenerationLeavesExistingInvoiceLinesUnchanged` covers declining draft regeneration after an invoice-relevant gig edit; accepting regeneration and issued-invoice spot checks remain manual/backend-covered.

### Purpose

This guards against accidental invoice changes when a linked gig is edited.

### Status-Only Change

1. Start with a gig that is already linked to an invoice.
2. Edit the gig.
3. Change only the gig status between `Planned`, `Completed`, and `Draft`.
4. Save.
5. Open the linked invoice.

Expected result: no prompt appears, the gig remains linked to the invoice, and invoice lines are unchanged.

### Invoice-Relevant Change

1. Edit the same linked gig.
2. Change an invoice-relevant field such as fee, title, date, venue, or expenses.
3. Save.

Expected result: if the linked invoice is a draft, the app asks whether to regenerate the draft. If accepted, the draft invoice lines and PDF update. If declined, the existing invoice stays unchanged. Issued invoices are not silently changed.

## Cancelling A Gig With A Linked Invoice

> **Automation:** Backend automated; manual UAT

### Steps

1. Start with a gig linked to an invoice.
2. Edit the gig.
3. Change status to `Cancelled`.
4. Save.

### Expected Results

The app asks whether to cancel the linked invoice. If accepted and the invoice status allows cancellation, the invoice moves to `Cancelled`. If declined, the gig is cancelled but the invoice remains unchanged.

## Deleting A Gig

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.GigEndpointsTests.DeleteGig_WhenPlanned_DeletesGig`, `DeleteGig_WhenNotPlanned_ReturnsValidationProblem`, and `DeleteGig_WhenLinkedToInvoice_ReturnsValidationProblem`

### Positive Check

1. Create or identify an uninvoiced gig with status `Planned`.
2. Select the gig in Gigs.
3. Confirm the `Delete gig` button is red and enabled.
4. Click `Delete gig` and decline the confirmation prompt.
5. Confirm the gig remains in the list.
6. Click `Delete gig` again and accept the confirmation prompt.

Expected result: the planned, uninvoiced gig is removed from the list and cannot be reopened.

### Negative Checks

1. Select a gig with status `Draft`, `Completed`, or `Cancelled`.
2. Confirm `Delete gig` is disabled and explains that only planned gigs can be deleted.
3. Select a planned gig that is linked to an invoice.
4. Confirm `Delete gig` is disabled and explains that gigs with linked invoices cannot be deleted.

Expected result: only planned gigs with no linked invoice can be deleted. Linked invoice history is never removed by deleting a gig.

## Cloning A Gig

> **Automation:** Manual UAT

### Without Expenses

1. Create or identify a saved gig with a fee, date, venue, notes, and driving details.
2. Select the gig in Gigs.
3. Click `Clone gig`.
4. If the gig has expenses, decline the expenses prompt.
5. Confirm a new gig is created and immediately opens in the edit pane.
6. Confirm the cloned gig has the same core details as the original, starts in `Draft` status, has no linked invoice, and has no copied expenses.
7. Change at least one identifying detail, such as date or title, then save.

Expected result: cloning creates a separate `Draft` gig record, opens it for editing straight away, and never copies invoice linkage.

### With Expenses

1. Select a saved gig with at least one expense and, ideally, at least one receipt attachment.
2. Click `Clone gig`.
3. Accept the expenses prompt.
4. Confirm the cloned gig opens in the edit pane in `Draft` status with copied expense descriptions and amounts.
5. Confirm copied expenses have no receipt attachments.
6. Save the cloned gig, then generate an invoice from it.

Expected result: expenses are copied only when accepted, receipt attachments are not copied, and any invoice generated from the clone is a new invoice linked only to the cloned gig.

## Gig Attachments

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.UploadAndQuickCaptureWorkflowTests` covers browser file-only attachment upload/delete and quick attachment link capture; focused checks live in [Gig attachments](gig-external-resources.md).

### Steps

1. Open Gigs and select a saved gig.
2. Confirm existing attachments, if any, appear in the `Attachments` area.
3. Click `Add attachment` and save an attachment with a title, purpose, type, and URL.
4. Add or edit a second attachment for the same purpose and mark it primary.
5. Upload a small file to one attachment, download it, then delete the file.
6. Use the floating `+` button to quick add a file or link attachment and confirm the matched gig can be changed before saving details.

### Expected Results

Attachments stay scoped to the selected gig, the attachment editor opens as a modal, quick add uses the same gig matching behaviour as quick receipts, only one attachment per purpose is primary, URL-less file attachments are allowed, and file actions do not affect expenses or invoice state.

## Combined Invoice Journey

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.InvoiceAggregationWorkflowTests.CanGenerateCombinedInvoiceForSameClientGigs`

### Steps

1. Create two uninvoiced gigs for the same client.
2. Select both gigs in the gig list.
3. Generate a combined invoice.
4. Confirm the generated invoice preview modal opens.
5. Download the PDF or open the invoice.

### Expected Results

One invoice is created, both gigs are linked, invoice lines are ordered sensibly by gig date and line type, and the generated PDF can be previewed before navigating away.

### Negative Check

1. Select gigs from different clients.
2. Try to generate a combined invoice.

Expected result: the app blocks generation and explains that selected gigs must belong to the same client.

## Imported Gig Review Smoke Check

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.ImportedGigReviewTests.ImportedGigModalAutosavesAndCommitsReviewedRows` covers staging setup, modal entry, autosave, accept/reject, and commit; real MCP client creation and duplicate-warning presentation remain manual/backend-covered.

Run the focused [Imported gigs](gig-imports.md) journey when the change touches MCP, gigs, or profile-menu workflows. For broad pre-merge smoke, at minimum:

1. Open the profile menu.
2. Open `Imported gigs`.
3. If staged imports exist, select a batch and confirm rows load in the modal.
4. If there are pending rows, edit a harmless field and confirm there is no row-level save button.
5. If a row resembles an existing gig, confirm it shows a possible duplicate warning without disabling commit.
6. Close the modal and reopen it.

Expected result: imported gigs stay in a modal launched from the profile menu, row edits autosave, duplicate warnings are advisory only, and the main Clients/Gigs/Invoices navigation is unchanged.

## Monthly Invoice Journey

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.InvoiceAggregationWorkflowTests.CanGenerateMonthlyInvoiceForEligibleClientGigs`

### Steps

1. Create or identify multiple uninvoiced gigs for the same client in the same month.
2. Open the client.
3. Choose the month.
4. Generate a monthly invoice.
5. Confirm the generated invoice preview modal opens.
6. Download the PDF or open the invoice.

### Expected Results

The invoice is created as a draft, linked gigs remain linked, lines are generated after redraft, and the PDF can be previewed and downloaded.

## Detailed Journeys

Run the focused pages when the change touches those areas, or when the pre-merge path exposes a concern:

- [Invoices](invoices.md)
- [Expenses](expenses.md)
- [Gig attachments](gig-external-resources.md)
- [Imported gigs](gig-imports.md)
- [Enrolment and access](enrolment.md)

## Regression Notes

Pay special attention to workflows that cross boundaries:

- Editing gigs should not silently mutate historical or issued invoices.
- Draft invoice regeneration should happen only after an explicit user choice.
- Receipt attachment changes should not mutate reimbursement status.
- Reimbursement status should affect future generated documents, not old PDFs.
- Expense statements are projections and should not create invoices or mutate gig invoice links.

When a manual journey reveals a bug, record the exact steps and tell the engineer or release owner. The fix should usually include an automated backend test for the server-side rule, and the frontend flow may later become an automated browser test.

Keep these manual journeys aligned with automated coverage by updating the nearest **Automation** line whenever a Playwright UAT test is added, removed, or meaningfully changes scope.
