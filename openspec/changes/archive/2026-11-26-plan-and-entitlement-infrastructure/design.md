## Context

Glovelly is user-scoped, not tenant-scoped. Capability-based administration already defines `ManageLicences`, but current frontend administration is largely entered through `ManageUsers`. Invoice first-issue identity exists as `FirstIssuedUtc` / `FirstIssuedByUserId`; issue, redraft, reissue, PDF regeneration, email, and Drive workflows already share services. Draft delivery is prohibited and existing styled confirmations coordinate issue-before-delivery.

There are two important gaps. Invoice creation binds the `Invoice` entity, accepting caller-supplied status/audit fields unless explicitly reset. Also `InvoicePdfStorage` currently writes a mutable `invoice.pdf` key before callers save invoice changes. A quota check alone would neither prevent alternate creation paths nor prevent clean bytes replacing a committed draft's blob when issuance fails.

The paid-income summary is fetched independently for dashboard cards and a reconciled invoice filter. The MCP business summary also computes paid totals. There is no demonstrated general-account export endpoint; existing downloads/exports and raw owner-scoped records must remain available, and this change must not imply that a new comprehensive export product already exists.

## Goals / Non-Goals

**Goals:**
- Resolve effective product access from local plan/subscription state independently of authentication, administrative permissions, and future billing providers.
- Enforce first issuance once per invoice, atomically against the billing user's UK-month usage, with safe PDF publication and meaningful failure/retry behavior.
- Support audited manual grants, optional expiry, permanent Founder grants, and automatic Free fallback without disrupting existing signed-in users.
- Gate the agreed reporting convenience while preserving underlying business data and existing exports.
- Integrate into existing shared confirmation/delivery and notification patterns, not new parallel invoice journeys.

**Non-Goals:**
- Stripe/payment calls, purchased credit balances, rollover, calendar billing cycles from a provider, final pricing, or tax/VAT calculations for subscriptions.
- Tenants, seats, multi-user plans, history restrictions, new general-account export, unrelated feature gates, or restrictions on downloading existing documents.
- A self-service paid checkout or plan-catalogue editor. Solo and Pro initially share unlimited issuance and reporting access; differentiation remains provisional.

## Decisions

### 1. Small, versioned local catalogue and per-user subscriptions

Use persisted `Plan` records with stable codes `free`, `solo`, and `pro`, a display name, nullable monthly first-issue limit, and a typed PaidIncomeSummary feature grant. Seed through migrations and matching development/testing infrastructure:

| Plan | Monthly first-issued invoices | PaidIncomeSummary |
|---|---:|---|
| Free | 2 | No |
| Solo | Unlimited | Yes |
| Pro | Unlimited | Yes |

Values are an editable-by-migration/versioned catalogue, not scattered inline tier comparisons or environment variables. Resolve features and limits through one entitlement service. Do not add booleans for features not yet implemented.

One current `UserSubscription` per user holds assigned plan, `Active`/`Cancelled` state, grant start, optional exclusive end instant, provider/source metadata, and a concurrency version. Current assignments are Manual; Founder/Tester/goodwill are reasons, not providers or duplicated plans. An expiring Active grant is sufficient for manual trials; do not simulate payment-provider Trialing/PastDue behavior yet.

No row, cancelled state, or `now >= EndsAtUtc` resolves to Free. Manual grants start immediately; future scheduled starts and overlapping assignments are excluded. Paid grants may be permanent or have a future expiry. Explicit assignment to Free clears paid expiry. A user with an expired Pro grant can see assigned-versus-effective state, but ordinary requests consume only effective Free entitlements.

Alternative: storing a plan code directly on User loses expiry, provenance, and clear future provider ownership. Introducing tenant infrastructure now is unnecessary.

### 2. Founder migration is a one-time, auditable rollout

At migration time, assign permanent Manual Pro, reason `Founder`, only where both a non-empty bound Google subject and non-null `LastLoginUtc` exist. Do not add an `IsActive` condition to this agreed eligibility predicate; inactive eligible accounts still retain their independent access restriction. Unbound users and users who have never signed in remain Free, even if their first sign-in occurs immediately afterward.

Record one migration-source assignment audit per affected user with a stable rollout identifier. Migration replay/startup cannot re-grant Pro over a later operator decision. Do not bind eligibility to current email, administrative privileges, or future login handlers. Local/UAT fixtures must specify intended plans explicitly rather than relying on migration execution against EF in-memory.

### 3. Usage follows the user, independent of plan

