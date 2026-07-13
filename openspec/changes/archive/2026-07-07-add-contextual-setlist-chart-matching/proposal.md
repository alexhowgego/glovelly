## Why

The current set list chart matcher works mechanically, but title-first fuzzy matching is too brittle for real forScore libraries where chart titles vary (`L-O-V-E` vs `LOVE`, `&` vs `and`) and duplicate chart titles exist across bands or clients. Matching needs to become high-recall and context-aware so Glovelly can prefer catalogue numbers, library path/tag patterns, and whole-set context before asking the user to review ambiguous results.

## What Changes

- Add a narrow internal matching library project for deterministic text normalization and string similarity utilities, with dedicated unit tests and no Glovelly domain model dependencies.
- Replace title-first candidate selection with high-recall candidate retrieval that uses chart numbers, nearby chart numbers, normalized/compact title forms, token similarity, file paths, keywords, keys, and set-level context signals.
- Introduce contextual ranking as a bounded step that chooses only from supplied candidate chart IDs and can later call a GCP-hosted LLM provider such as Vertex AI Gemini when configured.
- Persist or expose match evidence and rationale so users can see why a chart was suggested, especially for chart-number and whole-set-context matches.
- Keep deterministic fallback behavior for local development, tests, and environments without LLM configuration.
- Preserve manual user review for ambiguous, missing, low-confidence, or conflicting results.

## Capabilities

### New Capabilities

- `deterministic-text-matching`: Pure text normalization and similarity primitives used by higher-level matching workflows.
- `contextual-chart-ranking`: Bounded contextual ranking of retrieved forScore chart candidates using whole-set evidence and optional LLM assistance.

### Modified Capabilities

- None.

## Impact

- Solution structure: add `backend/Glovelly.Matching` and `backend/Glovelly.Matching.Tests` projects and reference them from the API/tests.
- Backend services: split current matcher responsibilities into deterministic text utilities, domain candidate retrieval, and contextual ranking.
- Optional cloud integration: add provider abstraction for future Vertex AI/Gemini ranking without making LLM configuration mandatory.
- API response shape: enrich match candidates/results with evidence and contextual reasons where needed.
- Tests: add focused text matching tests, candidate retrieval tests using real-world examples, ranker contract tests, and regression coverage for `L-O-V-E`/`LOVE`, `Jump Jive & Wail`/`Jump Jive And Wail`, duplicate-title context, and chart-number-first matching.
