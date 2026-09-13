## 1. Guide foundation and deployment

- [x] 1.1 Create the independent `frontend/glovelly-guide` Astro/Starlight project with local TypeScript, build, check, lint, and preview scripts.
- [x] 1.2 Configure Starlight site metadata, search, responsive sidebar/navigation, canonical public destinations, and accessible global layout for `docs.glovelly.net`.
- [x] 1.3 Add the Firebase Hosting `user-guide` target, static output configuration, cache headers, and an independently scoped guide preview/production workflow.
- [x] 1.4 Document the local guide development, build, preview, and Firebase deployment commands without coupling them to the React application or DocFX handbook.

## 2. Public guide experience and content

- [x] 2.1 Implement guide-local visual tokens and layout patterns aligned with the landing site's public visual vocabulary, including visible focus states and reduced-motion behavior.
- [x] 2.2 Author Start here pages for product orientation, invitation/access-request guidance, and the cross-workspace first-invoice journey.
- [x] 2.3 Author Core work pages for clients, gigs, expenses/receipts/mileage, invoice creation/review/delivery, and invoice statuses, payments, and paid-income visibility.
- [x] 2.4 Author Set up and Help pages for seller profile, settings/defaults, common questions, canonical public links, privacy, terms, handbook, and repository routes.
- [x] 2.5 Review all guide claims against current shipped behavior and retain specialist imports, set lists, MCP, administration, entitlement plans, and technical/operator material outside V1.
- [x] 2.6 Update contributor orientation with the distinct user-guide task voice and DocFX handbook reference voice after the guide path is established.

## 3. Documentation fixture lifecycle

- [x] 3.1 Define a fixed staging-only `Glovelly Docs` account and deterministic core-workflow seed scenario, separate from the existing UAT regression account.
- [ ] 3.2 Implement ownership-scoped reset/seed support that removes documentation account data, related relational records, and associated attachment/blob storage without affecting UAT or other users.
- [x] 3.3 Extend protected staging test-auth support to reset, seed, and authenticate only the predefined documentation fixture account.
- [ ] 3.4 Add backend integration coverage proving reset idempotency, fixture isolation, profile/default restoration, and storage cleanup.

## 4. Playwright screenshot capture suite

- [x] 4.1 Add a separately selectable documentation-capture suite and configurable candidate artifact directory while preserving ordinary UAT selection and failure diagnostics.
- [ ] 4.2 Configure deterministic capture contexts with fixed Chromium settings, desktop/mobile viewports, locale, timezone, colour scheme, reduced motion, frozen browser clock, and settled-font/data waiting.
- [ ] 4.3 Capture named, selected element screenshots for representative core workflow views, including an invoice email-review state before sending any email.
- [ ] 4.4 Reset the documentation fixture before capture and in guaranteed cleanup paths; add coverage showing repeat runs do not mutate the UAT fixture.
- [x] 4.5 Add the initial reviewed screenshot assets to the guide and use semantic guide markup/CSS for any explanatory callouts rather than raster annotations.

## 5. Informational screenshot freshness CI

- [ ] 5.1 Add a post-staging-UAT documentation capture job for same-repository pull requests with narrowly scoped staging-secret and PR-comment permissions.
- [ ] 5.2 Compare capture candidates with checked-in guide screenshots, create a human-readable changed/current report, and upload candidates plus comparison output as a named artifact.
- [ ] 5.3 Add or update one marker-based PR comment that reports screenshot freshness, affected assets, and the candidate artifact location without failing the pipeline for drift.
- [ ] 5.4 Ensure forked pull requests do not run the credentialed capture/comment path, and document how maintainers review and manually replace approved screenshot assets.

## 6. Verification and release

- [ ] 6.1 Run Starlight checks and production build, and verify guide navigation, search, mobile layout, keyboard navigation, and all canonical outbound links in a preview.
- [ ] 6.2 Run backend integration tests, ordinary UAT coverage, and the documentation capture suite against staging; verify fixture cleanup and candidate artifact output.
- [ ] 6.3 Run `openspec validate --change "create-starlight-user-guide" --strict` and update relevant UAT/engineering documentation for the new capture workflow.
