## Context

The static Starlight guide currently publishes three raw application captures: the gig workspace, invoice status, and invoice email-review dialog. All are captured at a fixed desktop viewport with Glovelly's light theme. The guide itself has a user-selectable light/dark theme, so its dark presentation currently frames a light application UI.

The staging-only `Glovelly Docs` fixture already resets and seeds one client, one invoiced gig with mileage and an expense, one draft invoice, a complete seller profile, and user defaults. The existing capture suite authenticates only through that fixture, freezes the browser clock, and resets in `finally`. CI compares named candidates with checked-in assets and reports drift without blocking a pull request.

## Goals / Non-Goals

**Goals:**
- Give the clients, expenses, seller profile, and settings task pages a real, purposeful visual reference.
- Provide a light and actual Glovelly dark UI asset for each documented product state, selected by the active Starlight theme without duplicating captions or instructional prose.
- Keep all fourteen assets reproducible from the existing isolated fixture and visible to the established non-blocking freshness review.
- Preserve meaningful alternative text and fluid image sizing in the guide.

**Non-Goals:**
- Capture sign-in, access requests, overview/help pages, every UI control, mobile-specific product screenshots, receipts with real uploads, or external-service outcomes.
- Add product themes beyond Glovelly's `light` and `dark` variants, change the guide's own theme system, or turn screenshot drift into a merge gate.
- Add an Astro component library or a new visual-regression service.

## Decisions

### Pair static assets by Glovelly light and dark preference

For each selected state, publish a consistently named pair such as `client-workspace-light.png` and `client-workspace-dark.png`. The existing three states are renamed/replaced as paired assets and four states are added: client workspace, gig expenses/mileage, seller profile, and user settings.

The capture suite will set the Glovelly `glovelly.theme-preference` local-storage value before the application starts, once for `light` and once for `dark`. It will retain a fixed browser colour-scheme, locale, timezone, viewport, reduced motion, and frozen clock; the requested asset variant is therefore controlled by product preference rather than the runner's operating-system setting.

Alternatives considered:
- Capture only light UI: rejected because dark-guide visitors see a mismatched product reference.
- Capture every Glovelly theme: rejected because the guide has only light/dark modes and additional themes multiply assets without helping a guide task.
- Use `system` preference: rejected because it makes an asset dependent on browser media emulation rather than an explicit, durable product theme.

### Select images with Starlight's active theme attribute

Each guide figure will contain its paired images and one shared `figcaption`. Guide-local CSS will display only the light image below the guide's light `[data-theme]` state and only the dark image below its dark state. Hidden images will not be exposed to assistive technology; the visible image carries the meaningful alternative text.

This is intentionally CSS-based: Starlight changes a document theme attribute when its visitor-facing toggle changes. A `<picture>` `prefers-color-scheme` media query would follow operating-system preference instead, which can disagree with the guide's selected theme. One figure also prevents duplicated captions and instructional content.

Alternatives considered:
- `<picture>` with `prefers-color-scheme`: rejected because it cannot observe Starlight's persisted theme choice.
- Client-side JavaScript image switching: rejected because the guide is static and CSS already responds to the source of truth.
- Duplicate figures per theme: rejected because it duplicates explanatory content and creates avoidable accessibility noise.

### Extend the existing capture and review path, not its safety boundary

The documentation-capture test will add stable navigation and modal interactions for the four additional states, capturing controlled elements after the application is settled. It will continue to avoid send actions and uploaded receipts. Candidate names will exactly match the checked-in paired asset names so the comparison script can first fast-path byte-identical files, then compare decoded pixels for byte-different PNGs. Pixel-identical re-encodings are reported as current; any nonzero pixel difference remains reviewable with its changed-pixel count and generated visual diff in the existing artifact and PR comment flow.

The fixture reset before authentication and in `finally` remains unchanged. Its existing seeded client, gig/expense/mileage, seller profile, and defaults are deliberately the source of all new states, avoiding fixture data with personal or external-service content.

## Risks / Trade-offs

- [A guide theme toggle shows both or neither asset] -> Scope selectors directly to Starlight's `[data-theme]` values and verify light and dark guide builds/manual previews.
- [A product asset starts in the wrong theme] -> Set the explicit local-storage preference through Playwright init scripts before navigation and assert the expected application theme before capture.
- [New modal or detail captures are unstable] -> Use existing test IDs or stable semantic selectors, wait for visibility and settled fonts/data, and keep the same frozen browser conditions.
- [A future workflow adds user-owned data that the fixture reset misses] -> Continue using the ownership-scoped reset and extend it with the captured workflow, as required by the existing fixture contract.
- [PNG encoding changes create noisy review requests] -> Compare decoded pixels after the byte-identical fast path, report pixel-identical re-encodings as current, and generate reviewable diffs only for nonzero pixel changes.
- [Fourteen binary assets increase review overhead] -> Keep only task-completing states, name pairs predictably, and retain review-first CI rather than automatically publishing candidates.

## Migration Plan

1. Extend capture coverage and create paired candidates from staging.
2. Review the artifact, then add the approved fourteen PNGs under the guide's public screenshot directory and replace the three unpaired references.
3. Build and check the guide; review each documented page in light and dark at narrow and wide layouts.
4. Subsequent same-repository pull requests continue to report candidate drift non-blockingly. Roll back an unsuitable image by restoring its last approved checked-in pair; no application or data migration is required.

## Open Questions

None. The selected dark variant is Glovelly's actual `dark` preference, not one of its additional product themes.
