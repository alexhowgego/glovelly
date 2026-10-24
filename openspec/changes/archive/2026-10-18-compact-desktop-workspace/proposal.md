## Why

Issue #293 exposes a regression from #249: desktop record lists grow to their full content height instead of scrolling internally. The oversized introductory header also consumes working space, preventing comfortable viewport-sized use on laptops.

## What Changes

- Simplify the authenticated header to compact branding, navigation, capture, account controls, and concise section context; remove repeated introductory copy and the large mascot introduction.
- Keep a compact informational summary strip visible across all authenticated views, preserving contextual metrics and loading/error states without redundant action buttons. Admin receives its own user-access metrics in place of the separate banner.
- Remove the duplicate shell workspace title; active navigation and workspace headings provide section identification.
- Fit desktop Gigs, Invoices, Clients, and admin Users workspaces within the viewport, with independently scrolling record lists, details, and editors.
- Keep list controls reachable regardless of record count, selection, or empty filtered results, preserving the #249 fix.
- Preserve natural page scrolling for stacked/mobile layouts, document-like settings screens, and constrained height/zoom conditions.

## Capabilities

### New Capabilities
- `desktop-workspace-layout`: Compact authenticated chrome, bounded desktop workspaces, independent pane scrolling, and accessible responsive fallbacks.

### Modified Capabilities
- `contextual-dashboard-cards`: Present contextual summaries as a compact persistent informational strip, add Admin user-access metrics, and replace summary actions with the existing invoice filter chips.

## Impact

- Frontend `AppShell.tsx`, `App.css`, and potentially `index.css`; shared workspace markup in Gigs, Invoices, Clients, and Admin sections.
- Browser layout/navigation regression tests and matching `docs/uat/` journeys; review guide text and CI-generated screenshot candidates if affected.
- No backend API, persistence, dependency, or runtime configuration changes expected. Existing summary calculations and navigation behavior remain authoritative.
