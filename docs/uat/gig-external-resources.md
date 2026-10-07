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

## Add To Glovelly Resource Journey

> **Automation:** `Glovelly.Uat.Tests.UploadAndQuickCaptureWorkflowTests.UnifiedResourceMobileFlowKeepsUploadOpenAndOffersOptionalReview` covers the mobile URL upload/application and optional saved-record review in Ubuntu CI. Backend tests cover explicit resource confirmation, source retention/cleanup, URL type inference, moves and primary updates.

### Steps

1. On a phone-sized viewport, select the single **Add to Glovelly** action.
2. Confirm source type selection appears; choose a file, URL or pasted text and enter unified upload before acquisition. Repeat with a Google Doc or Google Sheet URL.
3. Confirm analysis/type inference happens in upload before any resource is created. Linked Google document contents are not fetched.
4. Confirm upload shows detected facts/confidence, suggested gigs, title, type, purpose, notes and primary controls. Choose a gig and explicitly **Attach resource**; nearby gigs must never cause automatic resource creation.
5. Confirm upload remains open with compact **Attached to [Gig]**, **Review attachment**, and **Done**. Finish without review and verify the resource in its gig; repeat and deliberately open saved-attachment review to edit/reassign/delete that exact resource without re-uploading.
6. With several valid historical gigs outside the nearby window, use `Load more gigs` until the intended gig is available. Confirm the current selection and submitted source remain unchanged while loading and after a failed request; retry the request and confirm it recovers.
7. Continue until `Load more gigs` is no longer available, then select an older gig and explicitly apply the resource.

### Expected Results

The unified journey supports files, URLs and text, infers Google Doc/Sheet types from URL metadata, and requires explicit resource confirmation. Automatic receipts and explicit resources share the same stable upload attached state and optional saved review. Inline save/failure feedback remains visible with notifications below modal overlays, and controls remain usable on mobile. Closing an unapplied source preserves recovery; closing after confirmed attachment preserves the saved resource.
Candidate expansion never saves or moves a draft by itself, keeps current options visible during loading, allows retry after an error, and eventually exposes visible non-cancelled historical gigs without duplicate options.

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

The app allows an attachment without a URL, stores uploaded file metadata, downloads the same file, and removes the file without deleting the attachment itself. Completed file actions show terminal notifications; unavailable stored files show a persistent, user-safe error notification.

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
4. Choose the worksheet/tab and click `Interpret set list`.
5. Confirm the modal shows queued/running AI interpretation progress without holding a long browser request open, then review the interpreted draft.
6. Confirm supported songs appear as included rows, medley/section headings and transitions appear as non-song review items, and singers, instruments, keys, durations, and rehearsal notes are not promoted to song titles.
7. Confirm spare or otherwise excluded sections remain visible but are excluded by default.
8. If interpretation fails, confirm the safe failure message offers retry and `Create manual draft`; confirm manual authoring starts empty beside the retained source worksheet grid and does not pre-parse rows.
9. Confirm song rows show forScore chart status such as suggested, choose chart, missing from latest library, or no library only after an interpreted or manual draft exists.
10. Confirm common title variants such as `LOVE`/`L-O-V-E` and `Jump Jive & Wail`/`Jump Jive And Wail` appear as plausible chart matches when present in the library.
11. Confirm rows with chart numbers such as `61-E`, `17`, or `104` prefer chart-number candidates over title-only candidates, while ambiguous or nearby-number-only candidates still require review.
12. Click `Ask AI to choose`, confirm the modal shows queued/running progress without a long blocking browser request, then confirm completed AI choices apply to matching rows.
13. Expand a song row, choose or clear the forScore chart, adjust title/pad/key/section/notes, and save the import.
14. Re-open the attachment and click `Review set list`.
15. Click `Ask AI to choose`, confirm existing rows can be matched without re-importing the Google Sheet, and save a chart mapping change.
16. Re-run `Interpret set list` and confirm replacing the active import requires confirmation and preserves historical imports.

### Expected Results

AI interpretation receives the selected worksheet as a bounded coordinate-preserving display grid, then produces a reviewable set-list draft with source evidence and confidence. Imported setlists preserve interpreted source order and primary source rows; separators, transitions, comments, and excluded material are retained for audit but are not included as songs. AI interpretation and optional AI chart matching are recoverable background jobs using SignalR/polling; interpretation failure retains the source grid for retry or manual draft authoring. Deterministic candidate lookup only runs after a valid interpreted or manual draft exists. Chart mappings are saved only for selected song rows, show copied forScore title/path context, and can be reviewed later without replacing the set list import. Reviewing and saving edits updates the active import without changing the linked Google Sheet. Re-importing creates a new active snapshot only after explicit confirmation.

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
