## Context

The current set-list preview endpoint reads `values` from a linked Google Sheet and applies `ISetListSheetParser` before the user can review anything. That parser contains header and position heuristics for regular sheets, while real set lists are sparse visual documents: songs, medleys, personnel, instruments, durations, and notes can occupy different columns and shift over time. The resulting false titles then flow into otherwise-correct forScore candidate lookup.

The existing asynchronous chart-matching job, Vertex client pattern, review modal, and item persistence provide useful precedents. They are chart-specific, however: their input and result identity is `SourceRowNumber`, which is not sufficient for an interpreter that can cite multiple cells or rows.

## Goals / Non-Goals

**Goals:**
- Interpret arbitrary linked Google Sheet layouts as bounded coordinate-preserving documents rather than as rows in a prescribed table.
- Make AI interpretation the only automatic extraction path, with constrained Vertex output and server-side validation before review or matching.
- Preserve source data and interpretation job state for retry, traceability, and retained-source-backed manual authoring.
- Keep chart candidate retrieval deterministic, user-scoped, and strictly downstream of reviewed interpreted songs.
- Maintain existing saved-import review and export behavior while migrating item source evidence safely.

**Non-Goals:**
- Supporting Excel uploads as an import source.
- Teaching deterministic heuristics to recognise more layouts or retaining a basic parser fallback.
- Replacing local forScore candidate retrieval or allowing an interpreter to choose forScore charts.
- Treating any regression fixture as a required worksheet format.

## Decisions

### Acquire a bounded display-value grid before queueing work

The source endpoint will resolve the selected worksheet, acquire a bounded grid with A1-style coordinates, interior empty cells, display/formatted values, and source order, then persist that grid as interpretation-job input before returning an accepted job response. Grid bounds will be explicitly capped by configured rows, columns, and serialised payload size; over-limit sheets will fail with an actionable response.

The Google Sheets `spreadsheets.get` grid-data representation is preferred over the existing `values` response because the latter drops trailing cells and cannot express a complete bounded layout. The system will use formatted values rather than raw numeric values so durations, dates, and similarly formatted cells retain their document meaning.

Alternative considered: send the existing values arrays to Vertex. Rejected because sparsity and coordinate context are the information the interpreter needs.

### Use a distinct persisted interpretation job

A new user- and gig-scoped interpretation job will store grid input, status, validated result or safe failure, timestamps, and correlation ID. A dedicated queue/worker/processor follows the existing chart-job lifecycle and emits its own workspace-event scope. The status response exposes job state, source grid, and result only to its owner.

Alternative considered: generalise the existing chart-matching job. Rejected because it serialises chart-specific input/results and would conflate source interpretation with chart selection.

### Constrain Vertex output, then validate it as untrusted input

Vertex receives the complete bounded grid plus explicit extraction rules. It returns only an ordered list of logical items: `Song`, `Separator`, `Comment`, or `Transition`; inclusion; title; optional pad/key/notes/section; source rows/cells; and confidence. The model does not create persistent IDs or select charts.

The server assigns stable draft item IDs and validates enum values, title/metadata lengths, row and cell references, display-value evidence, ordering, uniqueness, and inclusion rules. Invalid schema, invalid references, or an empty/malformed response fails the job without exposing a draft. A low-confidence valid item remains reviewable; unsupported content is omitted rather than invented.

Alternative considered: accept partially valid responses. Rejected because silent omission and mixed trusted/untrusted output would make review outcomes unpredictable.

### Replace parsing fallback with retained-source-backed manual authoring

When interpretation cannot run or fails, the modal offers retry or a manual draft. Manual authoring begins with no inferred items while the owner-scoped job retains the source grid for retry and audit; the review UI prioritises the editable draft rather than rendering the raw grid. It is not a deterministic conversion of rows. The same review/save validation applies to both AI and manually created drafts.

### Decouple downstream identity from source row number

Set-list items retain a primary `SourceRowNumber` for existing imports and display compatibility, but gain structured plural source evidence. Preview and chart-matching DTOs gain a stable per-draft item ID; chart results are keyed and merged by that ID rather than `SourceRowNumber`. Saved imports continue to have persistent item IDs.

Alternative considered: use the first source row as the identity. Rejected because multi-row evidence and future layout shapes can yield collisions or ambiguous mapping.

### Remove the deterministic sheet parser

`ISetListSheetParser`, its implementation, registration, parser-specific endpoint behavior, and parser tests will be removed once the interpretation workflow is available. Existing saved set-list imports remain readable and editable because they already persist reviewed item snapshots.

## Risks / Trade-offs

- [AI provider is unavailable, slow, or returns invalid output] -> Persist jobs, use queued processing, surface safe failure state, and provide retry/manual authoring rather than parser fallback.
- [Large or visually noisy sheets cause excessive prompts or weak interpretation] -> Enforce input bounds, include coordinates and blanks within those bounds, log safe metrics, and return an actionable size-limit failure.
- [The model invents songs or promotes metadata] -> Narrow schema, explicit negative prompt rules, evidence validation, required review, confidence display, and regression assertions for known false-positive categories.
- [Raw worksheet content is sensitive] -> Persist it only in user-scoped job data, never include it in workspace events/logs, and return it only through authorised job/status flows.
- [New identity and evidence fields disrupt chart matching] -> Add fields compatibly, preserve primary row numbers, and migrate all client/result mapping atomically to stable item IDs.
- [Job records accumulate source data] -> Define a retention/cleanup policy before production deployment; saved imports retain only item-level evidence needed for audit.

## Migration Plan

1. Add interpretation-job persistence and additive item evidence/identity fields with an EF migration.
2. Deploy grid acquisition, interpreter, worker, validation, and owner-scoped APIs alongside existing saved-import read/update support.
3. Update the modal to use interpretation jobs and manual authoring; invoke candidate lookup only after an interpreted or manual draft exists.
4. Remove parser registration, preview parser endpoint path, and parser code/tests in the same change, then update UAT documentation.
5. Roll back application code if necessary; additive columns and job tables can remain, and previously saved imports continue using their stored snapshots.

## Open Questions

- What retention period should apply to completed and failed interpretation jobs that contain raw worksheet grids?
- Should an interpretation job’s raw grid remain available after an import is saved, or should the saved import retain only item-level source evidence?
