## 1. Persistence and rollout

Implementation evidence (2026-11-22): persistence tests pass (4/4); EF reports no pending model changes. Migration `20261122173306_AddPlanEntitlements` includes the one-time Founder assignment/audit and historical issuance backfill. Docker is now available: real PostgreSQL 17 validation covers empty/populated upgrades, replay, retention, per-user cross-context locking, and nested transaction ownership. Invoice allowance enforcement, reporting restrictions, licence administration, and frontend integration are implemented.

Issuance evidence: 18 allowance/issuance tests passed without skips, including PostgreSQL last-unit/shared-invoice races, a licence-grant race, stale line edits, render/storage/save/commit failures, and lost acknowledgement after actual commit. The writer inventory, publication policy, cleanup and retention follow-up are in `docs/engineering/plan-entitlements.md`.

Latest verification: the complete backend solution passed 668/668 tests with zero skips against isolated PostgreSQL, and `./verify.sh` passed (including 200 frontend tests, lint, and build). Final frontend review added generation-key/legacy-issuance and assigned-versus-effective-plan regressions; the frontend subsequently passed 203/203 tests, lint, and build. UAT Release compilation, guide check/build, handbook build, strict OpenSpec validation, and EF pending-model checks passed. The existing NU1903 advisory for Microsoft.AspNetCore.WebUtilities 10.0.12 remains.

UI consistency follow-up (2026-11-23): Licences now reuses Admin's compact directory rows, overview/body/action dock, and shared `RecordEditorFrame` / `RecordEditorActions` components. Selection reviews before editing; Save retains the editor with inline feedback and refreshed version, Save and close retains the refreshed selected overview with notification feedback, and dirty edits are protected. The separate table layout/styles were removed. All 211 frontend tests, lint/build, strict OpenSpec validation, updated UAT Release compilation, guide check/build, and handbook build passed. Desktop/mobile browser assertions were updated for Ubuntu CI; no local browser run or visual staging result is claimed.

Dashboard/height polish (2026-11-23): added global effective Free, Solo/Pro, and 30-day expiring-grant cards backed by a ManageLicences-only database aggregate, independent of directory search/page. All five sections now use `RecordWorkspace` and a shell layout contract instead of duplicated pane geometry and a viewport section allowlist. The four summary tests passed against PostgreSQL; full `./verify.sh` passed with 672 backend tests (zero skips), 220 frontend tests, lint, and build. UAT compilation, handbook build, and strict validation also passed. The first full verification attempt had three existing administration-test failures because the shared PostgreSQL database was not migrated; the successful rerun used a correctly migrated isolated database. Browser results remain for Ubuntu CI. The existing NU1903 advisory and Vite chunk-size warning remain.

New-account preview follow-up (2026-11-23): invoice thumbnails now use a 180px overview-body minimum height rather than the map-specific 300px minimum, preserving previews when the seller-profile setup notice reduces the body's available space. Width/visibility gating, map thresholds, and PDF access/publication behavior are unchanged. Added threshold/mounting regressions and an Ubuntu browser case for an unconfigured seller profile. All 222 frontend tests, lint/build, and UAT Release compilation passed; the new browser case has not been run locally.

- [x] 1.1 Add plan catalogue, per-user subscription/version, licence audit, usage ledger, and trusted legacy-issuance models/configurations with appropriate indexes and non-cascading invoice usage identity.
- [x] 1.2 Seed Free (two/month, no reporting), Solo and Pro (unlimited, reporting); implement matching explicit development/test fixtures.
- [x] 1.3 Create the one-time Founder migration/audit for bound Google identities with LastLoginUtc, preserving inactive-account restrictions and excluding later enrolment.
- [x] 1.4 Backfill attributable historical first-issue usage at actual timestamps and trusted legacy exemptions without fabricating dates or charging ambiguous/shared history repeatedly.
- [x] 1.5 Test populated and empty PostgreSQL migrations, replay/startup safety, invoice/account deletion retention, and Founder eligibility boundaries.

## 2. Entitlement resolution and serialization

- [x] 2.1 Implement local effective-plan resolution with immediate manual grants, optional exclusive expiry, cancellation/Free fallback, and TimeProvider-based decisions.
- [x] 2.2 Implement Europe/London calendar-month usage queries and self-service snapshots with continuous usage, unlimited limits, clamped remaining units, and reset instants.
- [x] 2.3 Add a stable per-user PostgreSQL transaction advisory-lock namespace and in-memory fixture equivalent; document lock ordering with the administrative coordinator.
- [x] 2.4 Test month/DST/expiry boundaries, unlimited-to-Free downgrade, repeated grant changes, and unchanged administrative permissions.
- [x] 2.5 Expose authenticated `/entitlements/me` and stable allowance/feature ProblemDetails contracts without provider calls or leaked audit information.

## 3. Document publication and authoritative issuance

