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
- `glovelly.net` is served by the Firebase Hosting landing target; it does not route through Cloud Run.
- The DocFX handbook is published at `handbook.glovelly.net`; `docs.glovelly.net` is reserved for the user guide.
- Google OAuth uses the Menu sign-in and integration callback registrations. The former apex registrations have been retired.

## Application Origin

`App:PublicBaseUrl` is the application source of truth for public URLs. Deployed environments require an absolute HTTPS origin. The production deployment receives `https://menu.glovelly.net`; staging receives `https://staging.glovelly.net`.

The application derives its Google sign-in redirect, Drive, Sheets, and Calendar callbacks, invitation links, access-review links, and fallback MCP metadata from this origin. It does not derive deployed public URLs from request headers. The authentication cookie remains host-only, so users moving from the apex application to Menu sign in again rather than sharing a cookie with the public landing site.

## Steady-State Ownership

- Firebase Hosting owns the apex landing site and its TLS certificate.
- Cloud Run owns only the Menu application origin and API.
- GitHub Pages owns the handbook hostname and its DocFX output.
- The user-guide Firebase target owns `docs.glovelly.net` when the guide is released.
- Application URLs, OAuth callback URLs, and MCP metadata are constructed from `App:PublicBaseUrl`; deployed environments never infer a public origin from an incoming request host.

## Post-Deployment Checks

- Load all four public hostnames and confirm each serves its documented purpose.
- Confirm `menu.glovelly.net` can sign in and sign out, and that a protected request has an authenticated session.
- Send an invitation and access request; verify their generated links start with the intended application origin.
- Start and complete Drive, Sheets, and Calendar authorization; confirm each provider callback uses the configured application origin.
- Request `/.well-known/oauth-protected-resource` and `/.well-known/oauth-authorization-server`; confirm Menu issuer and resource URLs.
- Confirm GitHub Pages serves the handbook at its new hostname and Firebase Hosting serves the intended landing and user-guide artifacts.
