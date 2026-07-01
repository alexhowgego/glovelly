## 1. Data Model And Persistence

- [x] 1.1 Add set list item mapping fields for forScore chart id, library snapshot id, copied chart title, copied chart file path, mapping status, mapping confidence/source, and mapping update timestamp.
- [x] 1.2 Add EF Core configuration, relationships, delete behavior, and indexes for set list item chart mapping and impacted set list lookup.
- [x] 1.3 Add a one-time PostgreSQL script under `scripts/manual/` for the new nullable mapping columns and indexes.
- [x] 1.4 Update backend response/request DTOs and frontend TypeScript types to include chart mapping status, selected chart identity, and match candidates.

## 2. Shared Matcher

- [x] 2.1 Implement a shared set list chart matcher that accepts draft or saved set list song inputs and compares them with the user's active forScore library snapshot.
- [x] 2.2 Add conservative scoring for exact file path, exact normalized title, filename/title matches, and ambiguous/missing results.
- [x] 2.3 Return match result status, confidence, selected chart where applicable, candidate list, and user-friendly reason text.
- [x] 2.4 Ensure non-song rows and users without an active forScore library return safe not-applicable/no-library match states without blocking set list review.

## 3. New Set List Import Journey

- [x] 3.1 Enrich the Google Sheet set list preview endpoint with forScore match results when an active library snapshot exists.
- [x] 3.2 Accept selected, cleared, or unchanged chart mapping decisions when saving a new set list import.
- [x] 3.3 Validate saved chart mappings are owned by the authenticated user and belong to the user's active forScore library snapshot.
- [x] 3.4 Update the set list import modal to show mapping summary counts, row-level match chips, candidates, manual chart selection, and issue-focused review.

## 4. Existing Set List Mapping Journey

- [x] 4.1 Add backend support to generate match suggestions for an existing active gig set list without re-reading or replacing the Google Sheet import.
- [x] 4.2 Add backend support to save mapping decisions on existing set list items while preserving row content and ordering.
- [x] 4.3 Update the existing set list review modal to show current chart links, older-library links, missing/latest-library review states, and manual fix-up controls.
- [x] 4.4 Ensure retroactive matching and save operations publish the existing gig workspace update events where appropriate.

## 5. Library Snapshot Import Impact

- [x] 5.1 Extend successful forScore snapshot import to find active set lists on Draft and Confirmed future or undated gigs with mappings from an older snapshot.
- [x] 5.2 Auto-relink mapped set list items to charts in the new active snapshot when an exact file path match exists.
- [x] 5.3 Mark ambiguous or missing rematches as needing review while preserving the prior copied chart title and file path for display.
- [x] 5.4 Return or expose a post-import impact summary with affected set list count, auto-relinked item count, and items needing review using friendly wording.
- [x] 5.5 Update the connected services forScore import UI to guide users to affected set lists after importing a library snapshot.

## 6. Tests And Verification

- [x] 6.1 Add matcher unit tests for exact title, exact file path, ambiguous, missing, non-song, and no-active-library cases.
- [x] 6.2 Add endpoint tests for new set list import preview/save with chart mappings and cross-user mapping rejection.
- [x] 6.3 Add endpoint tests for retroactive existing set list match preview/save without replacing the set list import.
- [x] 6.4 Add snapshot import impact tests for Draft/Confirmed gig inclusion, exact file path auto-relink, ambiguous/missing review marking, and Completed/Cancelled exclusion.
- [x] 6.5 Update or add UAT documentation for new import-time matching and retroactive mapping journeys.
- [x] 6.6 Run `dotnet test glovelly.sln -m:1`, `npm --prefix frontend/glovelly-web run lint`, and `npm --prefix frontend/glovelly-web run build`.
