## Context

Glovelly currently uses `Gig` as the core entity for a discrete musician work item, but the stored model and user-facing workflows still assume live-performance language in several places. The existing `Gig` entity already carries the shared data needed by performances, teaching, rehearsals, recording sessions, admin work, and other work: client, title, date, venue/location, fee, status, invoice linkage, expenses, mileage, notes, and external resources.

The repository now has EF Core migration bundle infrastructure and an `InitialBaseline` migration registered for existing PostgreSQL databases. This change should therefore be implemented as the first ordinary post-baseline EF migration, not through startup schema mutation or manual SQL. Existing set-list workflows are allowed to remain available to all gig types because "set list" can reasonably apply to rehearsals and recording contexts as well as performances.

## Goals / Non-Goals

**Goals:**

- Keep `Gig` as the single domain entity and user-facing umbrella term.
- Add required type classification for gigs and import drafts with an explicit initial enum.
- Preserve existing performance behavior by backfilling/defaulting existing and inferred gigs to `Performance`.
- Make type visible and editable in the web UI, import review UI, REST API, and MCP contracts.
- Let users filter gigs by type in the web UI and MCP listing tools.
- Generate type-aware invoice fee descriptions.
- Use the established EF migrations project and migration bundle pipeline for schema changes.

**Non-Goals:**

- Rename `Gig` to `WorkItem`, `Engagement`, `Activity`, or another abstraction.
- Introduce inheritance, separate gig tables, or type-specific workflow subclasses.
- Add recurring gigs, teaching/student records, lesson plans, attendance, type-specific fields, custom gig types, or configurable metadata schemas.
- Restrict set-list resources/imports to performance gigs.
- Change Google Calendar event title/description behavior for this slice.
- Rename the persisted `Venue` property or overhaul API field names solely for wording cleanup.

## Decisions

### Add string-backed `GigType` enum values to `Gig` and import drafts

Add a backend enum with `Performance`, `Teaching`, `Rehearsal`, `Recording`, `Admin`, and `Other`. Store it with EF string conversion and a bounded length on both `Gig` and `GigImportDraft` (`ProposedGigType` or equivalent). Existing records and nullable/legacy creation paths default to `Performance`.

Rationale: string-backed enum columns match existing status storage, are readable in PostgreSQL, and avoid adding a parallel entity hierarchy.

Alternative considered: separate `PerformanceGig`/`TeachingGig` subclasses or a generic `WorkItem` rename. This is larger than the product need and would force invoice, expense, import, and MCP refactors without proven type-specific data requirements.

### Use a normal post-baseline EF migration

Generate a migration in `backend/Glovelly.Migrations` that adds the required columns, backfills existing rows to `Performance`, and updates the model snapshot. The web app must not apply this migration during startup.

Rationale: issues #197 and #198 established EF migrations as the schema history and deployment path; #196 is explicitly the first ordinary post-baseline schema change.

Alternative considered: manual SQL or startup backfill. That would bypass the new migration ledger and reintroduce the drift risk the migration work removed.

### Keep `Venue` as storage/API field, present it as location in generic UI

Continue using `Venue`/`venue` in persisted models and current API contracts, but update generic labels, placeholders, empty states, and search wording to prefer `Location` where not performance-specific.

Rationale: this solves the user-facing language problem without combining a type feature with a broad persisted-property rename.

Alternative considered: rename `Venue` to `Location` throughout the model/API. That would create unnecessary migration and compatibility churn for a copy-level concern.

### Add editable gig type to import drafts from the outset

Import drafts should carry the proposed type through REST DTOs, frontend review forms, MCP draft-add contracts, and commit mapping. If an importer or MCP client omits type, default the draft to `Performance`.

Rationale: import review is where users correct source interpretation before creating real gigs; forcing type correction after commit would make typed imports immediately incomplete.

Alternative considered: default all imports to `Performance` and edit after commit. That is smaller but likely creates a follow-up task immediately and weakens the import review workflow.

### Generate type-aware fee descriptions

Invoice line generation derives the fee description from gig type, title, and date.

Default generated descriptions:

- `Performance fee for {Title} ({Date})`
- `Teaching fee for {Title} ({Date})`
- `Rehearsal fee for {Title} ({Date})`
- `Recording fee for {Title} ({Date})`
- `Admin fee for {Title} ({Date})`
- `Fee for {Title} ({Date})` for `Other`

Rationale: type-aware descriptions cover the immediate terminology need without introducing partial invoice-line editing. Invoice-native editing is deferred to a separate roadmap item so its regeneration semantics can be designed coherently.

### Do not restrict set-list workflows by gig type

Leave `GigExternalResourcePurpose.SetList`, set-list import endpoints, set-list chart matching, and set-list export available for every type.

Rationale: rehearsals and recording sessions can have set lists in ordinary musician workflows, and the user explicitly prefers keeping the behavior available.

Alternative considered: show/allow set-list only for `Performance`. This is simpler conceptually but incorrectly narrows the workflow.

### Extend MCP read and staged-import contracts

Expose type in MCP gig summary/detail responses, add optional type filtering to gig listing and uninvoiced gig listing, and allow staged gig import drafts created through MCP to include type. Preserve the existing MCP visibility model and staged-write safety posture.

Rationale: MCP consumers need the same type information as the UI, and import tools should not require an immediate follow-up to correct type.

Alternative considered: limit this change to REST/web and defer MCP. That would make typed gigs incomplete for existing agent-assisted workflows.

### Leave calendar output unchanged

Do not add type to Google Calendar event titles or descriptions in this slice.

Rationale: gig names are expected to be descriptive enough, and calendar changes are not needed to solve the issue.

Alternative considered: add `Type: ...` to calendar descriptions. This adds visible calendar churn without a clear user benefit.

## Risks / Trade-offs

- Existing API clients may omit gig type on direct create/update requests → default only known legacy/inferred paths where practical, validate normal user-facing requests, and cover behavior with tests.
- Type and status may be confused in UI copy → label type as nature of work and status as lifecycle, and keep `Confirmed` displayed as planned where already established.
- Generated invoice line descriptions could change existing performance expectations → preserve the performance default wording and add regression tests for invoice generation.
- Import/MCP contract changes touch several layers → update schemas, snapshots/docs, DTOs, and integration tests together.
- Adding non-null columns to live databases can fail without defaults/backfill → implement the EF migration with safe defaults/backfill and validate through the migration chain CI.

## Migration Plan

1. Add enum/model/configuration changes and generate a normal EF migration in `backend/Glovelly.Migrations` after `InitialBaseline`.
2. Migration adds gig type columns to gigs and import drafts, backfills gig and draft type values to `Performance`, and enforces non-null where required.
3. CI validates pending model changes, applies the migration chain to a fresh PostgreSQL database, and verifies no-op reruns through the existing migration checks.
4. Deployment runs the packaged migration bundle against staging before service deployment, then against production after staging UAT and production approval.
5. Rollback follows the established database posture: restore from backup or deploy a forward-fix migration rather than running automatic `Down()` migrations.
