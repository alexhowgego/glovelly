## Context

Glovelly already stores active gig set list imports as `GigSetListImport` records with ordered `GigSetListItem` rows parsed from linked Google Sheets. Each item captures the imported title, section, pad number, key, inclusion flag, row kind, and parser confidence, but it does not identify the corresponding forScore chart.

Glovelly also now stores user-owned forScore library snapshots and chart metadata from `.4sb` exports. Snapshots are read-only and intentionally snapshot-local: importing a new library creates a new active snapshot rather than mutating or merging a stable catalogue. Set list chart mapping must therefore record both the selected chart row and copied chart identity data so old mappings remain explainable when a newer snapshot becomes active.

This change spans backend data model, set list import preview/save/update APIs, forScore snapshot import impact reporting, and the frontend review flows for both new and existing set lists.

## Goals / Non-Goals

**Goals:**

- Provide one shared backend matcher for both new set list import previews and existing saved set list mapping review.
- Let users confirm, change, clear, and save forScore chart mappings on song items.
- Persist snapshot-local mapping metadata on set list items, including copied chart title and file path.
- Detect the impact of a new forScore library snapshot on mapped active set lists for Draft and Confirmed future gigs.
- Auto-relink exact file path matches to the new active snapshot and mark ambiguous or missing mappings as needing user review.
- Present friendly drift/review wording in the UI rather than exposing implementation terms such as stale.

**Non-Goals:**

- Generating `.4ss` forScore set list exports.
- Creating a merged, stable forScore chart catalogue across snapshots.
- Importing existing forScore set lists from `.4sb` backups.
- Blocking forScore library snapshot import when existing set list mappings may need review.
- Persisting every generated match candidate unless the user selects a chart.

## Decisions

### Store Snapshot-Local Mappings On Set List Items

`GigSetListItem` will own nullable mapping fields for the selected forScore chart and the snapshot it came from. It will also copy the chart title and file path used at confirmation time. This preserves the meaning of old mappings even after a newer snapshot becomes active.

Alternative considered: introduce a stable chart identity model independent of snapshots. This is cleaner long-term but conflicts with the existing snapshot model and adds catalogue reconciliation complexity before there is evidence it is needed.

Alternative considered: store only `ForScoreChartId`. This is simpler but makes post-snapshot replacement UI and future export reasoning harder because the displayed mapping depends entirely on an old snapshot row.

### Compute Candidates On Demand

The matcher will return ephemeral candidates for draft and saved set list rows. Persisted state will contain the user's selected mapping and review status, not every candidate that was considered.

Alternative considered: persist a candidate table with accept/reject history. This could support audit and learning later, but it increases model complexity and is not required for the first chart-linking workflow.

### Match Conservatively

Exact normalized title and exact file path matches should be high confidence. Exact file path matches after snapshot replacement can be auto-relinked to the new snapshot. Title-only matches should be suggested and only auto-selected when unique and clearly high confidence; ambiguous results must be surfaced for user choice.

Alternative considered: aggressive fuzzy matching and auto-selection. This risks silently linking the wrong chart version, which is worse than asking for review because the mapping will eventually drive forScore set list generation.

### Treat Snapshot Import Drift As Review Work, Not A Blocker

New forScore library imports will remain allowed. After a successful import, the system assesses active set lists for Draft and Confirmed gigs that are today/future or undated, relinks exact file path matches, and reports set lists with chart links that need review.

Alternative considered: block library import while mapped set lists depend on the previous snapshot. This prevents the user from importing the very data needed to fix mappings and makes normal library refreshes feel risky.

### Separate Mapping Review From Set List Re-Import

Existing set list mapping review updates chart links on the saved active set list. It does not replace rows from Google Sheets or require re-reading the source spreadsheet.

Alternative considered: force users to re-import the Google Sheet to get chart matching. This is unnatural after a library import and risks overwriting reviewed set list row edits.

## Risks / Trade-offs

- Wrong chart auto-link → Limit auto-relink to exact file path matches and keep ambiguous/title-only matches reviewable.
- Snapshot-local links become old after later imports → Store copied file path/title and mapping snapshot id, then mark links from older snapshots as needing review when they cannot be relinked.
- Drift warning noise → Scope first-pass impact to active set lists on Draft/Confirmed future or undated gigs and summarize only items requiring action.
- UI overload during set list import → Default review to issue-focused status while still allowing users to inspect/change matched rows.
- Existing production data needs additive schema changes → Use nullable columns and a one-time PostgreSQL script matching existing manual database-change practice.

## Migration Plan

- Add nullable mapping columns to `GigSetListItems` so existing set list imports remain valid and initially unmapped.
- Add any supporting indexes for chart lookup and impacted set list queries.
- Provide a one-time PostgreSQL script under `scripts/manual/` for production/staging databases.
- Deploy backend support before frontend controls rely on the new response shape if staged deployment becomes necessary.
- Rollback is additive: leave nullable columns unused and hide frontend controls if the feature must be disabled.

## Open Questions

- Should unique exact normalized title matches be auto-selected during new set list import, or displayed as suggested until save?
- Should Draft gigs without dates be included in the same priority bucket as dated future Draft gigs, or shown separately in the impact summary?
- Should manually cleared mappings be protected from automatic rematch during later library imports?