Persist an append-only `UsageLedger` for `InvoiceFirstIssued`: billing user, invoice source ID, quantity one, occurrence timestamp, and relevant provenance. Uniqueness on metric + source invoice ID ensures an invoice consumes at most one unit, including intentionally shared legacy records. Do not use a cascading invoice foreign key: deleting a previously issued invoice after reissue must not erase its usage.

Track events on unlimited plans as well as Free. A Pro user who has issued ten invoices and changes to Free in the same month has ten used against two allowed, zero remaining, and cannot first-issue another until reset or a plan change. Reissue, regenerated PDFs, delivery/retry, paid/cancelled transitions, and deletion do not add events or refund existing ones.

Compute month boundaries in `Europe/London`, then query the half-open UTC interval `[start, nextStart)`. Store instants in UTC; use `TimeProvider` for expiry, period, and issuance decisions. Never use invoice dates, caller timestamps, or a server-local timezone to choose the allowance period. Unused allowance does not roll over. Capture the authoritative first-issue timestamp once inside the protected issuance operation and use that same instant for its ledger event and accounting month, even if completion crosses midnight.

The billing user is the authenticated user issuing an ordinary owned invoice. Preserve existing intentional null-creator visibility; for a genuinely shared legacy draft, bill its first authenticated issuer once without claiming/reassigning record ownership. Backfill historical usage only once per invoice with an unambiguous creator/first issuer; never duplicate null-creator history across all users.

Alternative: counting current invoice rows loses deleted events and can be manipulated through state changes. Resetting usage on plan assignment creates downgrade/regrant loopholes.

### 4. Serialize allowance consumption and assignment changes per billing user

Use a transaction-scoped PostgreSQL advisory lock in a dedicated, documented stable namespace, keyed deterministically by user ID. Hash collisions may serialize unrelated users but must never bypass a lock. Avoid process-dependent hash functions. An in-memory keyed semaphore provides process-local fixture behavior, not evidence of cross-instance safety.

First issuance and subscription mutation for the target user acquire this same lock and re-read current subscription, usage, first-issue identity, and invoice state after waiting. The invoice read must also be refreshed/revision-checked, not merely reused from a tracked endpoint entity loaded before the lock. Two requests competing for the last unit cannot both succeed; duplicate requests for one invoice cannot double-charge.

Licence mutations additionally acquire the existing administrative mutation coordinator first, then the target entitlement lock; recheck the actor's active `ManageLicences` under that administrative boundary. This coordinates permission revocation with commercial mutations without inventing Super Administrator privilege bypasses. Issuance acquires only the entitlement lock; no path may acquire these locks in the reverse order.

Assignment and its audit commit together. Ledger consumption and invoice first-issue/document publication metadata commit together. Recheck effective access for every protected request; no long-lived session claim or billing-provider call is authoritative.

### 5. Prepare private document generations, then publish the committed pointer

Move all new invoice PDF writes to private generation-specific keys, for example `users/<owner>/invoices/<invoice>/generations/<guid>.pdf`. Keep user-facing filenames unchanged. Existing legacy storage pointers remain readable under current authorization/readiness checks; do not eagerly regenerate historical PDFs.

Split document preparation/storage from publication of invoice metadata inside the shared PDF/workflow services:

1. Begin protected issuance, reload authoritative state, resolve effective plan and current-month usage, and determine whether this invoice is genuinely first-issued.
2. Reject an exhausted allowance before rendering clean output. If this is a subsequent issue/correction, do not consume another unit.
3. Build the intended Issued invoice representation using the authoritative timestamp and existing defaults/lines, retaining immutable original first-issue fields when present.
4. Render clean bytes and save to a new private generation key. Do not overwrite the committed draft's key or expose the candidate through a separate API.
5. Update invoice state, document generation pointer/provenance/revisions, and first-issue fields, and add the unique ledger event where required.
6. Commit the database transaction. Only then return successful issuance and allow ordinary authorized reads/delivery to discover the clean pointer.

Rendering/storage/database failures leave the committed draft pointer and allowance unchanged. A saved candidate whose database transaction fails is an unreachable orphan, not a published PDF. Delete that candidate best-effort on known failure; never delete a blob if commit outcome is uncertain. Retain old published generations initially rather than delete bytes that in-flight authorized reads might still use. Bounded orphan/obsolete-generation garbage collection is a documented follow-up, not an unsafe broad sweep.

