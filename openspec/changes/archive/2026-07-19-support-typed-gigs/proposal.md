## Why

Early musician feedback shows that Glovelly currently presents every gig as though it were a live performance, which makes teaching, rehearsal, recording, preparation, and admin work feel awkward or disguised. The existing `Gig` model already supports the shared workflow shape, so this change adds explicit gig typing without renaming or refactoring the core domain entity.

## What Changes

- Add a required gig type to the existing `Gig` entity with initial values `Performance`, `Teaching`, `Rehearsal`, `Recording`, `Admin`, and `Other`.
- Add a post-baseline EF Core migration that stores gig type for gigs and import drafts, backfilling existing gigs and staged drafts to `Performance`.
- Let users choose and edit gig type in normal gig forms and gig import draft review before commit.
- Default calendar/import/quick-capture/MCP-created draft paths to `Performance` when no explicit type is available.
- Show gig type in gig lists, detail panels, import drafts, and MCP gig/draft responses.
- Add gig type filtering to the main gigs UI and MCP gig listing tools.
- Keep all gig types on the same fee, invoice, expense, mileage, notes, external-resource, tax-summary, and receipt flows.
- Generate type-aware invoice fee line descriptions.
- Keep set-list resource and import functionality available for all gig types; do not restrict set-list workflows to performances.
- Review generic gig-related UI copy so it no longer implies that all gigs are performances, while preserving performance-specific language where the feature is genuinely performance-oriented.

## Capabilities

### New Capabilities

- `typed-gigs`: Required gig type classification, type-aware gig UI/API behavior, import draft typing, and type-aware invoice fee descriptions.

### Modified Capabilities

- `mcp-read-tools`: MCP gig listing/detail responses and filters include gig type while preserving existing visibility boundaries.

## Impact

- Backend domain/model: `Gig`, `GigImportDraft`, validation, EF configuration, seed data, and a normal post-`InitialBaseline` EF migration under `backend/Glovelly.Migrations`.
- Backend workflows: gig CRUD, gig import commit/review, quick capture candidates, invoice line generation, tax/expense/invoice regression coverage, and MCP query/tool contracts.
- Frontend: gig and import draft types/forms, gig editor, import review UI, gig list/detail presentation, search/filter behavior, copy updates, and TypeScript API shapes.
- API compatibility: existing records are backfilled to `Performance`; existing creation paths that omit a type continue where practical by defaulting only on known legacy/import/quick-capture/MCP paths, while normal user-facing gig creation requires a type.
- No change to calendar event title/description behavior and no set-list restriction by gig type.
