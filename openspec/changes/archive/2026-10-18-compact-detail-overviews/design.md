## Context

The shell and record workspaces already allocate remaining desktop viewport height to panes. Detail panels currently use `overflow: auto`, and Gigs renders full facts, notes, `GigAttachmentsPanel`, and `GigExpensesPanel` in a vertical stack. Invoices has many actions and metadata fields plus an existing line-items editor. Clients embeds monthly invoicing and billing details; Admin shows access and identity metadata.

`GigExpensesPanel` already owns expense editing and receipt-analysis dialogs; resources include editing, uploads, and set-list import dialogs. Moving these panels must not create competing focus traps, reset drafts unexpectedly, or replace the existing mutation paths. The preceding change's browser validation was waived; static checks alone do not establish a verified layout baseline.

## Goals / Non-Goals

**Goals:**
- Use the available desktop detail space for normal records at 1280×800 and 1440×900 CSS pixels, with independent detail scrolling for anomalous text rather than an absolute no-scroll constraint.
- Keep overview facts and actions usable when existing editors narrow the available detail width.
- Show standard details fully inline, without redundant read-only dialogs or silent clipping.
- Reuse workflow hooks and existing domain components in accessible record-scoped dialogs.

**Non-Goals:**
- Redesigning record editors or moving invoice line items out of their current scrollable editor.
- New business rules, APIs, dependencies, runtime settings, summary metrics, tips, or a new navigation model.
- Guaranteeing zero scrolling in arbitrary zoom/text-size conditions; accessibility takes precedence.

## Decisions

### 1. Fit by information architecture, not overflow suppression

Use structured facts and a deliberately bounded pane composition. Reduce repeated labels, large card padding, and action wrapping before altering type size. Show standard fields in full and preserve line breaks. Never use `overflow: hidden` as a substitute for fitting the content or remove critical warnings to satisfy a geometry assertion.

Keep current desktop width/height eligibility and natural-flow fallbacks. At supported sizes, bound the middle detail region independently of header and applicable dock. It grows to use free space and scrolls when standard fields or long text exceed it. Intrinsically sized children do not shrink or truncate. Auto block margins center the facts group only while free space exists, falling back to top-aligned scrolling on overflow. Key the middle region by record ID so a new selection starts at the top while same-record refreshes preserve reading position.

Review refinement: keep the top header and full-pane structure, but left-align labels/values rather than individually centering them. Use a structured two-column record grid with strong values and quieter labels. Keep a stretched bottom dock only for meaningful collection/task controls; Admin needs no redundant detail button or empty dock. Mobile and short-window layouts keep natural flow. Read-only notes/contact/workflow/account dialogs have been removed following user review.

### 2. Workspace content map

| Workspace | Overview retains | Focused secondary content |
|---|---|---|
| Gigs | Full identity/client, type, date, fee, status, location, driving/invoice link, notes, resource count, expense count/total | Resource management and expense/receipt management |
| Clients | Full contact and billing information, compact monthly-invoice controls, settings/edit actions | Existing client settings and generated invoice preview task dialogs |
| Invoices | Full description, issue/reissue/delivery metadata, status control, dates, total, document readiness/retry, line-items entry and primary delivery/PDF actions | Existing sending/preview task dialogs; line items/adjustments/description editing remain in the existing editor |
| Admin Users | Full identity/email, role, active state, enrolment, account reference, creation and last-login metadata, access editing | Existing access editor only; no read-only account dialog |

The pane tells the user about the record; dialogs manage its collections or perform a focused task. Notes/descriptions remain editable through existing editors. Counts and totals come from authoritative current records and existing normalized amounts, with no new financial calculations. Resource/expense dialogs remain openable with zero items so creation is always discoverable.

### 3. Actions remain reachable without a button wall

Keep the main task and existing editor entry directly visible. Compact action groups may use an accessible More actions menu for less frequent/destructive operations, preserving all existing eligibility rules, tooltips/explanations, confirmations, testable names, and trigger references. Invoices must retain status changes, redraft/reissue, preview/download, email, Drive publishing, and draft deletion; Gigs must retain invoicing, statements, linked-invoice navigation, cloning, and deletion.

