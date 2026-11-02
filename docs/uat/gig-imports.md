# Imported Gig UAT Journeys

Use these journeys when a change may affect MCP gig extraction, staged import batches, imported gig review, notification dots, or committing imports into real gigs.

## Preconditions

- You can sign in to Glovelly.
- At least one client exists that can be matched or selected for imported rows.
- The MCP client or local test harness can call the gig import tools.
- If testing locally, the frontend dev proxy includes `/gig-imports`; otherwise the import modal can fail to load behind the Vite shell.

## Prepare Gigs With Add to Glovelly

> **Automation:** Backend integration and frontend component tests cover manual recovery, owner-scoped source inspection and navigation. Provider-backed PDF and Google Docs checks require staging credentials and manual review in Ubuntu CI.

1. Submit a synthetic booking email, a multi-date PDF, and an accessible Google Doc in turn. Confirm source, proposal, evidence and confidence remain visible, and a PDF is not automatically treated as a gig merely because it is a PDF.
2. Override the treatment to **Gig information**, retry analysis, and confirm the override and source remain intact. Choose **Prepare gig drafts** and refresh while it runs; resume and verify the same batch is recovered.
3. Confirm **Draft batch prepared** remains open with **Review import** and **Done**, and that no gig, expense, resource or set list exists before explicit commit. Open review and download the original source or Doc snapshot; inspect references, missing fields, confidence and duplicate warnings.
4. Make the provider unavailable or use malformed/low-confidence content. Confirm a recoverable error, retained source and **Prepare manual gig draft**. Complete a row and commit only after accepting it.
5. Revoke Google Docs access. Confirm the URL remains available, connection explains the broad read-only permission, and explicit resource attachment still works without it. Attach a Sheet as a resource, choose **Go to gig**, and confirm no set-list interpretation starts automatically.
6. Repeat on mobile. Confirm the modal controls and source inspection remain usable, inline errors persist, and notifications do not cover modal controls.
7. With a controlled London reference date, submit an offer for “16th December” without a year. Confirm the next occurrence is proposed with an inferred-date warning, not an invented distant year. Repeat around New Year, with “this coming Sunday”, an ambiguous “next Saturday”, and an explicitly dated past gig. Check warnings and preserve the explicit year.
8. Force an unknown or failed analysis for text and a Google Doc URL. Confirm no receipt is preselected, **Gig import** remains available, and an unreadable document explains how to connect, paste text or upload a PDF without losing the link.
9. Exercise slow, malformed-output and provider-failure extraction with the configured test provider. Confirm preparation allows at least 90 seconds, reports an actionable safe failure, and retains retry/manual recovery. Confirm operational logs include the intake ID and safe category but never document text, URLs, credentials or raw provider responses.
10. Use a synthetic multi-date document with both valid and invalid section citations. Confirm valid rows show source excerpts, invalid citations are cleared and marked low confidence with a visible warning, and all structurally valid rows remain available for review. PDF page references must say they are not independently verified.

## Create A Staged Import Batch

> **Automation:** Backend automated; manual UAT: MCP/staging endpoint rules have backend coverage; creating a batch through a real MCP client or LLM harness remains manual.

### Steps

1. Use the MCP client to create a gig import batch with a recognisable source name, such as `Swing Into Christmas 2026`.
2. Add at least three draft rows:
   - one complete, high-confidence gig with client/contact match, title, date, venue, and fee
   - one incomplete gig with a missing client or missing date
   - one non-gig row that should be rejected
3. Include at least one informal date/time value, such as `Sat 28 Nov 2026` or `7:30 PM`, if using a conversational LLM client.

### Expected Results

The MCP call creates a staged batch without creating real gigs. Informal but parseable dates/times are normalised; vague values are left blank for review rather than failing the whole tool call.

## Notification And Modal Entry

