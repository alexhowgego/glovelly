# MCP

Glovelly exposes a small authenticated MCP-compatible JSON-RPC endpoint at `/mcp`. The surface is read-only and scoped to the signed-in Glovelly user.

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

## Notes for Agents

- Prefer MCP tools for user business-data questions when they are available in the host environment.
- Do not use MCP tools for codebase inspection; use the repository files.
- Add tests in `McpEndpointsTests.cs` when changing protocol/tool dispatch.
- Add query behavior tests when changing `GlovellyMcpQueryService`.
