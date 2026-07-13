## 1. Backend Export

- [x] 1.1 Add a focused forScore `.4ss` export service or helper that generates UTF-8 XML from a gig title and ordered mapped set list items.
- [x] 1.2 Add authenticated active-setlist export endpoint under the gig set list routes.
- [x] 1.3 Load only gigs visible to the current user and the gig's active saved set list with items.
- [x] 1.4 Validate that at least one included song row exists and every included song row has a selected forScore chart id and file path.
- [x] 1.5 Return client-actionable validation/conflict details for included song rows that cannot be exported.
- [x] 1.6 Return the generated XML as a `.4ss` attachment with a safe filename derived from the gig or set list title.

## 2. Backend Tests

- [x] 2.1 Add endpoint tests for successful export of a visible gig's active set list.
- [x] 2.2 Verify exported `<score>` entries include only included song rows and preserve saved sort order.
- [x] 2.3 Verify exported score paths preserve the saved forScore chart file paths exactly.
- [x] 2.4 Verify XML-sensitive titles and paths produce well-formed XML.
- [x] 2.5 Verify export is rejected for inaccessible gigs, missing active set lists, empty included song rows, and unmapped included song rows.

## 3. Frontend Export UX

- [x] 3.1 Add an `Export forScore .4ss` action to the reviewed set list modal.
- [x] 3.2 Enable the export action only when the modal has at least one included song row and every included song row has `forScoreChartId`.
- [x] 3.3 Fetch the export endpoint, create a browser download from the returned blob, and preserve the server-provided filename when available.
- [x] 3.4 Surface backend validation errors when export is blocked, including guidance to select charts for remaining rows.
- [x] 3.5 Add concise iPad-friendly copy indicating that the downloaded `.4ss` file can be opened/imported in forScore.

## 4. Verification

- [x] 4.1 Run targeted backend tests for set list import/export behavior.
- [x] 4.2 Run frontend lint and build checks.
- [x] 4.3 Manually compare a generated `.4ss` file against the sample shape from `Bella 6-4-26.4ss`.
