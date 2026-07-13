## Context

Glovelly currently stores gig set list imports parsed from linked Google Sheets. Those imports contain ordered set list rows, but Glovelly has no durable knowledge of the user's forScore chart library, so imported rows cannot yet be matched to chart files.

forScore exports library backups as `.4sb` files. The observed sample format is a small `4SBV02` wrapper containing a gzip payload whose decompressed content is an Apple binary property list. The plist contains score metadata keyed by file path plus suffixes such as `|title`, `|keywords`, `|added`, `|printNumber`, and `|version`. It also contains forScore set lists (`&SET;...`), system settings (`&SYS;...`), annotations, and binary assets. This change only uses the score metadata needed for interoperability with later set list matching and `.4ss` export generation.

## Goals / Non-Goals

**Goals:**
- Allow an authenticated user to upload a forScore `.4sb` library export as a read-only input.
- Parse chart metadata from the wrapped gzip binary plist format.
- Store a user-owned snapshot containing normalized chart records and import metadata.
- Maintain one active successful snapshot per user for later matching.
- Report clear success, warning, and failure outcomes.

**Non-Goals:**
- Do not modify, regenerate, or write `.4sb` files.
- Do not import existing forScore set lists from `&SET;...` keys.
- Do not import annotations, thumbnails, system settings, or embedded binary assets.
- Do not implement chart matching against gig set lists in this change.
- Do not generate `.4ss` set list exports in this change.
- Do not integrate with forScore iCloud backups in this change.

## Decisions

### Use a Snapshot Model

Store each successful import as a distinct library snapshot with child chart records. Mark the latest successful import active for the user and deactivate previous snapshots.

Alternatives considered:
- Merged catalogue: Upsert charts into a persistent per-user library. This needs identity and deletion rules before they are valuable, especially when titles, filenames, and forScore identifiers can diverge.
- Parse-only without persistence: Simpler, but later matching needs a stable catalogue and would require repeated uploads.

Rationale: A snapshot mirrors the user action, avoids premature merge semantics, and gives matching a clear source of truth.

### Parse Only Required Score Metadata

The parser will scan for the gzip magic bytes, decompress the payload, parse the binary plist, and extract score records from keys of the form `<file path>|<field>`. A record is chart-eligible when it has a non-empty file path and title. Optional fields such as keywords, added timestamp, print number, and version are retained when present.

Alternatives considered:
- Hardcode the gzip offset observed in the sample. This is brittle if wrapper metadata length changes.
- Convert and inspect the full plist as XML. This is useful for diagnostics but unnecessary for application parsing.
- Parse all plist content. This expands scope into annotations, set lists, and app settings that are not needed for issue 176 phase one.

Rationale: Scanning for the gzip stream and parsing the plist directly is robust enough for observed exports while keeping the implementation narrowly scoped.

### Ignore Existing forScore Set Lists

The import workflow will skip `&SET;...` keys. Imported Glovelly gig set lists remain the source for later matching.

Alternatives considered:
- Import forScore set lists into Glovelly. This is tempting because the backup contains them, but it changes the product workflow and overlaps with existing gig set list imports.

Rationale: Issue 176 needs a chart library to match against, not another set list source.

### Store Normalized Matching Fields Early

Each chart record will store raw title/file path values plus a normalized title used for deterministic matching later. Initial normalization should be conservative: trim, case-fold, collapse whitespace, remove file extensions where applicable, and normalize punctuation consistently with later matching rules.

Alternatives considered:
- Compute normalization only at match time. This reduces storage but makes import validation and future query indexing weaker.
- Store heavily parsed musical dimensions now. Prefixes, catalogue numbers, keys, and roles are useful later but can be derived in the matching phase once requirements are clearer.

Rationale: Normalized title is a stable phase-one output and a natural future index; deeper semantic parsing can wait.

### Keep Uploaded Backup Contents Out of Long-Term Storage

Store chart metadata and import diagnostics, not the original `.4sb` binary, unless a future requirement explicitly needs retained source files.

Alternatives considered:
- Store the uploaded backup as an attachment/blob. This helps debugging but retains more user data than needed and increases privacy/storage risk.

Rationale: The product requirement is interoperability metadata extraction, not backup management.

## Risks / Trade-offs

- `.4sb` wrapper changes in future forScore versions -> Detect unsupported versions, scan for gzip rather than hardcoding offsets, and return actionable parse errors.
- Binary plist values vary by export/version -> Treat optional fields defensively and require only file path plus title for chart records.
- Large backups could be memory-heavy -> Enforce upload size limits and parse within request limits or move to a bounded service if needed.
- Duplicate chart titles or file paths can exist -> Preserve raw file path and import all eligible records; matching/review will resolve ambiguity later.
- Legal sensitivity around reverse engineering -> Keep the implementation read-only, user-supplied, interoperability-focused, and avoid modifying `.4sb` data.
- Snapshot replacement can remove a previously active catalogue -> Keep historical snapshots for audit/review unless storage pressure later requires pruning.

## Migration Plan

Add new EF entities and database migration for library snapshots and chart records. The migration is additive and should not affect existing gig, set list, invoice, or Google integration data.

Rollback can remove the new endpoints from the UI/API while leaving tables unused. A database rollback would drop only the new snapshot/chart tables if required before production use.

## Open Questions

- What maximum `.4sb` upload size should be accepted for the first release?
- Should the UI expose historical snapshots immediately, or only show the active import plus latest status?
- Should parse warnings include ignored set list counts, or is that unnecessary noise for phase one?
