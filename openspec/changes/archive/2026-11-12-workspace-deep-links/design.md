## Context

Menu stores its active section in React state and defaults to Gigs. `/clients`, `/gigs`, and `/invoices` are already API routes, proxied by Vite and bypassed by the service worker. The SPA has an existing `/access-requests/{id}` deep link that initiates an authorization-sensitive review after session loading. Sign-in sends the current location as a return URL, which the server validates against its configured origin.

## Goals / Non-Goals

**Goals:** Provide stable workspace entrypoints; keep the address accurate as the user changes workspaces without adding history entries; preserve sign-in and special review links; update the guide and verify real browser navigation.

**Non-Goals:** Individual record URLs, a general-purpose router or full workspace history, new public data endpoints, and changes to Menu authorization.

## Decisions

1. **Use a distinct path namespace.** Canonical paths are `/workspaces/clients`, `/workspaces/gigs`, and `/workspaces/invoices`. Exact matching, optionally accepting a trailing slash, avoids API collisions and accidental matches. Unknown SPA paths select the normal Gigs entry state without being treated as arbitrary destinations. API and server routes keep their existing behavior. Direct `/clients`-style paths were rejected because they are established API endpoints; query-only links were rejected in favor of readable stable paths.

2. **Initialize from the path, then synchronize explicit navigation.** Resolve the initial section before rendering or session checks. Route all section changes, including cross-workspace actions, through a shared navigation mechanism that replaces the current history entry for the three supported sections. Do not add entries for each switch or introduce a router. On explicit navigation to an unsupported section such as Admin, replace a stale workspace path with the root rather than falsely advertising the prior workspace; Admin deep links remain out of scope. Root and invalid entry paths retain normal entry behavior until explicit navigation. Preserve unrelated URL state only where doing so does not perpetuate special-route semantics.

3. **Treat access-request links as a separate route lifecycle.** Do not replace a review URL while authentication or review initialization is pending, or while its review surface remains open. Preserve the selected request through sign-in and existing admin/non-admin behavior. Once the review flow is dismissed or the user explicitly navigates away, transition to the appropriate normal workspace URL; ensure the request is not reopened merely because a render or reload observes a stale path. Verify the exact existing close/navigation behavior when implementing rather than clearing the path on initial mount.

4. **Reuse existing security and delivery boundaries.** Serve workspace document navigations through the SPA without changing API endpoints or authorization. Keep existing return-URL validation, and test unsafe external and scheme-relative destinations rather than broadening allowed redirects. Verify deployed SPA fallback, Vite proxy matching, and service-worker navigation handling for the new namespace. Workspace URLs never grant access or expose data independently of the normal session and API policies.

5. **Make handoffs task-led.** Update task-specific guide CTAs: Clients to Clients; Gigs and expense capture to Gigs; invoice review, status, and payment to Invoices; the first-invoice journey starts at Clients. General access and Menu links remain at the root where no single workspace is appropriate. Document canonical entrypoints and update relevant UAT journeys.

## Risks / Trade-offs

- [History replacement does not make Back traverse workspace switches] → Document and test the chosen behavior, while ensuring Back to a pre-existing entry remains usable.
- [Early URL synchronization could overwrite a review link or a post-login destination] → Initialize from the incoming URL and explicitly guard the review lifecycle; cover signed-out and signed-in review paths.
- [Local proxy, service worker, or deployed routing could return API data instead of the shell] → Use a separate namespace and exercise direct document requests under the relevant configurations.
- [A stale path may misrepresent Admin or revive a dismissed review] → Normalize only on explicit navigation or review dismissal, and test refresh and copied URLs.

## Migration Plan

Deploy Menu and guide changes together so guide links do not precede application support. Existing API and review URLs remain valid and require no data migration. On rollback, restore generic guide links before or with the application rollback; workspace deep links cannot be guaranteed to select their destinations on the older application.

## Open Questions

None; exact implementation of review dismissal should follow the existing lifecycle without changing its authorization semantics.
