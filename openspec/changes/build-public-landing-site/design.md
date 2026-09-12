## Context

The authenticated React application and API have been assigned to `menu.glovelly.net` by the active `reorganize-public-routing` change. That change also establishes Firebase Hosting target `landing` (`glovelly-landing`) for an independently deployable apex site, but no landing-site project or deployment workflow exists yet.

Issue #261 calls for a human, visual front door rather than a product manual or feature catalogue. Product discussion has established the audience as working musicians and small creative businesses; the primary promise as "Music work, less admin"; and art-directed interface compositions rather than literal screenshots. A separate handbook-domain pull request is expected to establish `handbook.glovelly.net` and release `docs.glovelly.net` for the future user guide before this change merges.

## Goals / Non-Goals

**Goals:**

- Deliver a fast, accessible, static Astro landing page that can become the apex site's production content.
- Distinguish the public product introduction from the authenticated Menu, user guide, and technical/operator handbook.
- Establish a reusable public visual vocabulary through documented tokens and patterns, without implementation coupling to the future Starlight guide.
- Use visual compositions and concise copy to make real product outcomes tangible while avoiding claims about unshipped or advisory services.
- Deploy the site independently through Firebase Hosting with preview and production paths appropriate to the existing GitHub/GCP deployment model.

**Non-Goals:**

- Changing the React application, API, authentication, sessions, or account-access model.
- Account registration, payments, lead capture, a CMS, a blog, a support system, analytics, advertising, or embedded media.
- Replacing the planned Starlight user guide or the DocFX technical/operator handbook.
- Sharing a component library, runtime, or deployment artifact with either existing application.
- Attaching `glovelly.net` before the prerequisite Menu migration, overlap window, and routing cutover have completed.

## Decisions

### Create a standalone Astro project under the frontend workspace

Place the new site beside the existing Vite application under `frontend/` as its own Astro project, with a single static page, locally owned styles, static assets, and build scripts. The site will not import the React application's packages, CSS, components, or API client.

Astro is selected because the site is content-led and has no authenticated or dynamic behaviour. It generates static HTML with minimal client JavaScript, isolates public-site releases from the application container, and creates a natural foundation for the separate Starlight guide.

Alternatives considered:

- Add a public route to the existing React SPA: rejected because it couples the apex release to authenticated application deployment and unnecessarily loads application code.
- Serve a static directory through the existing ASP.NET application: rejected because the apex must be independently deployable on Firebase Hosting.
- Use a generic HTML-only site: rejected because Astro provides structured content/layouts and an aligned future guide ecosystem without requiring a client runtime.

### Use an outcome-led page rather than a feature inventory

The page will have a small, intentional content architecture:

```text
Header: wordmark | How it works | User guide | Open Glovelly Menu
Hero: primary promise, audience, primary CTA, hero composition
Supporting story: "A gig is rarely just a gig"
Three outcomes: plan the work | keep the record | see where things stand
Closing invitation: return to Menu
Footer: user guide | handbook | GitHub | privacy | terms
```

The three outcomes give each composition a job: a paid-work plan, an expense/receipt record, and an invoice/payment or year-view. Copy remains concrete and allows one light, knowing observation per section; action labels, product boundaries, and legal content remain direct.

### Art-direct product moments from semantic HTML and CSS

Product moments will be created with semantic HTML, CSS, locally stored graphics where needed, and decorative layers that are hidden from assistive technology. They will depict credible interface states without being represented as literal screenshots or guarantees of the current product UI.

This preserves the product's warmth and avoids a screenshot-maintenance burden as the application changes. It also allows compositions to scale, reflow, and reduce motion more reliably than image-heavy mockups.

Alternatives considered:

- Sanitised production screenshots: rejected for this first release because they freeze a moving application interface into marketing material and are harder to compose responsively.
- Stock musician imagery: rejected because it would not demonstrate the product and would dilute its distinct lived-in voice.
- Canvas or video compositions: rejected because they increase accessibility, performance, and privacy complexity without improving the product narrative.

### Define visual alignment as tokens and patterns, not shared source code

The landing site will define a small public visual system: Fraunces-style editorial display typography, Instrument Sans-style interface typography, warm paper-like neutrals, deep blue structural colour, restrained orange actions, rounded surfaces, and visible focus treatment. Layout and component patterns will be documented in the site for the future guide to reproduce independently.

The landing site will not import the authenticated application's styles because those styles include application-specific density, themes, and interactions. It will not create a shared package before two public sites demonstrate a genuine shared-maintenance need.

### Keep the site static and cookie-free by default

No client SDKs, authentication calls, analytics, embeds, forms, or non-essential browser storage will be added. Privacy and terms links will point to the canonical public documents produced by the handbook-domain migration. This avoids consent-management complexity and honours the present privacy position.

If analytics or embedded media are later proposed, they require an explicit privacy review and a separate product decision before introduction.

### Deploy through a dedicated Firebase Hosting workflow

Add a landing-specific GitHub Actions workflow or clearly isolated job that installs the landing dependencies, runs the Astro production build, and deploys only the generated output to the Firebase `landing` target. It will use the existing Workload Identity Federation model or equally least-privileged deployment identity, and support a non-production preview path before production deployment.

The domain attachment remains an external cutover step governed by `reorganize-public-routing`. The build can deploy to Firebase's platform domain without making the apex live.

## Risks / Trade-offs

- [The product UI changes after compositions are published] -> Use representational compositions and outcome-led labels rather than pixel-accurate screenshots or exhaustive feature claims.
- [Humour weakens clarity or trust] -> Keep claims, calls to action, navigation, and legal boundaries literal; limit each section to one quiet humorous observation.
- [A visually rich page harms performance or accessibility] -> Prefer CSS/HTML compositions, local optimised assets, semantic headings, keyboard testing, and reduced-motion support.
- [The apex is pointed at an empty Firebase target] -> Keep custom-domain cutover blocked on the prerequisite routing checks and first deploy a verified landing artifact.
- [Public links are stale while handbook migration is open] -> Make the handbook PR a merge dependency and verify all final destinations during landing release checks.
- [Landing and guide visually drift] -> Record the core public tokens and patterns, then let #262 implement them independently rather than prematurely sharing code.

## Migration Plan

1. Complete the handbook-domain migration so the final handbook, legal, and guide destinations are available.
2. Create and verify the Astro project's local build, accessibility baseline, and static output.
3. Configure the Firebase Hosting landing deployment and publish a preview/platform-domain release.
4. Review content, outbound destinations, responsive behaviour, keyboard navigation, reduced motion, and a no-tracking browser session.
5. After the `reorganize-public-routing` Menu verification and overlap window complete, attach `glovelly.net` to the verified Firebase landing target.
6. Smoke-test the apex, Menu CTA, guide, handbook, GitHub, privacy, and terms links after DNS and certificate propagation.
7. To roll back after apex cutover, follow the public-surface-routing runbook to restore the prior Cloud Run mapping and application-origin configuration; do not change application source to compensate for a landing-site issue.

## Open Questions

- What exact GitHub Actions preview behaviour and Firebase permissions are available for the landing target?
- Which GitHub repository URL and branch should be presented as the canonical public code link?
- Should privacy and terms remain under the technical handbook hostname for the first landing release, or move to public-site-owned paths in a future change?
