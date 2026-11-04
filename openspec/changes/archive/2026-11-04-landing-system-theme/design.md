## Context

The independently deployed Astro landing site uses a fixed light palette, with both root variables and hard-coded colours throughout its page and CSS illustrations. The Starlight guide has branded light and dark palettes and an independent manual theme selector. The landing page should be unobtrusive and remain static, without changing either the guide or authenticated Menu.

## Goals / Non-Goals

**Goals:**
- Respect the system colour preference at initial render and when it changes.
- Retain a coherent Glovelly visual identity and accessible light and dark presentations.
- Document system-only behaviour and verify it across viewport sizes.

**Non-Goals:**
- Add a manual selector, shared cross-origin preference, tracking, authentication, or theme-specific JavaScript.
- Change the guide or Menu theme systems, or establish a shared component package.

## Decisions

1. **Use CSS `prefers-color-scheme`.** Define the existing light presentation as the default and override semantic page colours for dark preference, so browser preference changes apply without a reload or script. A manual selector and persistent preference were rejected as unnecessary controls and state for a short landing page; light-only would disregard dark-preferring visitors. A missing or unsupported preference falls back to light.
2. **Align semantic colours, not every decorative pixel.** Use the guide's dark navy, warm text and muted teal accents as a reference, assigning semantic tokens for page surfaces, copy, links, buttons, borders and focus. Audit hard-coded colours in all sections. Treat illustrations as intentionally light paper objects where appropriate, explicitly maintaining their internal foreground, border and accent contrast instead of blindly inheriting dark-page tokens. Retain the existing light art direction. Review native controls and browser theme colour where relevant.
3. **Keep preferences independent.** The guide's selector is its own origin-scoped setting and is not read or synchronized by the landing page. Document this decision for maintainers rather than presenting another control to visitors.
4. **Verify actual rendered combinations.** Check normal text at least 4.5:1 and large text at least 3:1, and meaningful control boundaries and focus indicators at least 3:1 against adjacent colours. Review mobile and desktop, both modes, keyboard focus, preference changes, and reduced motion. Include the existing consent UI where it appears, without changing its analytics policy. Run the landing site's check and build; do not run local Playwright UAT or documentation captures.

## Risks / Trade-offs

- [Guide manually set differently from system] → Accept a possible cross-site appearance change and document the independent preference model.
- [Inherited page tokens make light illustrations unreadable] → Explicitly scope illustration colours and inspect all compositions in both modes.
- [Hard-coded colours or consent UI remain light or lose contrast] → Audit the complete rendered page and interactive states, not just root variables.
- [Automated checks cannot establish visual quality] → Record manual mobile and desktop visual review results, including any checks deferred to CI or staging.

## Migration Plan

Ship through the existing static landing-site build and deployment; no stored preference or data migration is needed. Roll back by redeploying the prior landing-site artifact if visual regressions occur.
