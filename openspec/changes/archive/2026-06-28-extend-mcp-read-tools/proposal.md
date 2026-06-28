## Why

Glovelly's MCP surface already supports read-only business questions for contacts, invoices, receipts, and summaries, plus staged gig imports. It does not yet expose first-class read access to gigs, gig setlists, gig resources, contact details, or expense-statement previews, which limits how useful an authenticated assistant can be for common music-work admin questions.

## What Changes

- Add read-only MCP tools for listing gigs, fetching one gig, and listing uninvoiced gigs.
- Add read-only MCP tools for fetching contact detail, listing gig resources, and fetching a gig's active setlist import.
- Add a read-only MCP expense-statement preview tool using the existing structured statement builder, without generating PDFs or performing delivery actions.
- Keep all new tools scoped to the authenticated MCP user and aligned with existing `WhereVisibleTo` visibility rules.
- Keep direct writes, external API fetches, PDF generation, email delivery, Google Drive publishing, calendar actions, gig import commits, and admin operations out of scope.

## Capabilities

### New Capabilities
- `mcp-read-tools`: Authenticated read-only MCP tools for gig, contact, resource, setlist, and expense-statement query workflows.

### Modified Capabilities
- None.

## Impact

- Backend MCP catalog, contracts, dispatch, query service, generated public MCP docs, and MCP capability manifest.
- Backend integration tests and checked-in MCP tool contract snapshot.
- No new external dependencies, database migrations, frontend API routes, or write-capable MCP behavior are expected.
