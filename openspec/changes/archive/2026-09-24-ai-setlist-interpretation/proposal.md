## Why

Set-list import currently assumes that every Google Sheet is a stable row-and-column table. Real musician set lists are visual documents whose layouts change between bands, events, and revisions, so deterministic parsing can turn keys, singers, instruments, or notes into false song titles before chart matching begins.

The import must interpret each bounded worksheet as a whole document, rather than prescribing a sheet format or accumulating layout-specific parsing heuristics.

## What Changes

- Add an AI-assisted, schema-constrained set-list interpretation stage that receives a coordinate-preserving worksheet grid and produces an ordered, reviewable draft of songs, sections, transitions, comments, metadata, inclusion state, source evidence, and confidence.
- Make AI interpretation the sole automatic set-list extraction path; remove the deterministic row parser and its layout-specific fallback behavior.
- Persist the source grid and asynchronous interpretation job state so interpretation can be retried and the source remains traceable after browser interruption or AI failure.
- Add retained-source-backed manual draft authoring as the recovery path when AI interpretation is unavailable, fails, or produces invalid output. **BREAKING**: this replaces the existing basic row-import recovery route.
- Evolve set-list item source evidence from one primary row to one primary row plus multiple cited source rows/cells, and use stable item identity rather than source-row number for downstream matching results.
- Run deterministic forScore candidate lookup only against reviewed, valid interpreted song items; retain the existing optional AI chart-selection operation as a separate downstream workflow.
- Add sanitised grid regression fixtures derived from varied layout patterns without treating any fixture layout as a required production format.

## Capabilities

### New Capabilities
- `ai-setlist-interpretation`: Interpret arbitrary linked Google Sheet set-list layouts into validated, traceable, manually reviewable drafts.

### Modified Capabilities
- `google-setlist-import-reliability`: Replace row-preview failure behavior with bounded grid acquisition and actionable interpretation/recovery behavior.
- `setlist-chart-matching`: Require chart candidate lookup and AI chart selection to operate on reviewed interpreted item identity rather than parsed source rows.

## Impact

- Backend set-list source retrieval, import endpoints, persistence models/configuration/migrations, background-job infrastructure, Vertex integration, and tests.
- Authenticated-app set-list import modal, API types, item review identity, progress/retry/manual-authoring UI, and UAT journey.
- Google Sheets reads require coordinate-preserving formatted grid data with explicit bounds.
- The supplied `.xlsx` remains a local reproduction aid only; regression fixtures must be sanitised and production import remains linked Google Sheets.
