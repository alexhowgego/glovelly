# Imported Gig UAT Journeys

Use these journeys when a change may affect MCP gig extraction, staged import batches, imported gig review, notification dots, or committing imports into real gigs.

## Preconditions

- You can sign in to Glovelly.
- At least one client exists that can be matched or selected for imported rows.
- The MCP client or local test harness can call the gig import tools.
- If testing locally, the frontend dev proxy includes `/gig-imports`; otherwise the import modal can fail to load behind the Vite shell.

## Create A Staged Import Batch

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

### Steps

1. Select the staged batch in the modal.
2. Edit a missing or incorrect field, such as client, title, date, venue, fee, or source reference.
3. Wait briefly without clicking a save button.
4. Close and reopen the import modal, or select another batch and return.
5. Accept at least one valid row.
6. Reject at least one non-gig row.

### Expected Results

Draft edits autosave. There is no row-level `Save` button. Accepting or rejecting a row updates the batch counts immediately, but rejected rows do not jump to the bottom before commit.

## Commit Decisions

### Steps

1. With one or more accepted rows and one or more rejected rows, click `Commit decisions`.
2. Open Gigs.
3. Find the gig or gigs created from accepted rows.
4. Return to the import modal.
5. Confirm rejected rows are gone from the batch.
6. Confirm any still-pending rows remain available for a later review pass.

### Expected Results

Accepted rows become real gigs with source import linkage. Rejected rows are deleted from the import on commit. Pending rows remain staged. Previously rejected rows should not reappear or jump back into the review list on later passes.

## Validation And Multi-Pass Review

### Steps

1. Accept a row that is still missing a required field, such as client, title, date, or venue.
2. Click `Commit decisions`.
3. Fix the validation issue in the row.
4. Accept the row again if needed and click `Commit decisions`.
5. Repeat with a second slice of pending rows.

### Expected Results

The first commit attempt is blocked with clear validation feedback and creates no gig for the invalid row. After fixing the staged row, commit succeeds. Multi-pass review does not resurrect previously rejected rows, and already committed rows are visually handled and do not need further action.
