# Public Surface Routing

Glovelly has four public surfaces. Their roles are deliberately separate so the public product journey does not share an authenticated application origin.

| Address | Purpose | Delivery platform | Owner |
| --- | --- | --- | --- |
| `glovelly.net` | Public product landing site | Firebase Hosting target `landing` (`glovelly-landing`) | Product website deployment |
| `menu.glovelly.net` | Authenticated Glovelly Menu application and API | Cloud Run service `glovelly` | Application deployment |
| `docs.glovelly.net` | Public task-oriented user guide | Firebase Hosting target `user-guide` (`glovelly-docs`) | User-guide deployment |
| `handbook.glovelly.net` | Technical and operator handbook | GitHub Pages / DocFX | Handbook workflow |

The Firebase sites are in the `glovelly-dev` Google Cloud project. They are delivery targets only: Glovelly does not use Firebase Authentication, Firestore, or Firebase client SDKs. Until the landing-site and user-guide projects are implemented, their default `web.app` addresses can return `404`; do not attach their production domains to placeholder content.

## Current Baseline

- DNS is owned and managed in Squarespace.
- Cloud Run runs in `glovelly-dev`, region `europe-west1`. The production service is `glovelly`; staging is `glovelly-staging`.
- `menu.glovelly.net` is mapped to the production application and its Google OAuth redirects have been registered and verified.
- Production `DEPLOYMENT_URL` is `https://menu.glovelly.net`. Staging remains `https://staging.glovelly.net`.
- Production `App:LegacyApplicationHost` is `glovelly.net` while Cloud Run still serves the apex. It redirects every apex application request to Menu before authentication begins.
- The existing GitHub Pages handbook remains at `docs.glovelly.net` until the `handbook.glovelly.net` DNS and GitHub Pages custom-domain move is ready.
- Google OAuth retains apex callback registrations through the migration and rollback window. Do not remove them while `glovelly.net` can still receive an in-flight callback.

## Application Origin

`App:PublicBaseUrl` is the application source of truth for public URLs. Deployed environments require an absolute HTTPS origin. The production deployment receives `https://menu.glovelly.net`; staging receives `https://staging.glovelly.net`.

The application derives its Google sign-in redirect, Drive, Sheets, and Calendar callbacks, invitation links, access-review links, and fallback MCP metadata from this origin. It does not derive deployed public URLs from request headers. The authentication cookie remains host-only, so users moving from the apex application to Menu sign in again rather than sharing a cookie with the public landing site.

## Migration And Rollback

1. Record current Squarespace DNS, Cloud Run domain mappings, GitHub Environment variables, Google OAuth registrations, and GitHub Pages custom-domain settings before each cutover.
2. Confirm Menu sign-in, sign-out, invitations, access-review links, all Google integration callbacks, and MCP metadata with the configured Menu origin.
3. Keep the old apex application mapping and Google callback registrations for the agreed callback/session overlap window. The legacy-host middleware preserves paths and query strings while redirecting new apex requests to Menu before an OIDC challenge can set an apex-only correlation cookie.
4. Move DocFX to `handbook.glovelly.net`, confirm its GitHub Pages certificate and sitemap, then release `docs.glovelly.net` to the Starlight guide.
5. Attach `glovelly.net` to the Firebase landing target only after the Menu checks and overlap window are complete.
6. To roll back during the overlap, restore the apex Cloud Run mapping and the prior `App:PublicBaseUrl`/deployment URL. Keep both old and Menu OAuth registrations until rollback is no longer required.

## Post-Deployment Checks

- Load all four public hostnames and confirm each serves its documented purpose.
- Confirm `menu.glovelly.net` can sign in and sign out, and that a protected request has an authenticated session.
- Send an invitation and access request; verify their generated links start with the intended application origin.
- Start and complete Drive, Sheets, and Calendar authorization; confirm each provider callback uses the configured application origin.
- Request `/.well-known/oauth-protected-resource` and `/.well-known/oauth-authorization-server`; confirm Menu issuer and resource URLs.
- Confirm GitHub Pages serves the handbook at its new hostname and Firebase Hosting serves the intended landing and user-guide artifacts.
- Keep the previous Cloud Run mapping, DNS records, and Google OAuth registrations available until all checks pass and the rollback window closes.
