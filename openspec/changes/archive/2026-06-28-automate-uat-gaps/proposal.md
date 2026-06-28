## Why

Glovelly's manual UAT packs identify several high-value browser journeys where backend tests already protect business rules, but UI orchestration can still regress unnoticed. Automating the most deterministic gaps will reduce release risk around cross-workspace navigation, dirty editor guards, modal workflows, uploads, and prompt-driven state changes.

## What Changes

- Add browser-level UAT coverage for deterministic manual regression journeys that currently depend on human checks.
- Prioritise UI orchestration flows where the backend can be correct while the product experience is broken, including cross-workspace navigation, unsaved-edit prompts, imported-gig review, attachments, receipts, expense statements, and selected invoice prompts.
- Keep environment-dependent Google OAuth, Calendar worker, real Sheets, real Drive, and real email checks as manual or environment UAT unless they can be exercised safely through existing test-auth or controlled staging configuration.
- Update matching `docs/uat` automation status lines as browser UAT coverage is added.

## Capabilities

### New Capabilities
- `uat-browser-automation`: Browser-level UAT coverage for high-value manual regression journeys, including requirements for deterministic coverage, isolation, and documentation alignment.

### Modified Capabilities
- None.

## Impact

- Adds or expands Playwright/xUnit tests under `tests/Glovelly.Uat.Tests`.
- May add shared UAT helpers for seeded browser workflows, test data creation, file uploads, prompt handling, and modal assertions.
- Updates UAT documentation under `docs/uat` to reflect new automation coverage.
- Does not change production APIs or user-facing application behavior except where implementation uncovers defects that require separate fixes.
