# Gig External Resource UAT Journeys

## Purpose

Use these journeys when a change may affect linked gig resources such as set lists, gig plans, contracts, travel notes, URLs, or uploaded reference files.

## Preconditions

- You can sign in.
- At least one client and one saved gig exist.
- You have a small PDF or image available if testing file attachments.
- Seeded local and UAT environments should include at least one gig with linked external resources.

## Link External Resource Journey

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.GigEndpointsTests.CreateGigExternalResource_AddsResourceToGig`, `CreateGigExternalResource_WithoutUrl_CreatesFileOnlyResourceShell`, `CreateGigExternalResource_WithInvalidUrl_ReturnsValidationProblem`, and `UpdateGigExternalResource_UpdatesFieldsAndPrimaryForPurposeOnly` cover server-side rules; browser modal flow remains manual.

### Steps

1. Open Gigs and select a saved gig.
2. In `External resources`, click `Link resource`.
3. Create a `Set list` resource with a title and valid URL, and mark it primary.
4. Confirm the modal closes and the resource appears on the gig detail panel with a primary badge.
5. Click `Open` and confirm the link opens in a new tab.
6. Edit the resource, change its title or notes, and save.

### Expected Results

The resource is linked to the selected gig only, appears without refreshing, preserves type and purpose, and opens external links in a separate tab.

## File-Only Resource Journey

> **Automation:** Backend automated; manual UAT: `Glovelly.Api.Tests.GigEndpointsTests.UploadGigExternalResourceAttachment_AddsDownloadableAttachment` covers upload, download, and delete through the API; browser file-picker flow remains manual.

### Steps

1. Select a saved gig.
2. Click `Link resource`.
3. Create a `Contract` or `Other` resource with a title and no URL.
4. Upload a PDF or image file to the resource from the detail panel.
5. Download the uploaded file.
6. Delete the uploaded file.

### Expected Results

The app allows a resource without a URL, stores uploaded file metadata, downloads the same file, and removes the file without deleting the resource itself.

## Primary Resource Behaviour

> **Automation:** Backend automated; manual UAT

### Steps

1. Link two `Set list` resources to the same gig.
2. Mark the second one primary.
3. Link or edit a `Gig plan` resource and mark it primary.

### Expected Results

Only one `Set list` resource is primary for the gig. The primary `Gig plan` remains primary because primary status is scoped by gig and purpose.

## Negative Checks

1. Try to save a resource with a blank title.
2. Try to save a resource with an invalid URL such as `not-a-url`.
3. Delete a linked resource and decline the confirmation prompt.
4. Delete it again and accept the prompt.

Expected result: validation messages are clear, invalid links are rejected, declined deletion leaves data unchanged, and accepted deletion removes the resource and any attached files.
