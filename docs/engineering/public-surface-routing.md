# Public Surface Routing

Glovelly has four public surfaces. Their roles are deliberately separate so the public product journey does not share an authenticated application origin.

| Address | Purpose | Delivery platform | Owner |
| --- | --- | --- | --- |
| `glovelly.net` | Public product landing site | Firebase Hosting target `landing` (`glovelly-landing`) | Product website deployment |
| `menu.glovelly.net` | Authenticated Glovelly Menu application and API | Cloud Run service `glovelly` | Application deployment |
| `docs.glovelly.net` | Public task-oriented user guide | Firebase Hosting target `user-guide` (`glovelly-docs`) | User-guide deployment |
| `handbook.glovelly.net` | Technical and operator handbook | GitHub Pages / DocFX | Handbook workflow |

The Firebase sites are in the `glovelly-dev` Google Cloud project. They are delivery targets only: Glovelly does not use Firebase Authentication, Firestore, or Firebase client SDKs.

## Landing Site Appearance

The landing site follows the browser or device light/dark preference using CSS, defaulting to light when that preference is unavailable. It has no theme selector, saved theme preference, or theme-specific script. Its dark palette takes the user guide's palette as a reference, while its paper-like illustrations intentionally retain light surfaces. The guide's manual theme choice is independent and can therefore differ from the landing site's appearance.

When changing the landing palette, review both colour schemes on mobile and desktop, including keyboard focus, the production analytics-consent controls, reduced motion, and a preference change while the page is open. Check text and interactive contrast as well as the illustrations.

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

### Workspace entrypoints

Menu provides `/workspaces/clients`, `/workspaces/gigs`, and `/workspaces/invoices` on its configured origin. These are SPA document paths, not the `/clients`, `/gigs`, and `/invoices` API endpoints. Signed-out users sign in before workspace data is available and return to the requested workspace. Workspace switches replace the current history entry; they do not create a Back entry for every switch. Access-request review links retain their separate sign-in and review behavior.

Deploy Menu support before or together with the guide's workspace links. If rolling Menu back to a version without these paths, first restore generic Menu links in the guide or roll both back together. Confirm deployment and guide handoffs in the target environment; a repository change alone does not establish deployment ordering.

## Steady-State Ownership

- Firebase Hosting owns the apex landing site and its TLS certificate.
- Cloud Run owns only the Menu application origin and API.
- GitHub Pages owns the handbook hostname and its DocFX output.
- The user-guide Firebase target owns `docs.glovelly.net`.
- Application URLs, OAuth callback URLs, and MCP metadata are constructed from `App:PublicBaseUrl`; deployed environments never infer a public origin from an incoming request host.

## Post-Deployment Checks

- Load all four public hostnames and confirm each serves its documented purpose.
- Confirm `menu.glovelly.net` can sign in and sign out, and that a protected request has an authenticated session.
- Open each `/workspaces/clients`, `/workspaces/gigs`, and `/workspaces/invoices` URL directly and after sign-in; check refresh and guide handoffs select the intended workspace.
- Send an invitation and access request; verify their generated links start with the intended application origin.
- Start and complete Drive, Sheets, and Calendar authorization; confirm each provider callback uses the configured application origin.
- Request `/.well-known/oauth-protected-resource` and `/.well-known/oauth-authorization-server`; confirm Menu issuer and resource URLs.
- Confirm GitHub Pages serves the handbook at its new hostname and Firebase Hosting serves the intended landing and user-guide artifacts.
