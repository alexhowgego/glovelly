## Why

Glovelly currently uses `glovelly.net` as the authenticated application, OAuth callback origin, generated-link host, and public product identity, while its technical handbook occupies `docs.glovelly.net`. This prevents the project from establishing a clear public journey and makes moving the apex to a landing site unsafe without a deliberate application-host migration.

## What Changes

- Establish canonical, distinct purposes for the public product landing site, authenticated Glovelly Menu application, user guide, and technical/operator handbook.
- Move the application's canonical production origin from `glovelly.net` to `menu.glovelly.net`, including explicit configuration for generated application URLs and OAuth-related origins.
- Retain the DocFX technical/operator handbook on GitHub Pages and publish it at `handbook.glovelly.net`.
- Establish Firebase Hosting, connected to the existing Google Cloud project, as the static delivery platform for the future Astro landing site at `glovelly.net` and Starlight user guide at `docs.glovelly.net`.
- Define a phased migration, rollback position, smoke checks, and public-surface navigation so moving the apex does not strand existing users or external integrations.

## Capabilities

### New Capabilities
- `public-surface-routing`: Defines the canonical public hostnames, their deployment ownership, application-origin configuration, and safe migration behaviour.

### Modified Capabilities

None.

## Impact

- Cloud Run custom-domain mappings, GitHub Environment deployment URL values, Google OAuth authorized origins/redirect URIs, DNS, and Firebase Hosting configuration.
- Backend application configuration and host-derived URL generation for authentication, integrations, invitations, access-review links, email branding, and MCP metadata.
- Frontend login, legal/help links, and UAT configuration/documentation.
- DocFX CNAME, sitemap, handbook deployment documentation, and public-site privacy statements.
- Follow-on work in #261 (Astro landing site), #262 (Starlight user guide), and #131 (technical/operator handbook content).
