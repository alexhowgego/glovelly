## Why

Glovelly now has a public front door and a separate technical handbook, but musicians do not yet have a task-led guide that helps them complete the core work loop with confidence. The guide needs accurate product screenshots without reverting to a manually maintained, silently stale image library.

## What Changes

- Create a branded, static Starlight user guide deployed independently to `docs.glovelly.net` through Firebase Hosting.
- Establish a guided, plain-language V1 focused on the shipped core work loop: access, clients, gigs, expenses/receipts/mileage, invoices, payments, seller profile, and settings.
- Distinguish the user guide's "do this next" voice from the concise technical/operator reference voice of the DocFX handbook.
- Add a deterministic, staging-only `Glovelly Docs` fixture account and Playwright capture suite for selected guide screenshots.
- Reset the documentation account's owned data before every capture and after capture completion so it remains isolated from shared UAT data and repeatable after interrupted jobs.
- Publish screenshot candidates and freshness information as non-blocking CI artifacts and an idempotent same-repository PR comment; do not auto-commit or auto-deploy generated images.
- Keep specialist imports, set lists, MCP, administrator operations, public self-service plans, payment processing, and technical/operator content outside the V1 guide.

## Capabilities

### New Capabilities
- `public-user-guide`: A branded Starlight guide that helps Glovelly users complete supported everyday workflows from `docs.glovelly.net`.
- `deterministic-documentation-captures`: An isolated, reproducible screenshot fixture and non-blocking CI reporting path for current user-guide images.

### Modified Capabilities
- `uat-browser-automation`: Browser automation gains an isolated documentation-capture suite without weakening the existing UAT suite's diagnostics or coverage contract.

## Impact

- Adds a Starlight project, guide content/assets, a Firebase Hosting `user-guide` target, and guide-specific deployment/preview workflow integration.
- Extends staging-only test-auth and seed/reset infrastructure, the Playwright UAT project, and post-staging CI permissions/reporting.
- Adds `@astrojs/starlight` and its transitive build dependencies to a new frontend workspace.
- Updates durable contributor guidance to preserve the boundary between the public guide and the DocFX handbook.