> **Automation:** Automated UAT: `Glovelly.Uat.Tests.ImportedGigReviewTests.ImportedGigModalAutosavesAndCommitsReviewedRows` covers notification/menu entry using controlled staging setup; real MCP/LLM creation remains manual.

### Steps

1. Stay signed in after the import batch is created.
2. Wait up to 30 seconds, or refresh if testing without the polling path.
3. Confirm the profile avatar shows a red notification dot.
4. Open the profile menu.
5. Confirm `Imported gigs (N)` appears with a red dot.
6. Click `Imported gigs`.

### Expected Results

The import review modal opens from the profile menu. Imported gigs are not a primary navigation section. The count reflects pending plus accepted staged rows.

## Review And Autosave Rows

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.ImportedGigReviewTests.ImportedGigModalAutosavesAndCommitsReviewedRows` covers modal autosave persistence; backend tests cover duplicate warning refresh and duplicate warning presentation remains manual.

### Steps

1. Select the staged batch in the modal.
2. Edit a missing or incorrect field, such as client, title, type, date, location, fee, or source reference.
3. Wait briefly without clicking a save button.
4. Close and reopen the import modal, or select another batch and return.
5. Accept at least one valid row.
6. Reject at least one non-gig row.

### Expected Results

Draft edits autosave. There is no row-level `Save` button. Accepting or rejecting a row updates the batch counts immediately, but rejected rows do not jump to the bottom before commit.

## Duplicate Warning Review

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.GigImportEndpointsTests` covers duplicate warning rules and non-blocking commit behavior; modal presentation remains manual.

### Steps

1. Create or identify an existing gig on a known date and venue, such as `2026-12-08` at `Bath Forum`.
2. Stage an import row with the same date and venue.
3. Open `Imported gigs` and select the staged batch.
4. Confirm the row shows a possible duplicate warning before commit.
5. Edit the row to remove the duplicate signal, such as changing the date or venue, then wait for autosave or reload the batch.
6. Change the row back to the duplicate date and venue.
7. Accept the row and click `Commit decisions`.

### Expected Results

Duplicate rows show warning text in the review modal. Editing date, title, client, or venue refreshes the warning after autosave or reload. Duplicate warnings do not disable accepting the row and do not block commit; accepted rows still become real gigs when committed.

## Commit Decisions

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.ImportedGigReviewTests.ImportedGigModalAutosavesAndCommitsReviewedRows` covers browser accept/reject/commit workflow; `Glovelly.Api.Tests.GigImportEndpointsTests.CommitAcceptedRows_CreatesLinkedGigAndMarksDraftCommitted` and `CommitAcceptedRows_DeletesRejectedDraftsAndKeepsPendingRows` cover server rules.

### Steps

1. With one or more accepted rows and one or more rejected rows, click `Commit decisions`.
2. Open Gigs.
3. Find the gig or gigs created from accepted rows.
4. Return to the import modal.
5. Confirm rejected rows are gone from the batch.
6. Confirm any still-pending rows remain available for a later review pass.

### Expected Results

Accepted rows become real gigs with source import linkage and the reviewed gig type. Rejected rows are deleted from the import on commit. Pending rows remain staged. Previously rejected rows should not reappear or jump back into the review list on later passes.

## Validation And Multi-Pass Review

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.GigImportEndpointsTests.CommitSelectedRows_WithMissingRequiredFields_ReturnsValidationProblem` covers validation blocking; multi-pass modal workflow remains manual.

### Steps

1. Accept a row that is still missing a required field, such as client, title, date, or venue.
2. Click `Commit decisions`.
3. Fix the validation issue in the row.
4. Accept the row again if needed and click `Commit decisions`.
5. Repeat with a second slice of pending rows.

### Expected Results

The first commit attempt is blocked with clear validation feedback and creates no gig for the invalid row. After fixing the staged row, commit succeeds. Multi-pass review does not resurrect previously rejected rows, and already committed rows are visually handled and do not need further action.
