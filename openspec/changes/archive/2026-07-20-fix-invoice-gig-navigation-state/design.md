## Context

Gig selection and gig editor state are managed together in `useGigsWorkspace`. The normal `selectGig` operation updates the selected gig and, when the editor is open, replaces the form with the target gig's persisted values after handling unsaved-change confirmation. Some cross-workspace callbacks in `App.tsx` bypass that operation by setting the selected gig ID directly.

Saving an expense keeps the gig editor open. If the user then follows an invoice-line shortcut to a different gig, the selected gig changes while the editable form remains associated with the previous gig. The next save sends the full stale form to the newly selected gig's `PUT /gigs/{id}` endpoint. That endpoint intentionally replaces editable gig fields and normalizes the full expense collection, making the client-side mismatch destructive.

## Goals / Non-Goals

**Goals:**
- Keep selected-gig identity and editable gig form state aligned for every cross-workspace navigation path.
- Retain the established explicit confirmation before discarding genuinely unsaved gig edits.
- Prove the invoice-line navigation circuit does not leak the first gig's fields or expenses into the second gig and cannot overwrite either record.

**Non-Goals:**
- Change the gig API's full-update semantics or introduce optimistic concurrency tokens.
- Redesign the gig editor or invoice-line user experience.
- Cover unrelated multi-browser or multi-user concurrent editing conflicts.

## Decisions

### Use the workspace's canonical gig-selection operation for navigation

Cross-workspace navigation that opens a gig will use the workspace-level selection behavior rather than calling the raw selected-ID state setter. This centralizes selection, editor hydration, external-resource editor cleanup, and unsaved-change handling in one path.

The invoice-line callback is the confirmed incident path. Dashboard shortcuts using raw gig selection will be reviewed and routed through the same behavior where they open a gig workspace, preventing the same state mismatch through a different entry point.

Alternative considered: update the form from an effect whenever `selectedGig` changes. This would erase unsaved edits before the existing confirmation logic can decide whether navigation is allowed, and would split selection policy across the hook and a reactive side effect.

### Preserve the existing discard guard

Navigation to a different gig with unsaved edits will continue to require explicit user confirmation. After a successful save, the form is rehydrated from the saved gig and is no longer dirty; navigation can therefore safely hydrate the target form without a prompt.

Alternative considered: close the editor whenever the user leaves Gigs. This is broader behavior change, loses the current editing continuity, and does not establish one safe selection path.

### Test persisted state, not only visible selection

The regression UAT will create two distinguishable, invoice-linked gigs, edit an expense on the first while retaining the open editor, regenerate the draft invoice, then follow an invoice line to the second gig. It will assert both target editor hydration and final persisted values after a save, so it detects both the misleading UI state and the previous data-loss outcome.

The existing cross-workspace navigation UAT is the natural suite because it already exercises invoice-line gig shortcuts. The UAT documentation will name the expanded test and describe the state-isolation guarantee.

## Risks / Trade-offs

- [A navigation helper can trigger an unexpected discard dialog] -> Reuse the established `selectGig` behavior and cover both saved-form navigation and existing dirty-form guard tests.
- [Target gig data held in the local workspace could be stale] -> This change prevents cross-record form leakage; it does not claim multi-session conflict protection. A future concurrency change can independently address stale remote data.
- [A direct selected-ID setter remains available for future callers] -> Limit cross-workspace use of raw setters and review current App-level entry points as part of implementation.

## Migration Plan

1. Ship the frontend selection-path correction and browser UAT regression together.
2. Run the focused UAT suite plus frontend lint/build and the normal verification suite before deployment.
3. No data migration is required. The correction prevents future bad writes; it cannot reconstruct data already overwritten, so any historical recovery remains an operational database-restoration concern.
4. If a regression is found, revert the frontend change; no persisted schema or API changes require rollback work.

## Open Questions

- None for the incident fix. Optimistic concurrency for gig updates remains a separate defense-in-depth decision.
