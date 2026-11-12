## Why

The public guide currently sends readers to Menu's generic entry point even when they are following a specific task. Stable workspace links will take them to the right place without weakening authentication or disrupting existing navigation (#276).

## What Changes

- Provide canonical, authenticated-safe entry URLs for the Clients, Gigs, and Invoices workspaces.
- Keep the URL aligned with workspace changes using history replacement, rather than creating a browser-history entry for each switch.
- Preserve safe sign-in returns, access-request review links, cross-workspace actions, and safe default behavior for unknown application paths.
- Update the public guide's task-specific Menu links and relevant maintainer and UAT documentation; verify direct navigation in browser coverage.
- Exclude individual record links and unauthenticated workspace data.

## Capabilities

### New Capabilities
- `workspace-deep-links`: Canonical workspace entry URLs, URL synchronization, and safe interaction with authentication and other navigation.

### Modified Capabilities
- `public-user-guide`: Task-oriented guide handoffs lead to the appropriate Menu workspace.

## Impact

The React application, SPA serving and local development behavior, browser tests, public guide, and routing/UAT documentation may be affected. Existing API paths and authorization remain unchanged; no new unauthenticated workspace data or record-level routing is introduced.
