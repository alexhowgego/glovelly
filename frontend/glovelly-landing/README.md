# Glovelly Landing Site

The public landing site is a standalone Astro project. It is intentionally independent from the authenticated React application and does not call the Glovelly API, initialise authentication, set application storage, use analytics, or embed third-party media.

## Commands

Run the landing site from the repository root:

```bash
./run-landing.sh
```

The script installs dependencies through an isolated temporary npm cache before starting Astro. This avoids a broken or permission-conflicted shared `~/.npm` cache preventing local development.

For one-off checks and production builds:

```bash
npm --prefix frontend/glovelly-landing --cache "${TMPDIR:-/tmp}/glovelly-npm-cache" ci
npm --prefix frontend/glovelly-landing run check
npm --prefix frontend/glovelly-landing run build
```

## Public Visual Direction

- Editorial display type: Fraunces-style serif, with a practical sans-serif for interface text.
- Colour: warm paper neutrals, deep blue structure, restrained orange action accents.
- Compositions: semantic HTML and CSS that represent product moments; they are not application screenshots.
- Motion: no required animation; reduced-motion users receive no smooth scrolling or motion transitions.
- Accessibility: compositions are decorative, while page copy, landmarks, headings, controls, and focus states carry the meaning.

## Release Checks

Before the `glovelly.net` cutover, verify the Firebase preview, keyboard navigation, mobile and desktop layouts, and every public destination. The apex must remain on its current mapping until the Menu migration and overlap window documented in `docs/engineering/public-surface-routing.md` are complete.

### Manual Accessibility And Responsive Checklist

- Navigate the page with a keyboard and confirm the skip link, header, calls to action, and footer links show a visible focus state.
- Check the page at narrow mobile, tablet, and desktop widths for horizontal overflow, clipped navigation, or an incorrect reading order.
- Enable reduced motion and confirm the page does not use smooth scrolling or transition-based movement.
- Confirm the decorative interface compositions do not add duplicate screen-reader content.
