## 1. MCP Contracts And Catalog

- [x] 1.1 Add MCP request/result contract records for gig listing, gig detail, uninvoiced gigs, contact detail, gig resources, active setlist, and expense-statement preview.
- [x] 1.2 Add tool definitions, input schemas, output schemas, and `ReadOnly` safety metadata for all new tools in `GlovellyMcpToolCatalog`.
- [x] 1.3 Add any reusable schema fragments needed for gig selectors, invoicing filters, status filters, resource metadata, setlist items, and validation feedback.

## 2. Query Service Implementation

- [x] 2.1 Add `IGlovellyMcpQueryService` methods for `glovelly_list_gigs`, `glovelly_get_gig`, and `glovelly_list_uninvoiced_gigs` using existing user visibility rules.
- [x] 2.2 Add `IGlovellyMcpQueryService` methods for `glovelly_get_contact` and `glovelly_list_gig_resources` with detail/resource metadata projections only.
- [x] 2.3 Add `IGlovellyMcpQueryService` support for `glovelly_get_gig_setlist` using stored active setlist imports without calling Google Sheets or other external APIs.
- [x] 2.4 Add `IGlovellyMcpQueryService` support for `glovelly_preview_expense_statement` using `IExpenseStatementBuilder` and structured validation feedback without PDF generation or delivery side effects.
- [x] 2.5 Ensure all single-record tools return explicit found/not-found responses and all contact-query list tools handle ambiguous matches without guessing.

## 3. MCP Dispatch And Behavior

- [x] 3.1 Wire all new tool names into `McpEndpoints` dispatch with required ID validation and existing JSON argument handling conventions.
- [x] 3.2 Verify new tools do not publish workspace events, enqueue calendar work, mutate EF entities, generate PDFs, stream files, send email, publish to Drive, or call external APIs.
- [x] 3.3 Keep response ordering deterministic for lists, nested expenses, resources, and setlist items.

## 4. Documentation And Generated Artifacts

- [x] 4.1 Regenerate checked-in MCP public documentation and capability manifest from the typed tool catalog.
- [x] 4.2 Update `docs/mcp.md` with concise descriptions, arguments, and return behavior for the new tools.
- [x] 4.3 Update the MCP tool contract snapshot after verifying the generated catalog shape is intentional.

## 5. Tests And Verification

- [x] 5.1 Extend MCP catalog tests to assert all new tools are present, have `ReadOnly` safety metadata, and follow existing schema conventions.
- [x] 5.2 Add MCP endpoint tests for list/get gigs, uninvoiced gigs, contact detail, gig resources, active setlist, and expense-statement preview success paths.
- [x] 5.3 Add MCP endpoint tests for visibility boundaries, missing IDs, ambiguous contact queries, invalid expense-statement requests, no active setlist, and no file-content/PDF/delivery side effects.
- [x] 5.4 Run `dotnet test glovelly.sln -m:1` and resolve any backend test failures.