Unique keys are required for every new generation, including draft redraft/reissue/regeneration, so another writer cannot mutate a generation already referenced by committed state. Publication must respect current invoice revision/lifecycle; stale work must not replace a later document. All competing document/lifecycle writers for an invoice must coordinate with the issuance boundary or perform a conditional revision update that fails safely.

Email/Drive delivery remains outside the quota transaction and uses the committed published document. Delivery failure does not roll back issuance or refund usage. Existing thumbnail ownership, no-byte-cache behavior, blurred updating placeholders, and independent full-preview reads remain unchanged.

Alternative: a check-then-render sequence using the mutable blob key can expose clean output after rollback. A distributed database/blob transaction is unavailable; a private immutable candidate plus transactional pointer is the practical publication boundary.

### 6. Harden every lifecycle entrypoint and reconcile legacy identity

Replace Invoice entity binding with controlled create/update requests. New public/API invoices begin Draft; reject non-Draft create status and supplied first-issue, PDF storage/provenance, usage/billing subject, legacy exemption, or other lifecycle audit fields. Generic updates that request issuance must delegate to the same protected workflow as the status route. Never retain a second unmetered creation/issuance route for convenience.

First-issue identity is server-owned and cannot be cleared by cancel, restore, reissue, update, delete/recreate tricks, or incoming JSON. Existing previously issued invoices remain correctable at exhausted allowance, including after grant expiry.

Migration backfills ledger events from trustworthy existing `FirstIssuedUtc` for unambiguously attributed invoices, preserving the actual timestamp rather than charging them at deployment. For trusted legacy rows with evidence of prior issuance but missing time (Issued/Paid/Overdue or reissue history), persist a server-owned migration-only grandfathered-issuance marker instead of fabricating a historical date or charging a correction as new issuance. Never classify a never-issued Cancelled row as issued just because of cancellation or historical draft delivery. Newly created rows cannot obtain that marker. Document how ambiguous legacy attribution is reported and exempted without altering record visibility.

### 7. Capability-separated licensing workspace

Expose a standalone licence workspace and `/admin/licences` routes reachable with `ManageLicences` alone. A dedicated paginated/searchable directory returns only user ID, email/display name, active/enrolled indication, assignment/effective plan, expiry, usage, and version—not owned business data or Google subjects. Do not make licence-only users fetch the protected ManageUsers directory or gain account/permission-editing controls.

The licence dashboard uses a separate server aggregate, not totals inferred from paginated rows: effective Free accounts, effective Solo/Pro accounts, and active paid grants ending within 30 days. Include inactive accounts without implying sign-in access or payment revenue. Use the same effective-grant predicate as entitlement resolution, and refresh after assignment changes and workspace entry/focus.

All five record sections share a `RecordWorkspace` frame for directory/overview/editor geometry, while retaining domain row styles and workflows. The shell declares its layout contract explicitly rather than enumerating section names in viewport CSS. Desktop panes fill available height with contained scrolling; small/short viewports retain document flow. This consolidates layout rules without broad workflow rewrites or a new component library.

Authorized administrators can assign existing plans, set/remove future expiry, or revoke paid access with an optional bounded reason. Require the current assignment version (including an explicit missing-row version) and reject stale edits with conflict. No-op saves create no misleading audit entries. Actor/target identity snapshots, old/new assignment state, timestamp, source, and reason persist atomically and survive account deletion using the existing administrative audit pattern. Ordinary account deletion may remove live subscriptions/usage for the deleted account under the existing retention model; it must not erase historical licence audit or block deletion unexpectedly through new foreign keys. Do not add a general usage-reset or allowance-refund control.

Licence managers can assign their own plan and plans for privileged users because commercial access does not grant administrative authority; every change remains audited. General user administration cannot modify subscription fields through account CRUD. Holding Super Administrator does not implicitly grant an unlimited product plan.

### 8. Reporting gate and preserved data access

The paid-income-summary endpoint requires PaidIncomeSummary. Preserve UK financial-year/date, rounding, ownership, inclusion, and contributing-ID semantics for entitled users. Gate the MCP `glovelly_get_business_summary` aggregate using the same feature because it exposes paid-income totals; ordinary invoice/gig/receipt listing and detail remain available. This monetizes reporting convenience, not possession of underlying facts: users may still calculate totals from their own records.

Frontend dashboard state distinguishes restricted from loading/error/zero. Show a compact Solo/Pro-required explanation, not zero income, a persistent health warning, or repeated authorization-error toasts. Do not use browser-side totals as a fallback that bypasses the gate. Reconciled income drill-down is unavailable when restricted; ordinary paid-invoice filtering/history and paid-status recording remain available.

