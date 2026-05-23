# MCP

Glovelly exposes a small authenticated MCP-compatible JSON-RPC endpoint at `/mcp`. Most tools are read-only business-data queries. The gig import tools are write tools that create staged import batches and draft rows only; they do not create real gigs until the user reviews and commits them in Glovelly.

## Implementation

- Protocol and tool dispatch: `backend/Glovelly.Api/Endpoints/McpEndpoints.cs`
- Query/projection logic: `backend/Glovelly.Api/Services/GlovellyMcpQueryService.cs`
- Tool service registration: `backend/Glovelly.Api/Services/ServiceCollectionExtensions.cs`

The endpoint supports `initialize`, `ping`, `tools/list`, and `tools/call`. It accepts current and legacy MCP protocol version headers and exposes permissive MCP CORS headers.

## Tools

### `glovelly_search_contacts`

Search contacts by name or email.

Arguments:

- `query`: optional string

Returns possible contact matches without guessing.

### `glovelly_list_invoices`

List invoices by optional contact, status, date range, and date basis.

Arguments:

- `contactId`: optional UUID
- `contactQuery`: optional string. If it matches multiple contacts, the result is marked ambiguous.
- `status`: optional `all`, `outstanding`, `issued`, `paid`, `draft`, `overdue`, or `cancelled`
- `fromDate`: optional date
- `toDate`: optional date
- `dateBasis`: optional `issueDate` or `dueDate`

Returns invoice summaries plus total outstanding amount.

### `glovelly_get_invoice`

Fetch details for one invoice.

Arguments:

- `invoiceId`: required UUID

Returns invoice details and ordered lines when found.

### `glovelly_list_receipts`

List receipt and expense records.

Arguments:

- `fromDate`: optional date
- `toDate`: optional date
- `status`: optional `all`, `matched`, or `unmatched`

Unmatched receipts are expenses with zero amount or descriptions containing `receipt draft`.

### `glovelly_get_business_summary`

Summarise invoice and receipt totals for a date range.

Arguments:

- `fromDate`: optional date
- `toDate`: optional date

Returns invoice total, paid total, outstanding total, expense total, receipt count, and unmatched receipt count.

### `glovelly_create_gig_import_batch`

Create a staged gig import batch for AI-assisted extraction results.

Arguments:

- `sourceName`: required string
- `notes`: optional string
- `sourceFingerprint`: optional string

Returns validation errors or the created batch summary. Creating a batch does not create gigs.

### `glovelly_add_gig_import_draft`

Add one candidate gig row to a staged import batch.

Arguments include:

- `batchId`: required UUID
- `title`, `clientName`, `contactQuery`, `contactName`, `contactEmail`, `projectName`
- `date`, `arrivalTime`, `rehearsalStartTime`, `rehearsalEndTime`, `showStartTime`, `showEndTime`
- `venueName`, `venueAddress`, `postcode`
- `fee`, `perDiem`
- `notes`, `accommodationNotes`, `travelNotes`, `sourceReference`
- `confidence`: optional `low`, `medium`, or `high`
- `warnings`: optional string array

The MCP boundary is forgiving of common conversational date/time strings. Parseable values are normalised; vague values are stored as blank fields for human review.

### `glovelly_add_gig_import_drafts`

Add multiple candidate gig rows to a staged import batch.

Arguments:

- `batchId`: required UUID
- `drafts`: array of draft inputs using the same shape as `glovelly_add_gig_import_draft`, without per-row `batchId`

Returns per-row results. Valid rows can be staged even when other rows have validation errors.

### `glovelly_list_gig_import_batches`

List staged gig import batches for the signed-in user.

Returns source name, created date, status, notes, source fingerprint, and draft count.

### `glovelly_get_gig_import_batch`

Fetch one staged gig import batch and its draft rows.

Arguments:

- `batchId`: required UUID

Returns the batch and draft rows when found.

## Imported Gig Review

Staged imports are reviewed in Glovelly from the profile menu under `Imported gigs`. The profile avatar and menu item show a notification dot when pending or accepted rows exist. The frontend polls for import batches while signed in so newly-created imports appear without a manual refresh.

Draft row edits autosave. Users accept rows that should become gigs and reject rows that should be discarded. `Commit decisions` creates real gigs from accepted rows and deletes rejected draft rows from the import. Pending rows remain staged for later review. Committed gigs retain source import batch/draft linkage for auditability.

## Notes for Agents

- Prefer MCP tools for user business-data questions when they are available in the host environment.
- Use gig import write tools only to stage candidate gigs for review; do not imply that staged rows are real gigs before the user commits decisions.
- Do not use MCP tools for codebase inspection; use the repository files.
- Add tests in `McpEndpointsTests.cs` when changing protocol/tool dispatch.
- Add query behavior tests when changing `GlovellyMcpQueryService`.
