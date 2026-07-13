# Gig Attachment UAT Journeys

## Purpose

Use these journeys when a change may affect gig attachments such as set lists, gig plans, contracts, travel notes, URLs, or uploaded reference files.

## Preconditions

- You can sign in.
- At least one client and one saved gig exist.
- You have a small PDF or image available if testing file attachments.
- Seeded local and UAT environments should include at least one gig with attachments.

## Add Attachment Journey

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.UploadAndQuickCaptureWorkflowTests.BrowserReceiptAndAttachmentUploadsRoundTripThroughGigUi` covers browser file-only attachment creation/upload/delete; backend tests cover URL validation, resource scoping, and primary rules.

### Steps

1. Open Gigs and select a saved gig.
2. In `Attachments`, click `Add attachment`.
3. Create a `Set list` attachment with a title and valid URL, and mark it primary.
4. Confirm the modal closes and the attachment appears on the gig detail panel with a primary badge.
5. Click `Open` and confirm the link opens in a new tab.
6. Edit the attachment, change its title or notes, and save.

### Expected Results

The attachment is added to the selected gig only, appears without refreshing, preserves type and purpose, and opens external links in a separate tab.

## Quick Add Attachment Journey

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.UploadAndQuickCaptureWorkflowTests.QuickAttachmentMobileFlowSavesDraftAndOpensTargetGig` covers the mobile-sized quick attachment link flow; backend tests cover file draft matching, no-candidate handling, type inference, moves, and primary updates.

### Steps

1. Scroll on a phone-sized viewport and confirm the `+` quick attachment button floats beside `Scan receipt`.
2. Click `+` and choose `Upload file`.
3. Upload a PDF or image when a gig exists within the quick capture window.
4. Confirm the attachment is saved to the nearest gig and the modal shows editable title, type, purpose, URL, notes, and primary fields.
5. Save details, then click `Go to gig` and confirm the attachment appears in the gig detail panel.
6. Click `+` again, choose `Add link`, paste a Google Doc or Google Sheet URL, and save.

### Expected Results

The quick add journey uses the same gig matching behaviour as quick receipts, supports both uploaded files and URLs, infers Google Doc/Sheet types where possible, and preserves user-facing attachment terminology.

## File-Only Attachment Journey

> **Automation:** Partially automated UAT: `Glovelly.Uat.Tests.UploadAndQuickCaptureWorkflowTests.BrowserReceiptAndAttachmentUploadsRoundTripThroughGigUi` covers browser file-picker upload/delete and attachment-shell preservation; backend tests cover download/storage rules.

### Steps

1. Select a saved gig.
2. Click `Add attachment`.
3. Create a `Contract` or `Other` attachment with a title and no URL.
4. Upload a PDF or image file to the attachment from the detail panel.
5. Download the uploaded file.
6. Delete the uploaded file.

### Expected Results

The app allows an attachment without a URL, stores uploaded file metadata, downloads the same file, and removes the file without deleting the attachment itself.

## Primary Resource Behaviour

> **Automation:** Backend automated; manual UAT

### Steps

1. Add two `Set list` attachments to the same gig.
2. Mark the second one primary.
3. Add or edit a `Gig plan` attachment and mark it primary.

### Expected Results

Only one `Set list` attachment is primary for the gig. The primary `Gig plan` remains primary because primary status is scoped by gig and purpose.

## Set List Import Journey

> **Automation:** Backend automated; manual UAT: `SetListImportEndpointsTests`, `SetListChartMatcherTests`, `SetListChartMatchJobProcessorTests`, and `SetListSheetParserTests` cover source parsing, deterministic forScore chart matching, asynchronous AI chart matching jobs, review save, re-import history, and edit persistence. Browser OAuth and review modal flow remain manual.

### Preconditions

- Google Sheets is connected from the profile `Services` menu, or the tester is ready to connect it from the import modal.
- A forScore `.4sb` library snapshot has been imported from the profile `Services` menu when testing chart matching.
- A gig has a primary `Set list` attachment whose type is `Google Sheet` and whose URL points to a Google Sheets spreadsheet the connected Google account can read.

### Steps

1. Open Gigs and select the gig with the primary Google Sheet set list attachment.
2. Expand the attachment and click `Import set list`.
3. If Google Sheets is not connected, click `Connect Google Sheets`, complete OAuth, and return to the gig.
4. Choose the worksheet/tab and click `Import rows`.
5. Confirm likely songs appear as included rows and non-song headings/instructions appear as greyed review notes.
6. Confirm song rows show forScore chart status such as suggested, choose chart, missing from latest library, or no library.
7. Confirm common title variants such as `LOVE`/`L-O-V-E` and `Jump Jive & Wail`/`Jump Jive And Wail` appear as plausible chart matches when present in the library.
8. Confirm rows with chart numbers such as `61-E`, `17`, or `104` prefer chart-number candidates over title-only candidates, while ambiguous or nearby-number-only candidates still require review.
9. Click `Ask AI to choose`, confirm the modal shows queued/running progress without a long blocking browser request, then confirm completed AI choices apply to matching rows.
10. Expand a song row, choose or clear the forScore chart, adjust title/pad/key/section/notes, and save the import.
11. Re-open the attachment and click `Review set list`.
12. Click `Ask AI to choose`, confirm existing rows can be matched without re-importing the Google Sheet, and save a chart mapping change.
13. Re-run `Import set list` and confirm replacing the active import requires confirmation and preserves historical imports.

### Expected Results

Imported setlists preserve source worksheet order and row numbers. Separators/comments are retained for audit but are not included as songs. Importing rows locates deterministic chart candidates immediately; optional AI matching runs as a recoverable background job, uses SignalR/polling for completion, and keeps deterministic/manual choices available if AI is pending or fails. Chart mappings are saved only for selected song rows, show copied forScore title/path context, and can be reviewed later without replacing the set list import. Match chips and reasons distinguish chart-number matches from title-similarity matches where possible. Reviewing and saving edits updates the active import without changing the linked Google Sheet. Re-importing creates a new active snapshot only after explicit confirmation.

## forScore Library Drift Journey

> **Automation:** Backend automated; manual UAT: `ForScoreLibraryEndpointsTests.Upload_NewSnapshotRelinksMappedUpcomingDraftAndConfirmedSetLists` covers auto-relink and review marking after library replacement.

### Steps

1. Import a forScore `.4sb` library snapshot.
2. Map at least one chart on an active set list for a Draft or Confirmed future gig.
3. Import a newer `.4sb` snapshot where one mapped chart keeps the same file path and another mapped chart is absent or ambiguous.
4. Read the profile `Services` forScore library card status.
5. Open the affected gig's reviewed set list and click `Check forScore matches`.

### Expected Results

The library import succeeds and is not blocked by existing chart links. The services card explains that set lists have chart links needing review when applicable. Exact file path matches are updated automatically; missing or ambiguous chart links remain visible with prior chart context and can be fixed from the reviewed set list.

## Negative Checks

1. Try to save an attachment with a blank title.
2. Try to save an attachment with an invalid URL such as `not-a-url`.
3. Delete an attachment and decline the confirmation prompt.
4. Delete it again and accept the prompt.

Expected result: validation messages are clear, invalid links are rejected, declined deletion leaves data unchanged, and accepted deletion removes the attachment and any attached files.
