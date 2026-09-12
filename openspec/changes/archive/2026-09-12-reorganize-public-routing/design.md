## Context

The current Cloud Run service hosts the authenticated React application at `glovelly.net`; that same host is inferred for Google OIDC, Google integration callbacks, invitations, access-request review links, and some email branding. The DocFX handbook is deployed to GitHub Pages at `docs.glovelly.net`. This makes the apex unsuitable for an independently deployable public landing site and leaves no home for the planned user guide.

Issue #260 establishes the routing and deployment foundation for #261's Astro landing site, #262's Starlight user guide, and #131's technical/operator handbook. No public landing or user-guide application exists in this repository yet.

## Goals / Non-Goals

**Goals:**

- Assign a single canonical purpose, owner, and deployment route to each public hostname.
- Make `menu.glovelly.net` the explicit canonical origin for the production authenticated application and all generated application links.
- Remove production dependence on an incoming request host for OAuth callbacks and outbound application links.
- Retain the DocFX handbook's repository-adjacent GitHub Pages deployment at `handbook.glovelly.net`.
- Establish a low-operations, independently deployable static platform for the future landing site and user guide.
- Support a staged migration with an observable rollback position.

**Non-Goals:**

- Building the Astro landing site, Starlight user guide, or new handbook content.
- Changing Glovelly's Google identity provider, authorization model, database, or session model.
- Sharing the authentication cookie with `glovelly.net` or other subdomains.
- Moving the technical handbook away from GitHub Pages.
- Introducing a CMS, public account-registration flow, or analytics platform.

## Decisions

### Assign the four public surfaces fixed responsibilities

| Hostname | Purpose | Delivery owner |
| --- | --- | --- |
| `glovelly.net` | Public product landing site | Firebase Hosting landing target |
| `menu.glovelly.net` | Authenticated Glovelly Menu application and API | Existing Cloud Run service |
| `docs.glovelly.net` | Public task-oriented user guide | Firebase Hosting user-guide target |
| `handbook.glovelly.net` | Technical and operator handbook | Existing DocFX GitHub Pages deployment |

The GitHub repository remains linked from all public surfaces. The landing site and user guide own their public navigation in follow-on changes; this change establishes their stable destinations.

### Use Firebase Hosting for the two new static surfaces

Firebase Hosting will be connected to the existing Google Cloud project and configured with independent Hosting targets for the landing site and user guide. GitHub Actions will build and deploy each target independently through the existing Workload Identity Federation model or a narrowly scoped equivalent deployment identity.

Firebase Hosting supplies CDN delivery, managed HTTPS, custom-domain support, atomic releases, and deploy previews without coupling these static sites to the application container. No Firebase client SDK, Firebase Authentication, or Firebase data product is required.

Alternatives considered:

- Cloud Storage plus an external Application Load Balancer: rejected because HTTPS custom domains require load-balancer, certificate, public-bucket/backend, and caching configuration that is disproportionate for two static sites.
- Static-file containers on Cloud Run: rejected because it creates separate containers and request-serving infrastructure without the static-hosting deployment and CDN benefits.
- GitHub Pages for all static sites: rejected because the existing Pages site has one custom-domain deployment and cannot cleanly own two independent hostname/content deployments alongside the handbook.

### Make the canonical application origin runtime configuration

Introduce an explicit non-secret `App:PublicBaseUrl` runtime setting for every deployed application environment. Production will be `https://menu.glovelly.net`; staging retains its explicitly configured staging application origin. The setting must be an absolute HTTPS URL outside development and normalized once during startup.

Use this value as the source of truth for:

- Google OIDC redirect URI construction for `/signin-oidc`.
- Google Drive, Sheets, and Calendar OAuth callbacks.
- Post-authentication return-URL validation and default redirect targets.
- Invitation and access-request review URLs.
- Application links in email content and MCP issuer/resource configuration.

Development keeps explicit localhost/CORS-origin support where required, but deployed environments must not construct public URLs from request scheme or host. This removes host-header/multiple-domain ambiguity and keeps provider-console configuration aligned with the application.

The existing host-only `glovelly.auth` cookie remains host-only. Existing apex sessions will not transfer to Menu; users sign in at the Menu origin after migration.

