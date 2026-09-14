# Glovelly User Guide

The public user guide is a standalone Astro/Starlight project for musicians and sole traders. It uses a practical, task-led "do this next" voice. The technical/operator [Glovelly Handbook](https://handbook.glovelly.net) remains a separate DocFX site with concise reference material.

## Commands

Run the guide from the repository root:

```bash
./run-guide.sh
```

For checks and a production build:

```bash
npm --prefix frontend/glovelly-guide --cache "${TMPDIR:-/tmp}/glovelly-npm-cache" ci
npm --prefix frontend/glovelly-guide run check
npm --prefix frontend/glovelly-guide run build
```

Preview the built guide with `npm --prefix frontend/glovelly-guide run preview`.

## Screenshots

Published screenshots live in `public/screenshots/` and are reviewed repository assets. The staging documentation-capture job produces candidate replacements and reports drift on same-repository pull requests without modifying the branch.

To review a candidate, download the `glovelly-documentation-screenshots` artifact from the workflow run, inspect its report and PNGs, then replace approved files in `public/screenshots/` in the pull request. Do not copy screenshots from production or use personal data.

## Release checks

The `guide.yml` workflow creates Firebase Hosting previews for same-repository pull requests and deploys `main` to the `user-guide` target. Before attaching or changing `docs.glovelly.net`, verify search, keyboard navigation, narrow and wide layouts, and every public outbound link.
