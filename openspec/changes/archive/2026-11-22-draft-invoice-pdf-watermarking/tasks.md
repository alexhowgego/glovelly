## 1. Stored document provenance and legacy migration

- [x] 1.1 Add nullable generated watermark provenance to the invoice model/EF mapping and propagate metadata/types only where needed for consistent document readiness.
- [x] 1.2 Add an additive EF migration and snapshot update that invalidate legacy Draft PDFs with a recoverable explanation and advanced document revision while preserving business data, audit fields, and non-draft readiness.
- [ ] 1.3 Add migration coverage for legacy Draft and non-draft invoices, including document metadata and lifecycle-history preservation; validate through the PostgreSQL migration workflow.

## 2. PDF rendering and lifecycle integration

- [x] 2.1 Extend the internal PDF builder/canvas to draw a prominent diagonal DRAFT watermark after normal content on every draft page, isolating drawing state and retaining readable/selectable content.
- [x] 2.2 Add renderer tests for single-page and overflowing multi-page drafts and every supported non-draft status; verify watermark absence/presence per page without losing existing layout and monetary assertions.
- [x] 2.3 Correct intended-state rendering for issue, redraft, and reissue, and audit creation, combined-gig invoicing, adjustments, source refresh, and explicit regeneration call sites.
- [x] 2.4 Record truthful provenance only when matching rendered bytes are successfully stored; update development, documentation, and test seed generation accordingly.
- [x] 2.5 Add workflow tests for create → issue → reissue → issue, redraft, adjustments, and source refresh, preserving first-issue/reissue/delivery audit semantics.

## 3. Readiness, delivery, and recovery

- [x] 3.1 Centralize lifecycle/watermark compatibility with existing state/revision readiness checks; ensure metadata and all supported PDF read, email preparation/delivery, and Drive publishing paths enforce it.
- [x] 3.2 Ensure invalidated legacy drafts have an actionable inline explicit regeneration/retry path using existing workspace patterns; preserve read-only thumbnails/full previews and independent uncached PDF reads.
- [x] 3.3 Add integration tests for unknown/incorrect watermark provenance, legacy non-draft compatibility, authorization/ownership, and blocked download/email/Drive boundaries without successful delivery history.
- [x] 3.4 Add failure/retry tests for issue, reissue, and regeneration so mismatched output cannot be advertised as current and legacy bytes remain unavailable; preserve saved invoice data.
- [x] 3.5 Add frontend tests where recovery/readiness metadata changes, including no passive regeneration, stale-thumbnail clearing, and existing inline/notification policy.
- [x] 3.6 Add a compact visible draft-only hint explaining that issuance removes the DRAFT watermark, accessible without hover on desktop/mobile; test disappearance after issue and reappearance after reissue, and include this guidance in invoice UAT.

## 4. Documentation and verification

- [x] 4.1 Update `docs/uat/invoices.md` and matching Ubuntu CI browser fixtures/scenarios for watermarked draft preview/download, clean issue, reissue, legacy recovery, and failed retry without lifecycle side effects.
- [x] 4.2 Update relevant public-guide and engineering documentation with draft/issued PDF behavior, explicit legacy recovery, deployment/rollback constraints, and the non-DRM/external-copy limitation; do not create substitute screenshots.
- [x] 4.3 Run `./verify.sh`, affected frontend tests, and applicable guide checks/builds; run Playwright/documentation capture only in Ubuntu CI and record any remaining validation gaps.
- [ ] 4.4 Review actual single-page and multi-page PDF output in staging for watermark visibility, readable content, continuation-page coverage, clean issued output, and every allowed document surface before declaring the prerequisite complete.

## 5. Restoring drafts and single issuance confirmation

- [x] 5.1 Restore/regenerate watermark-compatible PDFs during Cancelled → Draft with safe failure/retry and audit-preservation tests.
- [x] 5.2 Add one accessible issuance confirmation with optional linked-gig completion, remove the post-issue prompt, and test confirm/cancel behavior.
- [x] 5.3 Issue before combined email/Drive delivery, retain Issued state after delivery failure, and test ordering, blocked delivery on issue failure, and retries without reissuance.
- [x] 5.4 Update UAT, user/operator guidance, and run local verification; leave browser/staging validation explicitly pending.

## 6. Coherent delivery and stable preview

- [x] 6.1 Enforce delivery eligibility on the backend (Issued/Paid/Overdue only), retaining read-only Draft review and PDF access, with endpoint/service regression tests.
- [x] 6.2 Remove optional Draft delivery and browser publication prompts; use one shared issue-before-delivery coordinator and consistent styled action confirmations, with failure/retry tests.
- [x] 6.3 Keep the selected invoice preview/frame mounted through mutation, blur and disable previous pixels during refresh, publish completed replacement canvases, and test cleanup/selection/failure/no-byte-cache behavior.
- [x] 6.4 Update specs, documentation, and Ubuntu UAT for the consolidated journeys and stable layout; run local verification and record staging/CI gaps.

## Implementation checkpoint

- Consolidation `./verify.sh` passed locally: 612 backend tests passed, 5 PostgreSQL tests skipped, all 189 frontend tests and lint/build passed. Guide checks/build, DocFX, and UAT Release compilation passed. The existing NU1903 advisory for Microsoft.AspNetCore.WebUtilities 10.0.12 remains.
- Guide checks/build passed; DocFX built with zero warnings/errors; UAT Release build passed with zero warnings/errors. OpenSpec strict validation and EF pending-model checks passed, and the migration SQL was generated/reviewed.
- PostgreSQL migration execution awaits CI: local Docker is unavailable and no local `psql` executable exists. Browser UAT and staging visual review have not run.
- Resolved cancellation behavior: Draft always requires a watermark; Cancelled requires it when `FirstIssuedUtc` is absent. Previously issued Cancelled invoices retain clean output eligibility. Include never-issued Cancelled invoices in legacy invalidation and regression coverage.

## Archive decision

Archived on 2026-11-22 with the user's explicit approval to retain tasks 1.3 and 4.4 as pending PR/CI/staging validation. Archiving does not certify those checks as passed. Record their results and update the archived checklist retrospectively after validation.
