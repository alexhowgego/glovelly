# Local plans, usage, and invoice publication

## Commercial state

Product access is per user and independent of administrative permissions. The migration-seeded catalogue defines Free (two first-issued invoices per Europe/London calendar month, no paid-income reporting), Solo and Pro (unlimited issuance and reporting). Solo/Pro differentiation and public pricing remain provisional. Payment processing is not integrated.

The local `EntitlementService` resolves subscription expiry on every request; it does not need a scheduled downgrade job. Missing, cancelled, or expired paid access falls back to Free. Founder grants are permanent unless explicitly changed. The one-time Founder migration selects bound Google identities with populated `LastLoginUtc`, not future sign-ins or permission sets. An inactive eligible user remains inactive.

Usage is an append-only first-issue ledger, including on unlimited plans. Downgrades/grants do not reset it; correction, reissue, cancellation, deletion, and delivery failure do not refund it. Reset is UK calendar-month midnight, not a rolling window or a provider billing anniversary. Raw history and existing downloads/exports remain available; no history window or new general-account export is introduced.

## Licence workspace summary and layout

`GET /admin/licences/summary` requires ManageLicences, not ManageUsers. It aggregates all accounts independently of directory search/pagination: effective Free accounts, effective Solo/Pro accounts, and currently effective paid grants ending within the next 30 days (inclusive upper boundary). Inactive accounts remain included because product grants do not activate accounts. Expired, revoked, and not-yet-started grants count as Free; permanent grants do not count as expiring. These are product-access counts, not paying-customer or revenue metrics.

The dashboard refreshes on workspace entry, window focus, and successful licence changes. Loading/unavailable states never invent zero counts. All five record sections use `RecordWorkspace` for shared directory/overview/editor geometry. `AppShell` opts into viewport containment through `data-layout="record-workspace"` rather than a section-name allowlist; small/short viewports retain document flow.

## Write-boundary inventory

- `POST /invoices` creates Draft only through a controlled request; generic `PUT /invoices/{id}` and status updates delegate issuance to `InvoiceWorkflowService`.
- Gig invoice generation and combined-gig selection create watermarked Drafts. Redraft, reissue, Cancelled restoration, explicit regeneration, and manual adjustment workflows produce replacement PDF generations.
- Generic invoice-line routes and generated-line/expense refreshes can modify PDF inputs. `AppDbContext` updates their parent invoice concurrency token and invalidates an existing document unless the same save publishes a newly prepared replacement. Moving a line affects both parents.
- Receipt/intake application calls draft-gig refresh/obligation methods; these participate in parent invoice concurrency even when running outside an invoice endpoint.
- Email and Drive read a committed document after issuance and outside its quota transaction. No provider delivery occurs inside that transaction.
- Development and documentation fixture seeders use the same PDF service but are trusted fixture writers, not public issuance routes. All new storage writes use unique generation keys.
- User fixture reset/account deletion are explicit data-lifecycle operations, not allowance refunds for retained accounts.

## Concurrency and publication

Lock order is administrative coordinator (licence changes only), then billing-user entitlement lock, then invoice row. PostgreSQL uses transaction-scoped two-integer advisory namespace `0x476C456E` and a deterministic SHA-256 user-ID key. The invoice row lock also handles shared legacy records issued by different users. In-memory bounded semaphore stripes provide fixture behavior, not distributed atomicity.

Invoice `MutationVersion` is an application-managed optimistic concurrency token updated on every invoice mutation, including invoice-line changes. A pre-lock tracked invoice is checked against fresh persisted state. A stale render/save fails with conflict rather than publishing over newer state.

PDF generation stores private unique `generations/<guid>.pdf` candidates. Preparing bytes does not publish metadata. Invoice lifecycle, the published generation pointer, and a first-issue ledger event commit in one transaction. Existing legacy keys remain readable; new writes never overwrite them. Rendering/storage/save failure leaves the committed document and usage unchanged. Commit-outcome uncertainty requires retaining candidate bytes.

