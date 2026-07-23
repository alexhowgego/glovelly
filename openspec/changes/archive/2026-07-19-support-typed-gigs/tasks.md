## 1. Backend Domain And Migration

- [x] 1.1 Add `GigType` enum and add required type fields to `Gig`.
- [x] 1.2 Add proposed gig type to `GigImportDraft` and configure gig/import draft EF string conversions, lengths, and nullability.
- [x] 1.3 Generate a post-`InitialBaseline` EF migration in `backend/Glovelly.Migrations` that backfills existing gigs and import drafts to `Performance` and updates the model snapshot.
- [x] 1.4 Update development, UAT, and test seed data so created gigs and import drafts have explicit or defaulted types.

## 2. Backend API Workflows

- [x] 2.1 Update gig create/update validation, normalization, responses, and invoice-relevant change detection to include gig type.
- [x] 2.2 Update gig import draft REST update/detail DTOs, validation, missing-field handling, and commit mapping so draft type is editable and committed to created gigs.
- [x] 2.3 Update quick capture gig candidate responses and any other inferred gig creation paths to return or default gig type consistently.
- [x] 2.4 Update invoice line generation to use type-aware fee descriptions.
- [x] 2.5 Confirm set-list endpoints and resource flows remain unrestricted by gig type and calendar event mapping remains unchanged.

## 3. MCP Contracts

- [x] 3.1 Add gig type to MCP gig summary/detail contracts and output schemas.
- [x] 3.2 Add optional gig type filtering to `glovelly_list_gigs` and `glovelly_list_uninvoiced_gigs` while preserving contact/status/date/invoicing filters.
- [x] 3.3 Add gig type to MCP staged gig import draft input, validation, persistence, output contracts, and tool schemas.
- [x] 3.4 Update MCP tool snapshots, generated MCP documentation, and capability manifest artifacts affected by schema changes.

## 4. Frontend Types And Gig Workspace

- [x] 4.1 Add frontend `GigType` types, formatters, form fields, payload mapping, and default values.
- [x] 4.2 Add required type selection to the gig editor.
- [x] 4.3 Show gig type in gig lists, selected gig details, and quick candidate surfaces where gig summaries are displayed.
- [x] 4.4 Add gig type filtering and type-aware search to the gig workspace without regressing existing quick filters and sorting.
- [x] 4.5 Replace generic performance/venue/show copy with neutral gig/location wording while keeping contextual set-list language.
- [x] 4.6 Retain a clear linked-gig navigation action for invoice lines.

## 5. Frontend Import Review

- [x] 5.1 Add proposed gig type to frontend import draft types, payloads, autosave/update handling, and status rendering.
- [x] 5.2 Add editable type selection to the gig import draft review UI and display type in draft summaries.
- [x] 5.3 Ensure committed/imported gigs returned to the frontend preserve and display the selected type.

## 6. Tests And Verification

- [x] 6.1 Add backend tests for gig type validation, create/edit/read behavior, defaults, migration-backed model changes, and existing performance workflow preservation.
- [x] 6.2 Add backend tests for import draft type editing/defaulting/commit behavior through REST and MCP paths.
- [x] 6.3 Add backend tests for type-aware invoice fee descriptions and generated-line behavior.
- [x] 6.4 Add MCP tests for type in gig list/detail responses, type filtering, and staged import draft validation/defaults.
- [x] 6.5 Add or update frontend checks for gig type form behavior, list/detail display, filtering/search, and import draft editing.
- [x] 6.6 Run `dotnet test glovelly.sln -m:1`, `npm --prefix frontend/glovelly-web run lint`, and `npm --prefix frontend/glovelly-web run build`.
