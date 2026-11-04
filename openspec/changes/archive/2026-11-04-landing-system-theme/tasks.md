## 1. System-driven palette

- [x] 1.1 Audit landing-page and consent UI colours, existing guide palette, illustration inheritance, interactive states and browser colour metadata.
- [x] 1.2 Introduce semantic light and guide-aligned dark page colours using CSS system preference, retaining light as the fallback and updating immediately when the preference changes.
- [x] 1.3 Adapt all page sections, controls and decorative compositions for both palettes, keeping intentional paper surfaces and their internal colours legible; review native controls and theme metadata.
- [x] 1.4 Preserve reduced-motion and responsive behavior without introducing a theme selector, theme preference storage, theme-specific JavaScript or changes to the guide or Menu.

## 2. Documentation and verification

- [x] 2.1 Document the system-only decision, light fallback and independent guide preference in appropriate maintainer-facing documentation.
- [x] 2.2 Verify light and dark appearances on mobile and desktop, including all illustrations, consent UI, navigation, focus, reduced motion, no horizontal overflow and live preference changes; record results and any deferred staging/CI checks.
- [x] 2.3 Check text, control boundaries and focus indicators against the contrast targets in the design, correcting failures in both appearances.
- [x] 2.4 Run landing-site check and build; add or update suitable automated coverage where practical without running local Playwright UAT or documentation captures.

Verification: Chrome headless review at 1440 × 900 and 390 × 844 covered light/dark, live preference changes, reduced motion, layout and cookie-settings focus. The production-only consent loader was injected locally with its host guard disabled for review; confirm the real consent experience on production after deployment. Automated token contrast tests and the landing check/build passed. Local Playwright UAT and documentation captures were not run.
