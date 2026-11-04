## Why

The public landing page is always light, even when a visitor prefers a dark appearance, while the user guide offers a branded dark palette. Respecting the system preference makes the transition more comfortable without adding a distracting theme control to the landing page. This follows up on #277.

## What Changes

- Make the landing page follow the browser or device light/dark preference, including changes while the page is open.
- Adapt the page's colours to the guide's established visual direction while preserving its warm, paper-like illustrations and accessible contrast.
- Keep the landing page free of a manual theme selector, theme preference storage, and theme-specific client runtime; document the decision and its independence from the guide's manual setting.
- Verify both appearances at mobile and desktop sizes, including keyboard focus and reduced-motion behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `public-landing-site`: The public landing page responds to the system colour preference while maintaining its accessible visual identity and static operation.

## Impact

- Landing-site Astro markup and CSS, plus relevant public-site documentation and verification.
- Guide palette provides a visual reference; the guide and authenticated Menu theme systems remain unchanged.
- No API, authentication, tracking, shared component package, or new runtime dependency is introduced.
