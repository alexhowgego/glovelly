## Context

The first set list chart matching implementation links set list song rows to forScore chart records and supports import-time and retroactive review. It currently relies on title-first deterministic matching, which is not robust enough for real libraries. The supplied Bella/Saara examples show that chart numbers, key suffixes, title aliases (`L-O-V-E` vs `LOVE`, `&` vs `and`), file path/catalogue patterns, and whole-set context are stronger signals than title equality alone.

This change adds a narrow deterministic matching library and restructures the API-side matcher into two stages: high-recall candidate retrieval and bounded contextual ranking. The ranking stage must choose only from supplied candidates, with deterministic fallback now and an optional GCP-hosted LLM provider later.

## Goals / Non-Goals

**Goals:**

- Add `backend/Glovelly.Matching` with pure text normalization and string similarity primitives that do not depend on API/domain models.
- Add focused tests in `backend/Glovelly.Matching.Tests` for punctuation, compact forms, token overlap, edit similarity, and common chart-title variants.
- Improve forScore chart candidate retrieval to favor recall using chart numbers, nearby chart numbers, normalized/compact titles, token similarity, file paths, keywords, keys, and context evidence.
- Add a contextual ranking abstraction that accepts set-level context plus candidate IDs/evidence and returns structured match decisions and reasons.
- Keep deterministic fallback behavior so matching works without LLM configuration.
- Prepare a Vertex AI/Gemini provider seam without requiring live cloud calls for local development or normal tests.

**Non-Goals:**

- Letting an LLM search the full chart library directly or invent chart records.
- Replacing human review for low-confidence, ambiguous, or conflicting matches.
- Generating forScore `.4ss` set lists.
- Creating a stable cross-snapshot chart catalogue.
- Persisting full LLM prompts/responses as required product data in this first pass.

## Decisions

### Add A Narrow Internal Matching Project

Create `backend/Glovelly.Matching` and `backend/Glovelly.Matching.Tests`. The matching project will expose reusable primitives such as `MatchText`, `MatchTextNormalizer`, and `StringSimilarity`. It will not reference EF, ASP.NET Core, `GigSetListItem`, `ForScoreChart`, or any Glovelly domain model.

Alternative considered: keep utilities inside `Glovelly.Api.Services`. That is faster initially but makes deterministic text behavior harder to test independently and invites domain matching logic to mix with low-level normalization.

Alternative considered: add `FuzzySharp`. This may be useful later, but rolling a small internal implementation avoids taking an unmaintained dependency before the actual algorithms needed are clear.

### Split Retrieval From Ranking

The API will use matching primitives in a domain-specific candidate retriever. The retriever's responsibility is high recall: include plausible candidates and attach evidence. A separate ranker chooses the best candidate, marks a row as needing review, or reports no match.

Alternative considered: make the deterministic matcher decide final matches directly. Real examples show that title matches can be actively misleading when multiple catalogue contexts contain the same song title.

### Make Chart Number Evidence First-Class

Set list row numbers such as `61-E`, `17`, and `104` are often stronger identifiers than song title. The retriever will extract chart numbers from set list rows and chart metadata/file paths, include exact matches, and include nearby `number +/- 1` candidates with weaker evidence for human-entry mistakes.

Alternative considered: treat chart numbers as another scoring boost. This underweights the strongest signal in the observed Bella workflow.

### Use Contextual Ranking With Bounded Inputs

The contextual ranker will receive the whole set list, inferred set context, and per-row candidate IDs/evidence. It must return structured decisions that reference only supplied candidate IDs. This supports an optional Vertex AI/Gemini implementation while keeping deterministic ranker behavior available.

Alternative considered: ask an LLM to inspect the user's library freely. That is harder to secure, test, bound, and explain, and it risks hallucinated chart selections.

### Keep User Review In The Loop

High-confidence contextual matches can be suggested or selected according to existing mapping behavior, but ambiguous, low-confidence, missing, or conflicting results remain reviewable. Match reasons should explain whether the evidence came from chart number, title alias, string similarity, or set context.

Alternative considered: auto-save all LLM high-confidence matches. Until real-world accuracy is proven, saving without review is too risky because wrong chart versions can be worse than no match.

## Risks / Trade-offs

- High-recall retrieval creates noisy candidate pools -> cap candidates per evidence bucket, deduplicate, and pass evidence labels so ranking can reject weak candidates.
- LLM latency or outage affects preview UX -> keep deterministic ranker fallback and design provider calls behind a service interface.
- LLM nondeterminism makes tests brittle -> unit-test prompt/request shaping and deterministic fallback; avoid live Vertex calls in normal test runs.
- Chart number `+/- 1` may suggest wrong charts -> label nearby-number evidence as weak and never let it override conflicting title/context evidence alone.
- Separate project adds solution overhead -> keep the library tiny and pure, with no domain abstractions.

## Migration Plan

- Add new projects and project references without database changes.
- Introduce new candidate/ranker services behind existing set list matching endpoint behavior.
- Extend match DTOs with evidence/reasons additively so existing clients remain compatible where possible.
- Add optional configuration for an LLM ranker provider, defaulting to deterministic ranking when unset.
- Rollback is code-only: switch configuration back to deterministic fallback or remove the ranker provider wiring.

## Open Questions

- Should LLM ranking be synchronous during preview initially, or introduced only as an async match-run flow?
- What confidence threshold is high enough to preselect a contextual match versus only marking it as suggested?
- Should match evidence/ranker output be persisted for audit/debugging, or only returned in preview results for this iteration?
