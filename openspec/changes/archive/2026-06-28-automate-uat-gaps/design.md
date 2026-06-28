## Context

Glovelly has manual UAT packs in `docs/uat` and browser-level Playwright/xUnit UAT tests in `tests/Glovelly.Uat.Tests`. Existing automated UAT coverage is strongest around invoice generation, invoice previews, mileage, dashboard summary, aggregation, and one expense statement happy path. Many remaining manual journeys are already protected by backend integration tests for server-side rules, but still depend on manual browser checks for UI orchestration: selected-record routing, stale search filters, dirty editor prompts, modal entry points, autosave, quick capture, upload/download controls, and prompt-driven state transitions.

The UAT tests run against a configured deployment via `GLOVELLY_UAT_BASE_URL` and authenticate through the staging-only `GLOVELLY_UAT_SECRET` test-auth path. They create timestamped records in shared environments and use diagnostic screenshots/traces on failure.

## Goals / Non-Goals

**Goals:**

- Add deterministic browser-level coverage for the highest-value manual UAT gaps identified in the existing packs.
- Reuse existing UAT helpers where practical and add small focused helpers only when they reduce duplication or flakiness.
- Keep tests isolated by creating run-specific records and avoiding broad assumptions about existing staging data.
- Prefer waiting on visible UI state or specific network responses over fixed sleeps.
- Keep `docs/uat` automation labels aligned with the implemented Playwright coverage.

**Non-Goals:**

- Do not automate real Google OAuth consent, asynchronous Calendar worker verification, real Google Sheets content access, real Drive publishing, or uncontrolled email inbox verification as part of this change.
- Do not change production behavior unless automation exposes an existing defect that must be fixed separately.
- Do not replace all manual UAT. Manual checks remain valuable for environment configuration, visual judgement, and external service integration.
- Do not introduce a new frontend test framework; continue using the existing Playwright/xUnit UAT project.

## Decisions

1. Prioritise browser orchestration over additional API coverage.

   Backend tests already cover many business rules named in the UAT packs. The highest remaining risk is the browser state between API calls: workspace selection, filters, modal state, prompts, and immediate UI refresh. New tests should assert these interactions directly rather than duplicating backend-only validation.

   Alternative considered: add more backend tests for every manual item. That would be cheaper and faster, but would not catch the regressions these UAT packs are designed to reveal.

2. Add coverage in focused workflow tests rather than one large pre-merge script.

   Focused tests make failures easier to diagnose, reduce cascading failures, and align better with the existing test classes. Candidate groupings are cross-workspace navigation, editor discard guards, imported gig review, attachments/receipts, expense statement variants, and invoice prompt choices.

   Alternative considered: one full manual-checklist automation. That would mirror the tester journey, but it would be brittle, slow, and harder to recover when one early step fails.

3. Use run-specific data and controlled setup paths.

   Tests should create clients, gigs, invoices, expenses, attachments, and staged imports using unique run IDs. Where browser setup is expensive or not the subject under test, direct authenticated fetch calls may be used for setup if the visible journey is still exercised through the UI.

   Alternative considered: rely on seeded UAT data. Seeded data is useful for manual testing, but shared mutable records increase flakiness and make tests order-dependent.

4. Treat external-service checks as boundaries.

   Browser automation may verify disconnected/connected UI states when they can be produced safely, but real OAuth consent, Calendar propagation, Google Sheets reads, and Drive/email delivery should remain manual or environment-specific unless stable test doubles already exist in the deployed UAT environment.

   Alternative considered: automate all external service paths. That would provide more coverage but risks flaky tests caused by third-party state, credentials, rate limits, and asynchronous workers.

5. Update UAT documentation with each automated journey.

   Each new or expanded Playwright test should update the nearest `docs/uat` automation status line. This keeps release reviewers aware of which parts remain manual and prevents stale claims of coverage.

   Alternative considered: leave docs unchanged until all automation is complete. That would make intermediate coverage ambiguous and reduce the usefulness of the UAT packs during rollout.

## Risks / Trade-offs

- Shared UAT data contamination -> use unique run IDs, avoid destructive operations on non-test records, and restore any shared user settings changed during a test.
- Browser prompt flakiness -> attach dialog handlers only around the action under test and explicitly cover accept and decline paths in separate flows where practical.
- Autosave/polling flakiness -> wait for network responses or stable UI count/status changes instead of sleeping for arbitrary durations.
- Slow UAT suite growth -> prioritise a small number of high-signal tests and avoid exhaustive field combinations already covered by backend integration tests.
- File upload/download variability -> use tiny generated or checked-in fixtures and assert metadata plus non-empty downloads rather than relying on OS-specific file picker behavior.
- Documentation drift -> include UAT doc updates in the task list for every automated journey.
