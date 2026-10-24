## Context

The #249 fix removed detail-pane height measurement and stretched workspace columns to prevent empty selections from truncating list controls. Desktop `.compact-record-list` now removes its maximum height without a bounded ancestor, so overflow never activates. `AppShell.tsx` additionally stacks introductory copy, section context, mascot introduction, navigation descriptions, and summary cards above the working area.

The four record workspaces share CSS but have different controls and detail/editor contents. Existing contextual summaries, explicit gig navigation, modal layering, and mobile flows must remain intact. No backend change is needed.

## Goals / Non-Goals

**Goals:**
- Allocate remaining desktop viewport space to work, independent of list length or selected detail height.
- Simplify authenticated chrome while retaining navigation, capture, account access, concise section identity, summaries, and legal links.
- Preserve empty-state usability and accessible fallbacks.

**Non-Goals:**
- Changing business rules, summary calculations, record filtering, APIs, or authentication.
- A component-library migration, broad visual rebrand, pagination, or list virtualization.
- Forcing settings and other document-like pages into independently scrolling panes.
- Adding an overview screen or a new summary preference.
- Redesigning detail panes into non-scrolling overviews with dedicated notes/resources/expenses dialogs; that interaction change is deferred to a follow-up.

## Decisions

### 1. Viewport owns desktop workspace height

Use explicit shell/view classes for bounded record workspaces. A desktop grid allocates content-sized compact chrome and footer rows around a `minmax(0, 1fr)` workspace row. Propagate `min-height: 0` and `min-width: 0` through intermediary grid/flex containers. Use dynamic viewport units with a suitable fallback rather than a manually estimated header subtraction. Keep height containment scoped to eligible views and viewport conditions.

Reject restoring measured detail heights: selection must not determine list capacity. Reject list-only `max-height`: it restores overflow but leaves the whole desktop page oversized. Avoid JavaScript height observers unless CSS proves insufficient.

### 2. Pane overflow is explicit

Keep list search, filters, and actions in content-sized panel rows; allocate the remaining panel height to `.compact-record-list`, retaining sticky column headings. List rows must not shrink to fit their container. Give details and open editor panels their own scroll regions; closed editor slots must contribute no height. Retain workspace column stretching from #249 without allowing child content to expand the parent.

Avoid nested scroll regions within an individual pane where possible. Pane controls and focus outlines must not be clipped. Audit explicit navigation and `scrollIntoView` calls so they reveal the requested detail within the appropriate scroll container without moving the desktop document. Preserve the existing gig-selection/navigation contract.

### 3. Compact chrome, not tiny controls

Refactor `AppShell.tsx` to a compact brand/navigation/actions area. Identify the current section through active navigation and existing workspace headings, without a duplicate shell title. Remove the permanent slogan, duplicate explanatory descriptions, and large mascot introduction. Small brand imagery can retain personality without consuming a separate row. Preserve semantic headings, navigation labels, keyboard access, account menu behavior, and capture entry points.

Use existing summary data in a shared compact informational strip on every authenticated view. Preserve the three contextual summaries for Clients, Gigs, and Invoices, but remove embedded summary action buttons and their now-unused callback/scroll-request wiring; invoice filter chips remain authoritative. Admin uses the already-loaded complete user directory for Users with access, Active accounts, and Administrators, with explicit loading/error states. Remove its separate banner and associated layout row/styles. No new API calls are needed. Allow the strip to wrap on narrow screens. On document-like views it remains present in the shared header, but need not be sticky during page scrolling.

Reject shrinking all typography and interaction targets: most savings should come from redundant chrome, padding, and layout.

### 4. Responsive containment is conditional

Start with the existing side-by-side desktop breakpoint above 1180 CSS pixels. Target bounded operation at 1280×800 and 1440×900 CSS-pixel viewports with default text sizing. Add a short-height fallback, initially below 720 CSS pixels, alongside the stacked-width fallback; tune only with browser evidence while retaining those target viewports. These fallbacks return the document to natural flow, retaining a usable bounded record list where space permits. Do not globally hide document overflow to conceal layout errors.

Zoom commonly reduces the effective CSS viewport and activates the fallback. Enlarged text or unusually wrapped controls must remain reachable even if this requires page scrolling. Non-record views always use natural page flow with the same compact header and summary strip.

### 5. Verify geometry and journeys, not just appearance

Extend browser regression coverage across all four workspaces: document versus pane overflow, reachable last records, empty and filtered states, long details, and editor toggling. Verify summary navigation, capture-to-gig navigation, keyboard focus, menus, and modal/notification stacking. Run browser UAT only in Ubuntu CI; run frontend unit tests/lint/build locally. Update UAT documentation and review real CI documentation-capture candidates before replacing any guide screenshots.

## Risks / Trade-offs

- [Nested scrolling changes wheel and keyboard behavior] → Limit each pane to one primary scroll container and test focus-driven reveal plus independent scrolling.
- [Controls wrap and consume pane space] → Use content-sized control rows, preserve a usable records region, and fall back to natural page scrolling on constrained viewports.
- [Shared shell accidentally bounds settings or sign-in] → Opt into containment only for authenticated record workspaces; leave sign-in/checking screens unchanged.
- [Existing navigation scrolls the wrong ancestor] → Audit explicit navigation and cover capture-to-gig and summary actions in CI.
- [Smaller summaries lose state clarity] → Retain labels, meaningful values, and distinct loading/empty/error states; keep record filtering in the workspace's filter chips.
- [Header change invalidates captures] → Review staging-generated light/dark screenshot pairs; never manufacture replacement screenshots locally.

## Migration Plan

Ship as a frontend-only deployment without data migration or feature flag. Validate frontend checks and Ubuntu browser CI before release. Roll back by reverting the implementation change through the user's normal git workflow; no persistent data is affected.

## Open Questions

No blocking product decisions remain. Exact spacing and the short-height fallback threshold require browser validation during implementation; the agreed desktop target sizes and accessibility behavior are acceptance constraints.