No date-based record hiding or deletion is introduced. Existing authenticated PDFs, receipts, expense statements, forScore exports, and raw record retrieval remain plan-independent. A complete general-account export is not added or claimed here.

### 9. Self-service snapshot and consistent actionable errors

Provide `/entitlements/me` with effective/assigned plan, expiry/fallback state, feature grants, usage, limit (`null` for unlimited), remaining allowance clamped at zero, and UK month/reset instants. Expose no provider secrets or another user's audit information. The snapshot is explanatory; server mutation checks remain authoritative.

Use HTTP 403 ProblemDetails with stable codes such as `invoice_allowance_exhausted` and `feature_not_entitled`, current limit/usage/reset metadata where appropriate, and no misleading authentication-expired behavior. HTTP 409 remains for stale/concurrent state conflicts. MCP returns the corresponding tool error without a partial paid aggregate.

Integrate into the existing direct-issue, email-review, and Drive action confirmation:
- New first issue on a metered plan explains one allowance unit and the remaining/reset information.
- Corrections/subsequent issues explicitly cost no additional unit; unlimited plans do not pretend to have a credit balance.
- Exhausted first issuance offers an inline explanation and does not attempt delivery; drafts remain editable/downloadable.
- Refresh entitlement state after issuance and manual assignment and on relevant workspace entry; handle grant expiry/revocation while a dialog is open.
- Do not add a second confirmation, fake checkout/upgrade button, or payment-provider call. Explain that plan changes are currently handled by a licence administrator.

New API prefixes require Vite proxy and service-worker bypass updates. Keep orchestration in existing hooks/services and the shared delivery coordinator rather than grow App.tsx.

## Risks / Trade-offs

- [Global accounting and multi-instance races] → Per-user serialization shared with assignment, unique ledger source IDs, fresh reads, and PostgreSQL concurrency tests including failed commit.
- [Generation-specific storage grows] → Best-effort candidate cleanup, documented retained generations, and a follow-up for safe bounded garbage collection; no deletion of possibly committed/in-flight bytes.
- [Legacy audit fields are incomplete] → Explicit migration grandfathering and accurate backfill only where timestamp/subject are trustworthy; never invent usage dates.
- [Expiring/manual grants surprise users] → Expose end instant/effective Free fallback and continuous usage clearly; preserve records and previously issued documents.
- [Mixed old/new writers bypass quota or mutate legacy keys] → Treat full writer cutover as mandatory before declaring enforcement live. Pause invoice writes/enrolment or use an equivalent verified maintenance procedure during migration/drain; do not assume old instances honor new rules.
- [Provider integration later competes with manual overrides] → Keep provider/source provenance and one effective local resolver; precedence/webhook semantics require their own later proposal.
- [Free reporting becomes unavailable] → Founder preservation, explicit locked states, consistent MCP gate, and continued raw-data/history access.

## Migration Plan

1. Confirm the watermarking prerequisite's outstanding PostgreSQL/browser/staging validation before release, without marking it passed speculatively.
2. Add catalogue/subscription/audit/ledger/schema, generation publication support, and trusted legacy issuance marker. Exercise empty-database and populated upgrades in isolated PostgreSQL.
3. Seed plans, perform one-time Founder assignment/audit, and backfill accurate historical usage/legacy markers transactionally. Verify ineligible/unbound users remain Free and repeated startup cannot re-grant access.
4. Upgrade all application/job writers and controlled request consumers. Plan an explicit maintenance/drain procedure for production; no unsupported old writer may continue issuing after enforcement is declared live.
5. Verify Free exhaustion, UK month/DST boundaries, concurrent issuance/plan changes, expiry, licence-only administration, reporting restrictions, raw-data/export access, and clean-PDF failure safety on staging. Run browser/documentation capture only in Ubuntu CI.
6. Prefer forward fixes. Preserve ledger/audit and published pointers on rollback. Rolling back to pre-entitlement code disables enforcement and can write mutable blobs; do not run it alongside quota-enforcing writers or treat a plan-only rollback as commercially safe.

## Open Questions

- None blocking proposal readiness. The user confirmed Free = 2/month in Europe/London, reporting gated to Solo/Pro, Free fallback, continuous usage, and permanent Founder grants unless explicitly changed.
- Implementation must document the actual production write-pause/drain procedure and storage-retention follow-up before release; these are verification tasks, not permission to weaken the publication/allowance invariants.
