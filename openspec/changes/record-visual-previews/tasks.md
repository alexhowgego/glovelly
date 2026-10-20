## 1. Configuration and location-source contract

- [x] 1.1 Register disabled-by-default Maps Static settings and DI, dedicated Secret Manager bindings for staging/production, local API-project user-secrets, and operator documentation without committing values.
- [x] 1.2 Define deterministic saved-address/venue normalization and versioned source fingerprints with shared TypeScript/C# test vectors and no location substitution.
- [x] 1.3 Require a valid API key and signing secret for URL issuance; treat missing/invalid/disabled configuration as unavailable and never issue unsigned URLs. Update configuration documentation consistently.
- [x] 1.4 Update privacy/terms/provider disclosures and dates for automatic saved-location queries and direct Google browser delivery, including browser request/network information and applicable storage/consent requirements; remove per-record approval and server-only disclosure claims.

## 2. Automatic-preview model

- [x] 2.1 Remove approval checkboxes, form intent, API approval properties, validators, SaveChanges invalidation hooks, and approval-specific tests. Preserve ordinary client/gig editing and the source-identity contract.
- [x] 2.2 Check whether the approval migration has been applied anywhere. Remove the unapplied change-local migration/snapshot additions or create a forward cleanup migration if already applied; leave no approval persistence dependency and preserve business data/deployed history.
- [x] 2.3 Gate imagery only on deployment/signing availability, usable saved location, selection/session, and actual pane visibility/space. Existing/new/imported/copied records use the same automatic behavior, without address classification or map-specific writes.

## 3. Shared pane-responsive visual layout

- [x] 3.1 Add actual-pane resize/visibility eligibility with stable size buckets and threshold hysteresis; use the design's width/height and minimum facts/visual budgets.
- [x] 3.2 Complete the shared side rail across Invoices, Clients, and Gigs, preserving inline facts, scrolling, headers and docks; omit rather than squeeze/stack on narrow/short/editor-open panes. Leave Admin unchanged.
- [x] 3.3 Provide accessible loading/unavailable treatments, existing full-preview navigation and explicit Maps links without global error-toast spam; preserve session expiry and normal record actions.

## 4. Signed location-preview URLs

- [x] 4.1 Define allowlisted image size buckets and safely encoded independent Maps search links; link rendering does not contact Google.
- [x] 4.2 Make authenticated client/gig map-preview endpoints return private/no-store signed-URL descriptors, enforcing owner visibility and expected source identity before issuance. Accept no arbitrary query/destination/provider parameters; remove image-byte responses and duplicate routes.
- [x] 4.3 Implement exact-final-URL server signing for a fixed Google Static Maps endpoint with bounded query/URL lengths and allowlisted rendering parameters. Expose only the restricted API key/signature in the URL, never the signing secret or appended application/client/contact metadata.
- [x] 4.4 Rate-limit descriptor issuance per user, reject malformed requests and redact credential-bearing URLs. Remove the outbound image HTTP client, byte buffering, provider-response inspection, and image-fetch concurrency/timeout machinery. Document that signatures do not expire or prevent replay and issuance limits do not cap Google usage.
- [x] 4.5 Load descriptor URLs directly in Google-hosted img sources, with origin-only referrers compatible with website restrictions, full attribution, a bounded loading watchdog, quiet failure, and no retry loops. Guard session/record/source/size and late load/error completions; clear obsolete images without blob/data URLs or image persistence.
- [x] 4.6 Verify Vite proxy/service-worker bypass for descriptor endpoints and no application offline/shared/durable image or signed-URL cache. Update frontend types and fallback handling for the descriptor contract.

## 5. Existing-document PDF thumbnails

- [x] 5.1 Document the uncached browser-rendering approach with existing PDF storage/generation unchanged and the accepted residual concurrent-replacement race.
- [x] 5.2 Read existing authorized PDF bytes and recheck metadata after downloading; discard changed/unavailable document identities without automatic retries, identity-header changes or storage redesign.
- [x] 5.3 Add lazy PDF.js with a matching locally bundled worker, updating package/lock files without a CDN or external renderer.
- [x] 5.4 Implement bounded per-mounted-thumbnail downloads and lifecycle guards, with no reusable byte/thumbnail cache or modal byte sharing. Cancel/release bytes on selection, identity, eligibility or session changes; ordinary renders must not refetch.
- [x] 5.5 Render page one into a bounded-DPR canvas with aspect-ratio preservation, owned renderer input, obsolete-result guards and resource destruction; invalidate immediately on record/revision/readiness changes.
- [x] 5.6 Connect the invoice-labelled keyboard-operable thumbnail to the existing full preview, which retains its own authoritative PDF read and modal/blob/readiness/repair ownership.
- [x] 5.7 Handle missing/stale/regenerating/failed/oversized/unrenderable PDFs quietly, with no automatic generation or hidden-pane work and no loss of valid explicit actions.

