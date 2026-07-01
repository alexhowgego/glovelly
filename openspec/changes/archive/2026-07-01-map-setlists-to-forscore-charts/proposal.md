## Why

Glovelly can now import read-only forScore library snapshots, but imported gig set lists still only contain text rows from Google Sheets. The next useful step is to connect each set list song to the matching forScore chart so Glovelly can identify missing/ambiguous chart links now and later drive generated forScore set lists.

## What Changes

- Add shared set list chart matching that compares set list song rows against the user's active forScore library snapshot.
- Augment new Google Sheet set list import review with forScore chart match status, suggested candidates, and manual chart selection before save.
- Add retroactive matching for existing active gig set lists after a user imports their forScore library.
- Persist confirmed chart mappings on saved set list items, including snapshot-local chart identifiers plus copied chart title and file path for explainability after later library imports.
- On new forScore library snapshot import, assess the blast radius across active set lists for Draft and Confirmed future gigs, auto-relink exact file path matches, and mark ambiguous or missing links for user review.
- Use friendly user wording such as "needs review", "missing from latest library", and "linked from older library" instead of technical stale-state language.

## Capabilities

### New Capabilities

- `setlist-chart-matching`: Matching, reviewing, persisting, and repairing links from gig set list song items to forScore library charts.

### Modified Capabilities

- `forscore-library-snapshot`: New snapshot imports assess mapped set lists that may drift from previously selected forScore charts and report review work created by the import.

## Impact

- Backend data model: add mapping fields/status to `GigSetListItem` and indexes/relationships to forScore snapshots/charts where appropriate.
- Backend services: add a shared matcher and library import impact assessment/relink service.
- Backend APIs: enrich set list import preview/save/update responses and add endpoints for retroactive match preview/apply or mapping review.
- Frontend: extend set list import and existing set list review UI with match status, candidate selection, search/manual fix-up, and post-library-import affected set list guidance.
- Tests: add matcher unit tests, endpoint integration tests for both journeys, snapshot import impact tests, and frontend lint/build verification.
- Data operations: add the one-time PostgreSQL script needed for new persisted mapping fields because this repo does not currently use committed EF migrations.
