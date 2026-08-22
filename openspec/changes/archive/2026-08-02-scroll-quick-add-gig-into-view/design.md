## Context

Quick receipt and quick attachment flows both call the gig workspace's `openGigReceiptDraft` callback when their `Go to gig` action is selected. That callback already merges the saved gig, reveals it through active filters, selects it, opens the Gigs workspace, and opens the editor. The gig-list component has no signal that this navigation should also move the selected Gig overview into the viewport.

The same callback is used immediately after automatic draft saves. Automatically scrolling in those cases would be disruptive, so scrolling must be limited to the explicit `Go to gig` action.

## Goals / Non-Goals

**Goals:**

- Make explicit `Go to gig` navigation from quick receipts and attachments visibly locate the selected Gig overview.
- Preserve existing filter clearing, historical-gig reveal, selected state, and gig-editor behavior.
- Use the gig workspace's established smooth, start-aligned scrolling behavior.
- Make browser coverage observe the result of `Go to gig` directly.

**Non-Goals:**

- Change quick-capture matching, receipt or attachment persistence, or gig-list ordering.
- Scroll after every gig selection or every draft save.
- Add API endpoints, persisted UI state, or new dependencies.

## Decisions

### Use an explicit, one-shot Gig overview scroll request

The gig workspace will expose state representing a requested Gig overview reveal. `openGigReceiptDraft` will request it only when called for explicit quick-add navigation; both quick-capture hooks will identify their `Go to gig` invocation accordingly.

`GigsSection` will consume the request after React renders the selected gig and call `scrollIntoView` on the Gig overview panel with smooth, start-aligned positioning.

This keeps navigation intent and list rendering separate, rather than passing DOM references into hooks or making quick-capture hooks manipulate the document directly.

Alternative considered: scroll whenever the selected gig changes. Rejected because normal list selection, data refreshes, and automatic draft saves would unexpectedly move the viewport.

### Retain existing filter reveal before scrolling

The existing `revealGig` path remains the authority for clearing incompatible filters and showing historical gigs. The scroll effect runs only after that state has produced the selected Gig overview.

Alternative considered: scroll before opening the Gigs workspace. Rejected because the target row may not exist yet, especially when the current section differs or filters must be reset.

### Verify navigation without reopening the gig in UAT

The quick-attachment browser test will assert the Gig overview following `Go to gig` directly. Equivalent quick-receipt navigation coverage will verify the same result. Documentation will describe the expanded coverage.

Alternative considered: retain the existing follow-up `OpenGigAsync` assertion. Rejected because it independently selects the target and therefore cannot detect broken quick-add navigation.

## Risks / Trade-offs

- [The overview can render after filters or section state update] -> Trigger scrolling from a post-render effect keyed by a one-shot request and target selection.
- [A delayed scroll can fire after a newer navigation] -> Clean up any pending timer/effect work when the request changes or the component unmounts.
- [Browser visibility assertions do not prove pixel-perfect positioning] -> Assert the target overview is visible after `Go to gig`; retain manual UAT for visual animation judgement.
