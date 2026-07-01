## 1. Data Model

- [x] 1.1 Add backend models for forScore library snapshots and imported chart records with user ownership, active snapshot state, import metadata, and chart metadata fields.
- [x] 1.2 Add EF Core configuration, indexes, relationships, and DbSet registrations for snapshots and chart records.
- [x] 1.3 Add an additive database migration for the new snapshot and chart tables.

## 2. Parser And Import Workflow

- [x] 2.1 Implement a `.4sb` parser that locates the gzip payload by magic bytes, decompresses it, and parses the binary plist.
- [x] 2.2 Extract chart records from `<file path>|<field>` plist metadata and ignore `&SET;`, `&SYS;`, annotations, and binary assets.
- [x] 2.3 Implement chart normalization for imported titles and record non-fatal warnings for skipped incomplete chart metadata.
- [x] 2.4 Implement an import service that stores a successful snapshot, imports chart records, marks it active, and deactivates the user's previous active snapshot atomically.

## 3. Backend API

- [x] 3.1 Add authenticated endpoints to upload a `.4sb` snapshot and retrieve the current user's active snapshot metadata.
- [x] 3.2 Add an authenticated endpoint or response shape for listing chart records from the active snapshot.
- [x] 3.3 Enforce user-scoped visibility so users cannot access other users' snapshots or chart records.
- [x] 3.4 Add validation responses for malformed, unsupported, empty, or oversized imports without replacing the active snapshot.

## 4. Frontend UI

- [x] 4.1 Add TypeScript types and API helpers for forScore library snapshot status and chart records.
- [x] 4.2 Add UI for uploading a forScore `.4sb` library export and showing import status, chart count, and warnings.
- [x] 4.3 Surface the active snapshot in the relevant settings or set list workflow area without exposing ignored set lists as importable content.

## 5. Tests And Verification

- [x] 5.1 Add parser tests covering valid `.4sb` wrapper parsing, variable gzip offset handling, invalid files, ignored set lists, and skipped incomplete chart metadata.
- [x] 5.2 Add integration tests for successful upload, active snapshot replacement, failed import preserving the active snapshot, and user ownership isolation.
- [x] 5.3 Add or update frontend checks for the upload/status UI and run frontend lint/build.
- [x] 5.4 Run backend tests with `dotnet test glovelly.sln -m:1` and frontend checks with `npm --prefix frontend/glovelly-web run lint` and `npm --prefix frontend/glovelly-web run build`.
