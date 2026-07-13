## Why

Users can already import or generate a gig set list, review it, and map included song rows to charts from their uploaded forScore library. The remaining gap is exporting that reviewed/mapped set list into forScore's open `.4ss` XML format so it can be downloaded and imported on the user's device.

## What Changes

- Add a forScore setlist export for a gig's active reviewed set list.
- Generate `.4ss` XML containing included song rows in saved set list order.
- Use the selected forScore chart title and file path already stored on each mapped set list item.
- Block export until every included song row has a selected forScore chart, surfacing the rows that still need selection.
- Add a frontend export action that is enabled only when all included song rows are mapped.
- Keep import/generation/chart-matching journeys unchanged; this change only exports the existing reviewed/mapped state.

## Capabilities

### New Capabilities
- `forscore-setlist-export`: Export a reviewed and fully mapped gig set list as a downloadable forScore `.4ss` file.

### Modified Capabilities

## Impact

- Backend API: add an authenticated export endpoint under the gig set list import routes.
- Backend services: add focused `.4ss` XML generation and filename handling.
- Backend tests: cover export XML, ordering, access control, and unmapped-row validation.
- Frontend: add an export button to the reviewed set list modal and download the returned `.4ss` file.
- No database schema changes are expected.
- No changes are expected to existing AI chart matching or set list import behavior.
