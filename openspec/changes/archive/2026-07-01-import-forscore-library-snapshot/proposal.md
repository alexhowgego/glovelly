## Why

Glovelly can already import gig set lists, but it does not yet know which charts exist in a user's forScore library. Importing a read-only forScore library snapshot gives Glovelly a reliable catalogue of chart titles and file paths to support deterministic set list matching and later `.4ss` export generation.

## What Changes

- Add a user-owned forScore library snapshot import capability for `.4sb` files exported by forScore.
- Parse `.4sb` files as a read-only interoperability input: detect the wrapped gzip payload, parse the Apple binary plist, and extract chart metadata.
- Store imported chart records in a normalized snapshot model rather than merging into a long-lived catalogue.
- Mark the latest successful snapshot as active for the importing user and supersede previous active snapshots.
- Ignore existing forScore set lists, annotations, system settings, and binary assets contained in the backup.
- Surface clear import outcomes, including chart counts, parse failures, and non-fatal warnings where applicable.

## Capabilities

### New Capabilities
- `forscore-library-snapshot`: Import a user-supplied forScore `.4sb` library export into normalized chart records for later set list matching.

### Modified Capabilities
- None.

## Impact

- Backend data model: new user-owned forScore library snapshot and chart entities, EF configuration, and migration.
- Backend services: new parser/import workflow for read-only `.4sb` ingestion and chart normalization.
- Backend API: authenticated endpoints for uploading a snapshot and viewing import status/chart records.
- Frontend: upload/review UI for forScore library snapshots, likely under a settings or set list matching area.
- Tests: parser coverage for `.4sb` wrapper handling, import workflow integration tests, ownership/visibility checks, and frontend build/lint coverage.
