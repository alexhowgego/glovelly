## Context

Glovelly's public estate comprises an Astro landing site on Firebase Hosting, a Starlight user guide on Firebase Hosting, and a DocFX handbook on GitHub Pages. These projects are independently built and deployed. The authenticated Vite/React Menu application shares neither their delivery path nor their privacy boundary.

The landing-site specification currently prohibits analytics. The privacy policy says it must be updated before analytics or other tracking is enabled. The GA4 measurement ID is `G-ZTRBP9KE40`.

## Goals / Non-Goals

**Goals:**
- Measure aggregate usage of the three production public sites only after affirmative analytics consent.
- Give visitors a consistent, accessible way to accept, reject, and revise that choice across public subdomains.
- Maintain one canonical analytics implementation and configuration source despite the separate static-site build systems.
- Prevent analytics from loading or emitting hits before consent, on non-production hosts, or in Menu.

**Non-Goals:**
- Analytics, consent controls, or event tracking in `menu.glovelly.net`.
- Recording business records, account data, form values, visitor identifiers, advertising audiences, or product-level interaction events.
- A tag manager, a third-party consent-management platform, server-side analytics collection, or cross-domain conversion attribution.
- Changing public routing, authentication, API behavior, or database schema.

## Decisions

### Use GA4 directly, after consent

The sites will load the standard GA4 tag only after the visitor accepts analytics. A rejected or undecided visitor will not request `googletagmanager.com`, initialise `dataLayer`, or send GA hits. GA will use the existing measurement ID and default automatic page views; no custom events are included in this change.

Direct GA4 matches the configured Analytics data stream and keeps the integration small. Google Tag Manager was rejected because it adds a second remote administration and loading layer without a current need for multiple tags.

### Treat production hostnames as an explicit allow-list

The integration will run only at `glovelly.net`, `docs.glovelly.net`, and `handbook.glovelly.net`, over HTTPS. It will not run on localhost, Firebase preview URLs, staging, pull-request previews, or Menu. This client-side boundary is required because the static sites can be built once and deployed to preview and production hosts.

The production allow-list will be held with the shared implementation alongside the public measurement ID. The measurement ID is not a secret and does not warrant deployment-secret plumbing.

### Use one site-owned, framework-neutral consent and GA loader

Implement one small vanilla-JavaScript module as the canonical source for consent UI, preference persistence, production-host gating, GA loading, and consent withdrawal. Each public-site build will package the same module as a first-party static asset and include it from its native page-shell mechanism: the landing page head/body, Starlight head configuration, and the DocFX custom template.

This avoids maintaining three divergent consent implementations while preserving independent deployments. A remotely hosted shared script was rejected because it introduces a cross-site availability and release dependency. A framework package was rejected because DocFX cannot consume the Astro/Vite dependency model.

### Persist a public-subdomain consent preference and make it revisable

The module will store the visitor's choice in a first-party secure, same-site cookie scoped to `.glovelly.net`, so a choice applies consistently to the three public subdomains. It will render an accessible initial choice prompt when no preference exists and expose a persistent "Cookie settings" control on each surface.

Choosing reject, or withdrawing previously granted consent, will prevent future GA loading and remove GA cookies reachable from the current public host and `.glovelly.net`. The consent preference itself is strictly necessary to remember the request.

### Keep collected content deliberately minimal

The implementation will use GA4's ordinary page views only. It must not call GA APIs with names, email addresses, URLs containing application data, search phrases, document titles that contain user-entered content, or custom dimensions/events. Google Signals and advertising-personalisation features will remain disabled in GA administration.

The privacy policy will identify Google Analytics, describe the analytics and consent cookies, state the purpose and visitor controls, and link to the relevant Google privacy information. Its static-site wording will be corrected to recognise that all three public surfaces can use the consent-gated integration.

## Risks / Trade-offs

- [A visitor sees the consent prompt on an older cached public page] -> Use the existing HTML no-cache/revalidation rules and ensure the module handles missing or malformed preference values safely.
- [Cookie domain handling differs across public hosting providers] -> Verify consent propagation and withdrawal on all three production hostnames in browser checks before release.
- [Future code changes add tracking before consent] -> Keep all GA loader code in the canonical module and add static/source checks that public shells do not include a direct GA tag.
- [GA configuration is changed in the vendor console to collect broader data] -> Document the required GA administration settings and verify them as part of release review.
- [Consent UI harms accessibility or blocks documentation] -> Use semantic controls, keyboard focus handling, screen-reader labels, responsive layout, and no blocking overlay after a choice is made.
- [Google service failure affects page performance] -> Load GA asynchronously only after consent; no public content or navigation depends on the request.

## Migration Plan

1. Configure and verify GA4 privacy settings without enabling collection from public sites.
2. Deploy the consent module, page-shell integrations, policy update, and automated build checks together.
3. Verify undecided/reject/accept/withdraw flows on every production public hostname, including network requests and cookie state.
4. Confirm GA4 receives only production public-host page views, then monitor reporting after release.
5. Roll back by removing the module from each public shell or disabling its production allow-list; this stops new analytics requests while retaining the visitor's rejection/withdrawal choice.

## Open Questions

- Confirm the intended consent-cookie lifetime and the precise GA4 data-retention period before implementation.
- Confirm the legal review of the final privacy-policy wording and the Google privacy-information destination.
