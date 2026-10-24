## Why

The compact desktop shell now fits the viewport, but detail presentation needs to use that space as a business record rather than a long collection stack or a set of redundant read-only dialogs. Show standard details inline, aiming to accommodate normal records comfortably, and reserve dialogs for independently growing collections and focused tasks.

## What Changes

- Redesign Gigs, Clients, Invoices, and Admin Users details as structured, left-aligned business records that use the available pane space, including when the existing editor is open.
- Show full standard facts, notes, contact/billing information, and issue/delivery/account metadata inline. Allow the middle detail region to scroll for anomalous records rather than hide information behind permanent read-only dialogs.
- Keep independently growing gig resources and expenses/receipts in focused dialogs, reusing existing management behavior rather than duplicating it.
- Keep common actions available and preserve invoice line items and record editing in their existing independently scrollable editor areas; no editor redesign in this change.
- Preserve mobile/short-window/zoom accessibility, safe modal focus and dismissal, record identity, asynchronous updates, explicit navigation, and notification behavior.

## Capabilities

### New Capabilities
- `compact-record-overviews`: Workspace-specific inline standard details, accessible long-content scrolling, and preservation of record actions.
- `record-detail-dialogs`: Accessible, record-scoped dialogs for growing detail content and existing resource/expense operations.

### Modified Capabilities
- `desktop-workspace-layout`: Allocate normal detail space within bounded desktop panes while allowing long-content detail scrolling, retaining list/editor scrolling and accessible constrained-viewport fallbacks.

## Impact

- React workspace sections, detail presentation, `GigAttachmentsPanel`, `GigExpensesPanel`, related modal components, CSS, and navigation/focus helpers.
- Existing workspace hooks remain responsible for business mutations; no backend APIs, persistence, runtime settings, new dependencies, or editor business-rule changes are expected.
- Update frontend component tests, browser UAT scenarios/helpers, user-guide instructions, and staging documentation-capture expectations. Browser validation and captures run in Ubuntu CI only.
- Builds on archived `compact-desktop-workspace`. Its CI browser/visual checks were waived, not passed; this change must verify its own layout and affected workflows rather than assume prior geometry was proven.
- Tips tracked in GitHub #302 remain deferred and outside this change.