- [x] 3.1 Inventory every creation, lifecycle, regeneration, delivery, and job writer; identify competing publication/revision boundaries and update all controlled consumers.
- [x] 3.2 Replace writable Invoice entity contracts with controlled requests; reject non-Draft creation and protected issuance/PDF/usage/legacy fields while retaining permitted business edits.
- [x] 3.3 Implement private generation-specific PDF storage and separate candidate preparation from committed pointer publication; retain readable legacy pointers and user-facing filenames.
- [x] 3.4 Protect all new document/lifecycle publications with shared coordination or conditional revision updates so stale renders cannot publish over newer state.
- [x] 3.5 Route all first-issue paths through one protected operation that refreshes state, checks allowance, captures one timestamp, stores a candidate, and commits invoice metadata plus a unique usage event atomically.
- [x] 3.6 Preserve immutable first-issue identity and legacy exemptions through correction/reissue/cancel/restore; retain usage after invoice deletion and avoid charging retries.
- [x] 3.7 Keep email/Drive outside the quota transaction and restricted to committed documents; preserve partial-success behavior without refunds.
- [x] 3.8 Add fault-injection tests for rendering, storage, stale revisions, database save/commit failure, and uncertain outcomes; verify uncommitted clean candidates cannot be read/delivered.
- [x] 3.9 Add PostgreSQL multi-context concurrency tests for the last allowance unit, duplicate issue requests, plan mutation races, shared legacy invoices, and stale pre-lock entities.
- [x] 3.10 Document safe best-effort candidate cleanup, retained published generations, and the bounded garbage-collection follow-up without deleting potentially committed/in-flight bytes.

## 4. Licence administration

- [x] 4.1 Add a minimal paginated/searchable licence directory and audit API protected solely by active current ManageLicences, independent of ManageUsers.
- [x] 4.2 Implement versioned permanent/expiring assignment and revocation under administrative-then-user lock ordering with transactional identity-snapshot audit and no-op handling.
- [x] 4.3 Test licence-only access, ManageUsers-only denial, actor capability revocation races, stale/missing-row versions, self/privileged-target grants, and audit survival after account deletion.
- [x] 4.4 Add standalone licence navigation, workspace hook, compact directory/editor/audit components, and truthful expiry/usage display without account-management controls.
- [x] 4.5 Apply existing notification policy: inline validation/conflicts for open editors and appropriate terminal feedback for completed actions; refresh edited assignment and effective entitlements.

## 5. Reporting and user-facing entitlement flows

- [x] 5.1 Gate paid-income summary and MCP business-summary aggregate with the shared reporting entitlement while preserving entitled calculations and owner-scoped raw records.
- [x] 5.2 Add frontend entitlement types/loading/refresh and restricted paid-income card/drill-down states without zero totals, client-side bypass totals, or repeated error toasts.
- [x] 5.3 Integrate accurate first-issue/no-additional-unit/unlimited wording into the existing shared issue confirmation and email/Drive reviews without extra prompts.
- [x] 5.4 Handle exhaustion and mid-review expiry/revocation inline, preserve draft/modal context, prevent delivery on failed issue, and refresh usage after committed issuance.
- [x] 5.5 Show current-plan/expiry/reset information and manual licence-administrator guidance without fictitious checkout/payment links.
- [x] 5.6 Update API types, Vite proxy, and service-worker API bypasses for new prefixes; keep workflow coordination out of large App.tsx bodies.
- [x] 5.7 Add backend/frontend tests for reporting gates, MCP tool errors, retained raw history/downloads/exports, correction at exhausted allowance, and combined delivery partial success.

## 6. Documentation and release verification

Local implementation is ready for PR validation, not release/archive. Remaining tasks require the user's pushed revision and real CI/staging/operator evidence. No commits, pushes, deployments, production IAM changes, local Playwright runs, or screenshot replacements were performed. GitHub Actions PostgreSQL service containers and `secrets.CI_POSTGRES_PASSWORD` remain unchanged.

- [x] 6.1 Update README, relevant engineering handbook/user guide, and AGENTS.md where operating constraints change; document no history window, provisional tiers, manual licensing, continuous usage, and UK reset rules.
- [x] 6.2 Update matching docs/uat journeys and Ubuntu browser fixtures for Free/paid/licence-only users, expiry, single confirmations, reporting restrictions, notifications, and mobile placement.
- [x] 6.3 Run backend suite serially, frontend tests/lint/build, relevant public-doc builds, and UAT Release compilation; run browser/documentation capture only in Ubuntu CI.
- [ ] 6.4 Run PostgreSQL migration/concurrency/failure tests in GitHub Actions using existing PostgreSQL service containers and `${{ secrets.CI_POSTGRES_PASSWORD }}` without insecure fallback.
- [ ] 6.5 Verify archived watermarking tasks 1.3 and 4.4 against actual PostgreSQL CI/staging evidence; leave them pending until that evidence exists.
- [ ] 6.6 Document and exercise staging/production writer-pause/drain cutover and rollback constraints so no old unmetered/mutable-key writer remains when enforcement goes live.
- [ ] 6.7 Perform staging entitlement/licensing/PDF failure-safety review and review actual Ubuntu browser results; record release evidence without claiming unrun checks succeeded.
- [ ] 6.8 Validate the complete OpenSpec change and reconcile implementation evidence with all requirements before archive.
