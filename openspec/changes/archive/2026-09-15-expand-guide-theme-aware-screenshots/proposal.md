## Why

The public guide currently uses only three light-theme screenshots, leaving several task-led pages without a visual reference and making the screenshots visually inconsistent when a visitor selects the guide's dark theme. The deterministic fixture and review-first capture workflow are now reliable enough to expand this coverage safely.

## What Changes

- Add purposeful real-product screenshots to the Clients, Expenses, Seller profile, and Settings guide pages, alongside the existing gig and invoice views.
- Publish a light and actual-dark Glovelly UI variant for every guide screenshot, selecting the matching image from the active Starlight light or dark theme while retaining one shared explanation.
- Extend the documentation capture suite to produce deterministic light and dark candidates for all seven selected workflow states.
- Preserve responsive image presentation, meaningful alternative text, isolated fixture reset/cleanup, and non-blocking screenshot freshness reporting.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `public-user-guide`: Add theme-aware, accessible product imagery to selected core workflow and setup guidance.
- `deterministic-documentation-captures`: Capture and review light and dark variants for the expanded documented workflow set.

## Impact

- `frontend/glovelly-guide` page content, screenshot assets, and guide-local CSS.
- `tests/Glovelly.Uat.Tests/DocumentationCaptureTests.cs` and its deterministic browser setup.
- Existing staging documentation capture artifact comparison and PR freshness comment; no new runtime dependencies, public APIs, or production data are introduced.