## 6. Regression coverage

- [x] 6.1 Add PDF tests for read-only browsing, post-download metadata changes, readiness, selection/session/eligibility races, resource limits, no reusable caching/no refetch on ordinary renders, explicit-preview independence, and renderer/canvas cleanup.
- [x] 6.2 Add backend tests for owner/shared visibility, source mismatch, malformed requests, configuration/signing failure, known signature vectors and exact encoding, per-user issuance limits, no-store descriptors, minimized URLs, and absence of outbound image calls/secret logging. Validate any schema cleanup in PostgreSQL and in-memory tests.
- [x] 6.3 Add frontend tests for automatic loading without approval, source/selection/session/size races, direct Google img URLs, referrer policy, attribution, hidden-pane suppression, loading timeout/failure/retry bounds, and independent external links. Assert normal edits/imports do not acquire approval semantics.
- [x] 6.4 Update Ubuntu browser UAT for automatic maps, saved-location changes, signed direct delivery, pane/editor/mobile fallbacks, keyboard actions, PDF readiness, unchanged Admin, long details and docks. Mock descriptor/Google image responses for repeatable non-billable tests; assert no hidden requests or invoice generation side effects.

## 7. Documentation, validation and rollout

- [x] 7.1 Update user guide, docs/uat journeys, capture expectations and operator guidance for automatic/unverified maps, direct delivery, independent links and unavailable/hidden states; remove approval/proxy instructions and distinguish visible API keys/signatures from private signing secrets.
- [x] 7.2 Run ./verify.sh, affected guide checks/build and browser-test compilation; record actual results and lazy PDF worker/bundle behavior. Do not run Playwright or documentation captures locally.
- [ ] 7.3 Validate origin restrictions, direct Google delivery and signing on staging; verify signatures are required and unsigned-request quota is disabled after signed requests work. Confirm local origins only where intended, Google-side request caps and budget alerts, and unavailable/failure fallbacks.
- [ ] 7.4 Run Ubuntu browser UAT and review geometry/behavior; tune thresholds and resource limits from evidence without reducing readable facts or cancellation safety.
- [ ] 7.5 Generate staging light/dark documentation captures in Ubuntu CI and review before replacing checked-in screenshots; use actual product captures with attribution preserved.
- [ ] 7.6 Resolve the pending Google privacy inquiry and any applicable automatic-loading/browser-storage requirements before production enablement; confirm all provider/deployment gates and document that PDF previews work independently of Maps setup.

## 8. Preview sizing and navigation

- [x] 8.1 Define bounded, centred, caption-free visuals with hover/focus descriptions, clickable map images and linked saved addresses/locations instead of a separate Maps navigation line.
- [x] 8.2 Implement measured-height visual budgets and responsive contain sizing, remove fixed small invoice display caps and visible ready captions, and preserve full attribution, PDF proportions and existing activation workflows without resize-triggered PDF downloads.
- [x] 8.3 Update component tests, Ubuntu browser assertions, user guide and UAT journeys for linked addresses/images, hover/focus help, height bounds and no visual-induced scroll.
- [x] 8.4 Run frontend tests/lint/build, affected guide checks/build and browser-test compilation; keep actual browser/capture review in Ubuntu CI.

## 9. Content-fit map polish

- [x] 9.1 Measure the map frame independently of the shared overview, request bounded matching dimensions, and coalesce subsequent map-only resizes without delaying initial rendering or changing invoice loading.
- [x] 9.2 Add roomier facts/visual spacing and rounded map corners while retaining full attribution, content-sized facts and spanning Notes.
- [x] 9.3 Validate measurement lifecycle, canonical size bounds/signatures, frontend checks and browser-test compilation. Keep real-image attribution/visual review in Ubuntu CI and staging.
