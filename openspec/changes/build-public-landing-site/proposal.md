## Why

Glovelly needs a public front door that explains its value before visitors encounter the authenticated Menu application. With the application now assigned to `menu.glovelly.net`, the apex can become a calm, distinctive introduction for working musicians and small creative businesses.

## What Changes

- Create an independently deployable Astro static landing site for `glovelly.net`.
- Establish a concise product story centred on keeping music-work administration predictable, visible, and under control without making it the main event.
- Use responsive, accessible art-directed interface compositions instead of product screenshots or stock imagery.
- Provide direct, unambiguous navigation to Glovelly Menu, the user guide, the technical handbook, the repository, and legal information.
- Define a shared public visual direction that the follow-on Starlight user guide can adopt without coupling its implementation to the landing site.
- Keep the public site static and privacy-conscious, with no analytics, tracking, account registration, or CMS.

## Capabilities

### New Capabilities
- `public-landing-site`: A production static product landing site that introduces Glovelly and directs visitors to the appropriate public surfaces.

### Modified Capabilities

None.

## Impact

- New Astro project, static-site assets, and Firebase Hosting deployment configuration/workflow.
- Firebase Hosting target `landing` (`glovelly-landing`) and the `glovelly.net` custom-domain cutover established by `reorganize-public-routing`.
- Public navigation and legal destinations, including the `docs.glovelly.net` user guide and `handbook.glovelly.net` handbook expected from the handbook-routing migration.
- Frontend-only visual and accessibility verification; no authenticated application API, data model, authentication, or registration-flow changes.
