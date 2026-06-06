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

## Dashboard Summary

> **Automation:** Manual UAT

### Steps

1. Sign in with at least one draft, issued, or overdue invoice available.
2. Confirm the dashboard shows an outstanding balance that includes draft, issued, and overdue invoice totals, but excludes paid and cancelled invoices.
3. Confirm the `Next gig` card shows the earliest non-cancelled upcoming gig.
4. Click `Open gig` and confirm the app opens Gigs with that gig selected.
5. Identify a recent past or current uninvoiced gig with status `Planned` or `Completed`.
6. Confirm the invoice prompt shows that gig and click `Generate invoice`.
7. Confirm the invoice preview opens, the gig becomes linked to the generated invoice, and returning to the dashboard no longer offers that gig as the invoice prompt.

### Expected Results

The top-level dashboard surfaces actionable work without relying on stale search filters or manual workspace navigation.

## Connected Services

When changes touch Google Calendar, run the focused [Google Calendar](calendar.md) journey.

## Cross-Workspace Navigation Shortcuts

> **Automation:** Manual UAT

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

### Expected Results

Cross-workspace shortcuts preserve the intended target record, clear stale search filters that would hide the target, and leave unrelated records unchanged.

## Editor Navigation Regression Checks

> **Automation:** Manual UAT

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

> **Automation:** Backend automated; manual UAT

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
6. Confirm the cloned gig has the same core details as the original, has no linked invoice, and has no copied expenses.
7. Change at least one identifying detail, such as date or title, then save.

Expected result: cloning creates a separate gig record, opens it for editing straight away, and never copies invoice linkage.

### With Expenses

1. Select a saved gig with at least one expense and, ideally, at least one receipt attachment.
2. Click `Clone gig`.
3. Accept the expenses prompt.
4. Confirm the cloned gig opens in the edit pane with copied expense descriptions and amounts.
5. Confirm copied expenses have no receipt attachments.
6. Save the cloned gig, then generate an invoice from it.

Expected result: expenses are copied only when accepted, receipt attachments are not copied, and any invoice generated from the clone is a new invoice linked only to the cloned gig.

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

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.GigImportEndpointsTests` covers commit rules; modal entry, autosave, and notification dots remain manual.

Run the focused [Imported gigs](gig-imports.md) journey when the change touches MCP, gigs, or profile-menu workflows. For broad pre-merge smoke, at minimum:

1. Open the profile menu.
2. Open `Imported gigs`.
3. If staged imports exist, select a batch and confirm rows load in the modal.
4. If there are pending rows, edit a harmless field and confirm there is no row-level save button.
5. Close the modal and reopen it.

Expected result: imported gigs stay in a modal launched from the profile menu, row edits autosave, and the main Clients/Gigs/Invoices navigation is unchanged.

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
