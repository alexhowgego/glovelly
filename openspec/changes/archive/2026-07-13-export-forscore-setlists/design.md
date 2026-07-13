## Context

Glovelly already lets authenticated users reach a reviewed gig set list whose included song rows are mapped to charts from their active uploaded forScore library snapshot. Saved set list items already persist the selected forScore chart id, chart title, and chart file path.

forScore supports an open `.4ss` setlist format consisting of UTF-8 XML with a `<forScore kind="setlist" version="1.0">` root and ordered `<score title="..." path="..." />` entries. The `path` attribute is resolved by forScore against files already present in the user's forScore library, which aligns with the saved `ForScoreChartFilePath` value.

## Goals / Non-Goals

**Goals:**
- Export the active reviewed gig set list as a downloadable `.4ss` file.
- Preserve saved set list order for included song rows.
- Use saved forScore chart paths so the exported file resolves against the user's existing forScore library.
- Prevent export when any included song row has no selected forScore chart.
- Keep the frontend export affordance simple: enable it only when all included song rows are mapped.

**Non-Goals:**
- Do not change AI generation, Google Sheet import, or chart matching workflows.
- Do not include PDFs, base64 score data, annotations, or metadata in the `.4ss` file.
- Do not create a live synchronization integration with forScore.
- Do not add database schema changes unless implementation discovers an existing field is insufficient.
- Do not require a direct forScore deep link for import; browser download/share is the reliable first path.

## Decisions

1. Export from the saved active import state.

   The endpoint should export the active `GigSetListImport` for the requested gig rather than accepting arbitrary draft rows. This ensures the file reflects the reviewed and saved state, and lets backend authorization and validation operate on persisted data.

   Alternative considered: export unsaved frontend draft rows. That would allow immediate export after local edits but risks exporting state that is not persisted, bypasses existing chart validation, and complicates auditability.

2. Treat selected chart path as the export contract.

   For each included song row, write a `<score>` element using the saved `ForScoreChartFilePath` as `path` and the saved `ForScoreChartTitle` as `title`, falling back to the set list item title only if needed for a human-readable placeholder title.

   Alternative considered: re-query charts by `ForScoreChartId` only. The saved denormalized path is already the exact compatibility value from the active library at review time, and it is what existing responses expose to the frontend. Re-querying can still be useful for validation if needed, but it should not change the exported ordering or intended selected path.

3. Block incomplete exports.

   If any included song row lacks a selected chart id or chart file path, the backend should reject export and return a client-actionable response listing the missing rows. The frontend button should mirror this by being disabled until all included song rows have `forScoreChartId`.

   Alternative considered: emit `<placeholder>` elements for unmapped songs. forScore supports placeholders, but issue 177 requires missing matches to be surfaced before export and the desired journey is a fully mapped set list.

4. Return a normal file download.

   The backend should return XML with a `.4ss` attachment filename. The frontend should fetch the endpoint and trigger a browser download. On iPad, the user can open/share the downloaded file into forScore using the platform's normal file handling.

   Alternative considered: direct forScore URL scheme import. forScore's URL scheme can open existing scores or setlists, but it does not appear to import a generated remote `.4ss` file from a web app. Download/share is more reliable.

## Risks / Trade-offs

- forScore path matching depends on exact filenames in the user's device library → Use the path captured from the user's uploaded `.4sb` library snapshot and preserve casing exactly.
- XML escaping bugs could create invalid exports → Generate XML with framework XML APIs or explicit escaping covered by tests.
- Browser download behavior varies on iPad → Keep the response as a standard attachment and present copy explaining that the file should be opened/imported in forScore.
- Saved chart paths can become stale if the user changes their forScore library after review → Existing chart matching/review flow already surfaces library changes; export should block only missing selected paths, not attempt to silently rematch.