### Treat Google provider registrations as deployment configuration

Before directing production traffic to Menu, Google Cloud OAuth configuration must authorize:

- `https://menu.glovelly.net` as the JavaScript/application origin where required.
- `https://menu.glovelly.net/signin-oidc`.
- The three Menu integration callbacks under `/integrations/google-drive/callback`, `/integrations/google-sheets/callback`, and `/integrations/google-calendar/callback`.

The current apex callback registrations remain throughout the overlap and rollback window. The exact authorized origins and redirect URIs must be documented alongside their owning Google Cloud project and reviewed after the old origin is retired.

### Migrate through an application overlap before assigning the apex to static hosting

The deployment order is:

1. Provision and verify the Menu Cloud Run domain mapping and TLS certificate.
2. Configure the new canonical application origin and Google registrations, then deploy and smoke-test Menu.
3. Make all newly generated application links point to Menu.
4. Preserve the existing apex application mapping only for a defined migration window; redirect legacy application paths and query strings to their Menu equivalents without redirecting the new landing-page root.
5. Wait until in-flight OIDC and integration transactions can no longer complete against the apex, then reassign `glovelly.net` to Firebase Hosting.
6. Move the DocFX CNAME and sitemap to `handbook.glovelly.net`, verify GitHub Pages, then release the static-site domains for the follow-on changes.

Rollback during the overlap restores the previous application configuration and directs the apex back to the known-good Cloud Run application mapping. Google callback registrations remain additive until the rollback window closes. After the apex has moved to Firebase Hosting, rollback requires restoring the Cloud Run mapping and the former application configuration as a coordinated operation.

## Risks / Trade-offs

- [DNS, Cloud Run mapping, and Firebase custom-domain operations are external to the repository] -> Record owner, console project, DNS records, verification state, and rollback command/checklist in deployment documentation.
- [An apex OIDC callback can be in flight when the apex stops serving the app] -> Do not reassign the apex until the defined overlap period has elapsed; preserve old Google registrations until rollback is closed.
- [A bad canonical base URL can break sign-in, invitations, integrations, and MCP simultaneously] -> Validate the setting at startup and add focused tests for every public URL producer.
- [Users must authenticate again at Menu] -> Keep the cookie host-only and communicate the expected one-time sign-in rather than weakening subdomain cookie isolation.
- [Firebase Hosting introduces another deployment service] -> Limit it to public static assets, reuse the existing GCP project and CI identity model, and document its ownership distinctly from Cloud Run.
- [Public links and privacy notices can become stale while follow-on sites are unfinished] -> Update current application and handbook links as part of the host migration; #261 and #262 own public-site copy and navigation.

## Migration Plan

1. Record the current DNS records, Cloud Run mappings, GitHub Environment URL values, GitHub Pages custom-domain configuration, and Google OAuth registrations as the rollback baseline.
2. Create Firebase Hosting targets and verify their platform domains before attaching production custom domains.
3. Add Menu DNS and Cloud Run mapping, then add Menu OAuth origins and redirect URIs without removing apex entries.
4. Deploy the application configured with Menu as its public base URL and run sign-in, sign-out, invitation, access-request, integration, MCP, and generated-link smoke checks on Menu.
5. Redirect legacy apex application paths to Menu during the agreed overlap window; monitor failures and preserve the immediate Cloud Run rollback option.
6. Reassign the apex to Firebase Hosting only after the overlap checks pass and no in-flight callback window remains.
7. Move the handbook to `handbook.glovelly.net`; update DocFX metadata, GitHub Pages configuration, documentation, and references.
8. Retire apex OAuth registrations only after the documented rollback window and post-deployment verification complete.

## Open Questions

- Who owns the `glovelly.net` DNS zone, and is Cloud DNS in the existing project the intended record-management system?
- What staging hostname will be the explicit staging `App:PublicBaseUrl`, and does it need an analogous Menu name?
- Which Firebase project configuration and GitHub deployment identity/permissions are available for Hosting releases?
- What migration-window duration is acceptable for old OAuth/integration transactions and bookmarked application URLs?
- Which legacy apex paths must preserve path-and-query redirects, beyond the application root and access-request routes?
