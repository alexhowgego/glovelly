## 1. Matching Library Project

- [x] 1.1 Add `backend/Glovelly.Matching` and `backend/Glovelly.Matching.Tests` projects to the solution using existing central package/version conventions.
- [x] 1.2 Implement `MatchText`, `MatchTextNormalizer`, and deterministic normalization forms for original, canonical, compact, and tokenized text.
- [x] 1.3 Implement string similarity component scores for compact equality, token overlap, edit-distance similarity, and best score.
- [x] 1.4 Add focused matching library tests for `L-O-V-E`/`LOVE`, `Jump Jive & Wail`/`Jump Jive And Wail`, descriptor-heavy titles, and dissimilar title pairs.

## 2. Candidate Retrieval

- [x] 2.1 Refactor current set list chart matching so domain-specific candidate retrieval is separate from final ranking/selection.
- [x] 2.2 Extract chart numbers and key suffixes from set list row pad/chart fields and from forScore chart metadata, file paths, titles, keywords, and print numbers.
- [x] 2.3 Include exact chart-number candidates and nearby `number - 1` / `number + 1` candidates with explicit evidence labels and weaker confidence.
- [x] 2.4 Include title candidates using matching-library compact forms, canonical forms, token similarity, edit similarity, file path/title containment, and keyword/title evidence.
- [x] 2.5 Add candidate caps and deduplication by evidence bucket so retrieval is high-recall but bounded for contextual ranking.

## 3. Contextual Ranking Abstraction

- [x] 3.1 Add ranker input/output DTOs for full set list context, per-row candidates, evidence labels, selected candidate id, status, confidence, and user-facing reason.
- [x] 3.2 Implement deterministic contextual ranker fallback that prefers exact chart number, then compatible title/context evidence, and marks conflicts as needing review.
- [x] 3.3 Add provider abstraction/configuration seam for optional GCP Vertex AI/Gemini contextual ranking without requiring live LLM calls in default environments.
- [x] 3.4 Validate ranker outputs so unknown chart IDs, malformed decisions, or missing rows fall back to deterministic or needs-review results.
- [x] 3.5 Add `IGeminiContentGenerator` abstraction and `GeminiContentGenerator` real implementation wrapping `PredictionServiceClient` for testability.
- [x] 3.6 Implement `VertexAiSetListChartContextualRanker` using `Google.Cloud.AIPlatform.V1` SDK, with prompt construction, JSON response parsing, markdown code-block stripping, and deterministic fallback on any error or invalid output.
- [x] 3.7 Wire conditional DI: register Vertex AI ranker when `SetListChartRanking:Provider` is `VertexAi` and required GCP config is present, defaulting to `DeterministicSetListChartContextualRanker` otherwise.
- [x] 3.8 Update config samples, deployment parameters, and docs for optional Vertex AI/Gemini ranking settings.
- [x] 3.9 Add structured logging for ranker provider selection, candidate retrieval, Vertex AI calls, validation failures, and deterministic fallback reasons without logging prompts or row titles.
- [x] 3.10 Tolerate common Gemini response wrappers, including object-wrapped decisions and prose before JSON, while keeping deterministic fallback for invalid responses.
- [x] 3.11 Treat Gemini-selected unknown chart IDs as row-level needs-review results instead of invalidating the whole ranking response.

## 4. API And UI Integration

- [x] 4.1 Update set list import preview and retroactive match preview to use high-recall retrieval plus contextual ranking.
- [x] 4.2 Enrich match candidates/results with evidence and contextual reason fields without allowing LLM-selected charts outside supplied candidate IDs.
- [x] 4.3 Update UI labels to distinguish chart-number, title-similarity, and set-context match reasons where useful.
- [x] 4.4 Preserve existing manual review and save behavior for ambiguous, missing, low-confidence, or conflicting matches.
- [x] 4.5 Add staged import progress feedback for loading worksheets, parsing Google Sheet rows, interpreting the set list, and saving the import.
- [x] 4.6 Split initial Google Sheet preview from AI/chart interpretation by adding an explicit draft chart-matching endpoint and a separate “Match charts with AI” action in the import modal.
- [x] 4.7 Consolidate set list import/review entry points into one “Manage set list” dialog with active-import loading, after-save review, AI matching, attention summary, and highlighted rows requiring review.
- [x] 4.8 Rename row preview action to “Import rows”, warn before replacing rows from a saved set list, preserve worksheet selection context where possible, and disable save when an active set list has no changes.
- [x] 4.9 Persist the latest per-row forScore match result JSON so reopening a saved set list preserves the candidate dropdown from the prior AI/deterministic matching pass.
- [x] 4.10 Split chart interpretation into deterministic “Locate candidates” and premium-ready “Ask AI to choose” actions, and confirm before either action modifies rows from an active saved set list.
- [x] 4.11 Remove the broken “Review first issue” shortcut and add spacing between worksheet selection and set list management actions.

## 5. Regression Tests And Samples

- [x] 5.1 Add API/service tests proving `LOVE` retrieves `L-O-V-E` candidates and `Jump Jive & Wail` retrieves `Jump Jive And Wail` candidates.
- [x] 5.2 Add tests proving chart number evidence outranks title-only duplicate candidates when set/list context supports that choice.
- [x] 5.3 Add tests proving nearby chart-number candidates are included but do not override conflicting title/context evidence.
- [x] 5.4 Add tests proving invalid ranker/LLM outputs cannot save or suggest invented chart IDs.
- [x] 5.5 Update UAT documentation with contextual matching expectations and reviewer guidance.
- [x] 5.6 Add Vertex AI ranker tests covering: valid JSON response, empty response, malformed JSON, unknown chart IDs, missing row numbers, markdown code-block wrapping, status/confidence normalization, SDK exception fallback, and no-song-row passthrough.
- [x] 5.7 Run `dotnet test glovelly.sln -m:1`, `npm --prefix frontend/glovelly-web run lint`, and `npm --prefix frontend/glovelly-web run build`.