Menus close before opening a dialog. Existing invoice-email-review focus restoration must target the real remaining trigger (or a sensible fallback), not an unmounted menu button. No mandatory new menu abstraction or component library is introduced.

### 4. Dialog ownership and composition

Use a small shared dialog shell where existing components do not already provide equivalent behavior: accessible title, initial focus, containment, close control, Escape, body-scroll locking, and focus restoration. Keep domain content in focused components and local section-level state or a small hook, not large new workflow bodies in `App.tsx`. Render one instance of each resource/expense workflow; remove the superseded inline full collections.

Reuse resource and expense components/callbacks, extracting presentational contents if necessary to avoid double frames or nested scrollbars. The outer detail dialog provides a bounded, scrollable body with reachable close/actions. On mobile it becomes a near-full-height dialog with the same information and operations, not a second independent implementation.

Existing child dialogs (expense editor, receipt analysis, resource editor, set-list import) require explicit modal coordination: only the top layer is interactive and traps focus. Suspend/inert the parent while a child is active, keep its draft/scroll context, and restore it when the child closes. Escape dismisses only the top layer. Reuse an existing equivalent dialog primitive if available; do not merely stack two independent traps and body-lock handlers.

### 5. Bind open content to stable record identity

Store dialog kind, entity ID, and the initiating trigger, not an editable copy of the selected record as the source of truth. Resolve current content by ID so saves refresh summaries and the open dialog. Selection/workspace changes or deletion invalidate and close that record's dialogs and local draft/expanded-item state; never silently retarget an open resource/expense dialog to a new selection.

In-flight operations keep their original record/item IDs. Ignore stale presentation updates after invalidation while preserving legitimate saved results through existing hooks. Apply busy/dirty guards or explicit discard confirmation to user-initiated dismissal. Native modal containment prevents manual background selection. If the record disappears, selection changes externally, or the workspace unmounts, close stale management dialogs immediately and explicitly notify that any unsaved detail edits were discarded; do not keep a mutable dialog bound to a new selection. This external-invalidation policy was confirmed by the user during implementation. If the origin no longer exists, restore focus to the selected overview heading or workspace control instead.

### 6. Navigation and feedback contracts stay authoritative

Capture review's Go to gig and invoice-line links continue using existing visibility/selection rules and reveal the overview, not a resource dialog automatically. Update browser helpers to explicitly open Resources or Expenses when a journey needs their contents. Operations that previously left an associated-item row visible now leave its dialog visible where appropriate; no automatic close/navigation on attachment success.

Use the existing notification policy: feedback for still-open dialogs stays inline, while terminal events that outlive their initiating UI use the wrapper. Durable seller-profile/document warnings remain actionable and accessible, with concise overview state plus full guidance where needed. Notifications stay below modal overlays.

## Risks / Trade-offs

- [Extra click to inspect collections] → Show meaningful counts/totals and obvious open/create affordances; keep primary actions direct.
- [Editor-open widths wrap facts/actions] → Validate both editor states at both desktop targets, with long text fixtures and overflow-menu use where necessary.
- [Existing nested dialog behavior breaks focus or scroll locks] → Explicit top-layer coordination plus keyboard and child-return regression tests.
- [Record changes race uploads or draft editing] → Stable-ID binding, context invalidation, existing busy/dirty guards, and tests switching/deleting during operations.
- [Long records exceed normal space] → Wrap full values and scroll the middle region; retain semantic names and reachable durable guidance without tooltip-only recovery.
- [Tests/screenshots assume inline collections] → Update workflow helpers and documentation journeys; review real staging light/dark capture candidates before replacement.

## Migration Plan

Frontend-only deployment with no data migration or feature flag. Validate unit tests, lint/build, full verification, and Ubuntu CI browser journeys before considering implementation complete. Update guide instructions to match the deliberate drill-down. Rollback is a normal user-managed revert; no stored data changes.

## Open Questions

No blocking product decisions remain. Exact spacing and action grouping are implementation choices bounded by the workspace content map, full inline information access, and browser geometry/focus acceptance tests. Header tips remain separately deferred in #302.
