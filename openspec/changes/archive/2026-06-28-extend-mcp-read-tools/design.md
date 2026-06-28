## Context

Glovelly's MCP endpoint is implemented as a small authenticated JSON-RPC surface. Tool definitions live in `GlovellyMcpToolCatalog`, dispatch lives in `McpEndpoints`, and user-scoped projections live in `GlovellyMcpQueryService`. Existing tools are either read-only business queries or staged gig-import writes that do not directly create production records.

The new scope expands the read-only side of MCP around gigs, contact details, gig resources, setlists, and expense-statement previews. These areas already exist in Glovelly's authenticated backend model and can be exposed through projections without new persistence, frontend routes, or external dependencies.

## Goals / Non-Goals

**Goals:**

- Add read-only MCP tools for gig discovery, gig detail, uninvoiced gigs, contact detail, gig resources, active gig setlists, and expense-statement preview.
- Reuse existing user visibility rules and domain services wherever possible.
- Return agent-friendly structured JSON with stable IDs, dates, statuses, money values, and found/ambiguous indicators where relevant.
- Keep the MCP contract documented through the generated tool docs, capability manifest, and checked-in contract snapshot.

**Non-Goals:**

- No direct creation, update, deletion, commit, or status-changing behavior through the new tools.
- No external API fetches, including Google Sheets reads or mileage route estimation.
- No PDF generation, receipt attachment file streaming, invoice delivery, email sending, Google Drive publishing, calendar actions, or admin operations.
- No database schema changes or frontend UI changes.

## Decisions

### Keep tools projection-based inside `GlovellyMcpQueryService`

New behavior will be added as service methods that project EF entities into MCP-specific contracts. This matches the existing MCP architecture and avoids returning full domain entities with navigation cycles or frontend-specific shape.

Alternative considered: reuse existing minimal API endpoint response bodies directly. That would reduce code initially, but it would couple MCP contracts to UI-oriented endpoint shapes and make agent-facing schema control weaker.

### Treat all selected tools as `ReadOnly`

The selected tools only read local Glovelly data or build an in-memory expense statement projection. They should use `McpToolSafetyLevel.ReadOnly` and must not publish workspace events, enqueue jobs, mutate records, call delivery services, or invoke external APIs.

Alternative considered: mark expense statement preview as staged or external because expense statements can also produce PDFs elsewhere. The selected tool only returns the existing structured preview projection, so read-only is appropriate as long as PDF generation remains out of scope.

### Use explicit not-found and ambiguity responses

Single-record tools should return `found: false` rather than leaking whether records exist outside the user's visible scope. Contact-query based list tools should mirror invoice ambiguity behavior where multiple contacts match, returning matches and no guessed records.

Alternative considered: return empty results for all missing records. Explicit `found` and `ambiguous` states are easier for agents to reason about and already appear in current MCP invoice behavior.

### Keep setlist access local-only

`glovelly_get_gig_setlist` should return the active imported setlist already stored in Glovelly. It should not fetch from Google Sheets or preview new source data, because that would introduce an external API dependency and a different safety profile.

Alternative considered: expose the existing Google Sheets source/preview endpoints through MCP. That is useful but was explicitly descoped because it reaches outside local Glovelly data.

### Return resource metadata, not attachment contents

`glovelly_list_gig_resources` should expose resource IDs, titles, URLs, purposes, types, primary flags, notes, and attachment metadata such as filename/content type/size. It should not stream attachment bytes or generate download URLs.

Alternative considered: allow MCP clients to retrieve attachment content. That is a broader document-access feature and has higher privacy and binary transport concerns.

## Risks / Trade-offs

- More MCP tools increase catalog and snapshot maintenance -> rely on existing catalog snapshot, generated docs tests, and focused endpoint tests.
- Gig detail can become large when including expenses, resources, and setlist references -> keep list responses summarized and reserve richer nested data for get/detail tools.
- Expense statement previews may expose receipt attachment metadata -> include metadata only and preserve user visibility checks through the existing builder.
- Date/status/invoicing filters could become inconsistent with UI wording -> normalize status strings against existing `GigStatus` values and document product wording where needed.
