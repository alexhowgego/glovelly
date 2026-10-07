## Why

Receipt and resource capture need one predictable presentation. The first uncommitted implementation branched into a visually distinct automatic receipt completion journey, causing abrupt dialog transitions and tangled orchestration. Revise this existing change and rebuild the workflow around what we have learned: automatic application saves a default in the open upload dialog; review is an optional next phase for saved attachments.

## What Changes

- Use one journey: `+ Add to Glovelly` → source type selection screen → unified upload dialog → optional Review attachment dialog.
- Keep the unified upload dialog open throughout submission, analysis, and application. Both automatic and explicit application end in the same compact `Attached to [Gig]` state with `Review attachment` and `Done` actions.
- Proactively save eligible receipts to the nearest visible, non-cancelled gig using existing configured candidate ranking. Other nearby gigs do not block automatic application.
- Respect Automatic receipt application preferences: Manual only, High confidence (default), and Medium confidence. Treat previously stored Very high confidence values as High confidence rather than expose an indistinguishable option.
- Preserve explicit receipt application when automation is disabled, confidence is insufficient, or no nearby gig is available. Require explicit destination, type/purpose, and confirmation for resources. These choices occur in the unified upload dialog rather than a separate pre-save journey.
- Reserve Review attachment for inspecting and correcting a saved receipt or resource; users on the go can finish from the upload dialog without entering review.
- Preserve one recoverable unapplied intake per user, original source linkage, retry/manual fallback, successful-intake cleanup, ownership, and draft-only invoice refresh.
- Preserve optional editable expense categories and independent high-confidence merchant, total, and category initialization on both automatic and explicit receipt application.
- Replace the current intake orchestration and presentation cleanly, removing obsolete branch-specific contracts and duplicate acquisition/application paths while retaining verified domain infrastructure.
- Keep URL processing metadata-based, including Google Doc/Sheet type inference; remote document-content analysis is deferred.

## Capabilities

### New Capabilities
- `unified-intake`: One source-selection, upload/application, and optional saved-attachment review journey with recoverable private intake state.

### Modified Capabilities
- `quick-capture-gig-selection`: Reuse configured nearest-gig ranking and safe candidate expansion in unified capture without an ambiguity gate on automatic receipt application.
- `vertex-receipt-analysis`: Analyse private intake sources and initialize independently high-confidence receipt fields through shared application while preserving validated evidence and correction workflows.
- `self-service-profile`: Persist and explain Automatic receipt application preferences without disrupting Settings control alignment.

## Impact

- Rebuild the unified intake frontend hook and source-selection/upload presentation as explicit stages, adapting saved receipt/resource review components.
- Replace branch-specific intake API contracts with shared attachment application state and identifiers; retain authenticated storage, analysis, candidate selection, and invoice services.
- Reuse and reverify the existing uncommitted private-intake and expense-category persistence/migrations rather than creating parallel models.
- Reset this change's implementation checklist for replacement work; update integration tests, UAT journeys, and applicable engineering/privacy documentation.
