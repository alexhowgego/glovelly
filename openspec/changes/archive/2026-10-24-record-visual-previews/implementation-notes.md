# Implementation status

`tasks.md` is the implementation checklist. Automatic signed maps, approval removal and local first-page PDF thumbnails are implemented across the shared pane layout. Client/Gig source fingerprints are computed metadata, not approval state. The change has no persistent schema addition; the approval migration was local-only and removed without rewriting published history.

## PDF rendering contract

Existing PDF storage and generation remain unchanged. Each eligible mounted thumbnail downloads a bounded PDF, rechecks authorized metadata, and renders page one locally with PDF.js and its bundled worker. There is no reusable byte/thumbnail cache or sharing with the full-preview modal. Known metadata changes discard the result without retry loops; the small residual concurrent-replacement race is accepted. Selection/session/eligibility changes cancel work and release the document/canvas.

## Validation scope

Targeted signed-map tests cover exact encoded signatures, configuration failure, issuance limits, owner/shared visibility, saved source changes, direct image delivery, referrer policy, timeout/fallback, hidden-pane suppression and stale completions. No live Google image requests are needed for these tests. EF reports no pending model changes. Guide checks/build and browser-test compilation pass; browser UAT/captures were not run locally. Full verification results are recorded at handoff.

PDF-specific unit/component checks pass (22 tests). `./verify.sh` passes with 474 backend tests and 108 frontend tests plus lint/build; the final service-worker boundary tests bring the frontend total to 110, with frontend tests/lint/build rerun successfully. Guide checks/build and browser-test compilation pass (no browser compilation warnings/errors; guide retains its i18n/404 build warnings). Strict OpenSpec validation and whitespace checks pass.

Build output separates the lazy renderer (~430 kB, ~129 kB gzip) and matching local worker (~1.26 MB) from the main bundle (~607 kB, ~162 kB gzip). The existing main-chunk size warning remains. No external PDF renderer/CDN is used. Service-worker checks verify API/cross-origin bypass and targeted cleanup of any legacy cached Static Maps images.

Checklist progress is 29/33. Browser/UAT/capture execution remains an Ubuntu CI requirement, not a local result; no checked-in screenshots were substituted or replaced.

Staging signed delivery/restrictions, browser geometry, Ubuntu UAT, documentation captures, and production privacy/provider gates remain unverified. Maps defaults to disabled; local/deployment enablement overrides are independent and are not changed by this implementation. Application tests do not establish provider approval or production readiness.