Known pre-commit database save failures attempt deletion of only their own unique candidate. Cleanup failure must not hide the original error. Old published generations are retained so in-flight reads are not broken. Storage growth is an accepted interim trade-off.

**Follow-up:** bounded orphan/obsolete-generation collection needs a separate safe retention design: account for active transactions, uncertain commits, and in-flight reads before deleting anything. Never sweep all unreferenced keys immediately or infer safe deletion from a single database snapshot.

## Migration and validation

`AddPlanEntitlements` seeds the catalogue, assigns/audits Founders once, backfills trustworthy historical first-issue timestamps/subjects, and marks legacy issued/reissued rows without inventing missing timestamps. Rows with unknown attribution are not charged to every user. A timestamp without attributable identity remains historical first-issue evidence but has no fabricated ledger subject. Never-issued cancellation or historical draft delivery alone does not grant prior issuance.

`AddInvoiceMutationVersion` adds optimistic publication protection. All API/job writers must be upgraded before enforcement is considered live. A production write/enrolment pause and old-instance drain must be planned and exercised before release; rolling old mutable-key/unmetered writers alongside new ones is unsafe. Prefer forward fixes and preserve usage/audit/pointers rather than treating schema rollback as a safe commercial rollback.

Local PostgreSQL evidence does not replace GitHub Actions service-container validation or staging/browser review. Keep outstanding archived watermarking validation pending until its actual CI/staging evidence exists.

### Writer cutover runbook (operator rehearsal required)

This is an intentional maintenance deployment, not a normal mixed-revision rollout. Rehearse on staging before production. No production pause/deployment has been executed as part of implementation.

1. Resolve the exact service, project, region, database migration job, receipt-analysis jobs, and trigger names from the environment's existing GitHub variables/deployment configuration. Record current revisions, tags/traffic, invoker policy, running executions, scheduler/trigger state, and image digests. Do not guess production resource names or put credentials in repository files.
2. Announce the window and pause other deployment workflows. Stop triggers that can start invoice-affecting analysis/application work. Cancel/drain already running old-image job executions and verify their tasks have terminated.
3. Block application invocation **before** migration/Founder selection. Where the service uses the existing public invoker binding, temporarily remove that binding under a reviewed IAM change; account for authenticated invoker bindings too. Verify both the custom domain and direct service/revision-tag URLs cannot invoke old writers. A browser maintenance banner alone is not a write barrier. Ensure deployment tooling will not re-enable public invocation automatically before verification.
4. Wait out the maximum configured request timeout and verify old requests and database writer sessions have drained. Include enrolment callbacks, MCP, receipt jobs, and retries—not just invoice buttons. Keep the barrier in place if any old writer is still live.
5. Run the new migrations once, then deploy the same new image to the API and every relevant worker/job template. Remove old tagged traffic paths and verify 100% traffic points to new-code revisions. Preserve the recorded ledger/audit and existing document pointers.
6. With invocation still restricted, use an authorized operator/staging test path to verify migrated catalogue/Founder counts, expected ineligible Free users, historical backfill/exemptions, and current image digests. Exercise new Draft→Issued quota/publication behavior and failure safety on isolated fixtures. Do not change a real user's plan merely to test.
7. Restore only the recorded invoker/trigger state after every writer has passed version verification. Resume scheduling and deployments, check ordinary sign-in and existing document reads, and review usage/audit for unexpected changes.
8. Attach the actual staging rehearsal and production cutover evidence to the release. If a barrier or writer inventory cannot be verified, stop: do not declare quotas live.

Rollback must retain the writer barrier until a single compatible writer version is chosen. Prefer a forward fix. Restoring old code also restores unmetered paths and mutable-key writes; never mix it with enforcing writers, erase ledger/audit, or run destructive down migrations as an automatic rollback.
