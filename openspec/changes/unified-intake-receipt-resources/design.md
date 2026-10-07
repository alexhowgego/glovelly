## Context

This change remains uncommitted and is being revised in place. The initial implementation introduced private current-intake persistence, source-scoped receipt analysis, shared receipt/resource application, expense categories, and unified entry. Its UI and orchestration accumulated separate automatic completion and explicit review branches. Abrupt dialog transitions expose those branches to users and make state difficult to reason about.

The revised journey is fixed: `+ Add to Glovelly` → source type selection screen → unified upload dialog → optional Review attachment dialog. Receipt and existing-gig resource outcomes remain the scope. Saved receipts/resources remain ordinary business records; private intake is only a recoverable pre-application source.

## Goals / Non-Goals

**Goals:**
- Give every source and application mode one stable upload presentation and one compact attached outcome.
- Proactively save eligible receipts while the upload dialog stays open, allowing users to finish without review.
- Make saved-attachment review a deliberate, optional next step for both receipts and resources.
- Rebuild orchestration with explicit states, shared domain operations, readable components, and verifiable lifecycle rules.
- Preserve ownership, recoverability, source linkage, field validation, expense categories, and draft-only invoice refresh.

**Non-Goals:**
- Intake history, source library, expiry jobs, or source reuse across multiple gigs.
- Gig, tour, set-list, or other import application; automatic resource creation.
- Google Docs/Sheets content retrieval or general remote URL fetching.
- New gig-ranking thresholds, ambiguity gates, invoice wording, or reimbursement rules.

## Decisions

### One visible journey; automation only changes when application happens

The source-selection screen offers photo/file, URL, and pasted text. Choosing a type enters the unified upload dialog; the native file picker is launched within that phase, not directly from the global `+` action. Submission starts analysis immediately. Progress, supported intent choices, candidate selection, retries, and explicit application remain within this dialog.

Eligible receipts are applied proactively. After confirmed backend success, the same dialog displays `Attached to [Gig]` with `Review attachment` and `Done`. Explicit receipt and resource application reach exactly this state. No completion automatically closes the dialog, navigates to a workspace, or opens review. Notifications cannot replace this inline outcome.

Alternative rejected: preserve a special automatic completion path and adjust its transitions. That retains the branch-heavy experience rather than removing its cause.

### Explicit presentation state and durable application state

Rebuild the frontend workflow around explicit stages such as `source-selection`, `upload`, and `review`, and upload states such as `editing`, `submitting`, `analysing`, `awaiting-application`, `applying`, `attached`, and recoverable `error`. Represent valid transitions directly; derive busy controls and status copy from state rather than unrelated booleans and mutable status strings. Source, candidate, analysis, and attachment data belong to their relevant states.

Keep workflow coordination in a focused hook and presentational phase components, not large bodies in `App.tsx`. Use existing plain React/CSS styling, progress, modal overlays, and notification policy. Remove obsolete completion/fast-path flags and their consumers instead of keeping compatibility branches without an external need.

The API exposes analysis and attachment application results, not a frontend-specific `fastPathGigId`. Backend orchestration owns the decision to proactively apply a receipt and delegates both automatic and explicit application to the same operation. The result includes authoritative destination and receipt/resource identifiers sufficient for inline attached state and review. Represent failed application separately from analysis failure; never label an item attached before persistence succeeds.

Alternative rejected: client-driven automatic apply based on an eligibility ID. It duplicates policy orchestration in UI and encourages competing success branches.

### Preserve one private unapplied intake and clean successful sources

Reuse owner-scoped current-intake storage, source metadata, analysis-attempt provenance, replacement, discard, and blob handling. An unapplied intake survives close, refresh, and failure until replacement or explicit discard. No expiry job or history is introduced.

Successful application removes the private current intake and transient source after establishing the durable receipt attachment or resource relationship. Keep the authoritative application result in the open frontend session so cleanup does not erase the attached presentation. Reopening after completion starts a clean acquisition; correction remains available through the ordinary gig workspace.

Make application retry-safe: repeated or overlapping application must not create duplicate expenses/resources/attachments. Define a bounded application identity/result mechanism as part of the shared command contract, without retaining completed intake blobs or adding user-visible history. Closing or backgrounding while work is in flight must not destroy recoverability or cause a successful save to be duplicated on retry. Show pending work as pending; early completion is safe once `Attached to [Gig]` is confirmed.

### Reuse nearest-gig policy, not a new matching heuristic

Use the existing configured nearby range, local application clock, visibility, non-cancelled filtering, stable absolute-distance ranking, candidate count, and cutoff ties. Automatically apply to the first eligible candidate when receipt confidence meets the user's preference. Multiple nearby candidates do not block this decision. Preserve any established informational proximity warning as non-blocking guidance, not as a new threshold or separate journey.

Manual only disables proactive receipt application. High confidence accepts High; Medium confidence accepts Medium or High. Normalize legacy Very high confidence to High in the Settings presentation and preserve compatible stored values. Confidence affects receipt eligibility, not candidate ranking. No nearby candidate, insufficient confidence, or provider failure leaves the retained source awaiting explicit action within upload.

Candidate expansion is read-only and preserves source, selection, and entered fields. Loading more gigs never applies or reassigns an attachment.

### Shared application and optional saved-attachment review

Explicit receipt application selects a visible destination; resource application also requires validated type, purpose, and title. Treat-as overrides remain possible for uncertain/failed analysis, subject to existing source validation. Resources are never saved solely because a nearby gig exists.

Both receipt application modes copy independently validated high-confidence merchant, total, and category values into editable expense fields. Lower-confidence suggestions remain evidence for optional review. Categories remain nullable `Travel`, `Meals`, `Accommodation`, `Equipment`, or `Other`, editable on all expenses, and do not affect invoice wording or reimbursement rules.

`Review attachment` enters the saved attachment's correction dialog using authoritative identifiers. Adapt existing receipt/resource editing logic for inspecting the original source, correcting fields, reassignment, and deletion. Avoid re-uploading the source or creating another record on review entry. `Done` closes with the attachment saved; closing review without saving changes leaves the existing attachment untouched.

Reuse receipt/resource validations, attachment storage, invoice refresh, and workspace events through shared services. Rebuild intake endpoint orchestration around them. Remove superseded acquisition routes/components once consumers move; retain APIs that actually serve correction workflows.

### Settings help without altering field geometry

Keep the control labelled Automatic receipt application with aligned label/select rows. Explain that the preference permits attaching analysed receipts to the nearest nearby gig and that users can review/reassign later through existing focus help and a hover tooltip. Do not insert a multi-line explanation as an extra grid row inside one label. The settings response and current-user state must return the persisted preference.

### Source analysis remains bounded and privacy-conscious

Reuse Vertex structured-output validation, per-field evidence, provenance, MIME/size bounds, cancellation/timeouts, rate limiting, and safe telemetry. Preserve retry/manual fallback for unsupported, unavailable, malformed, and low-confidence outcomes. URLs are validated HTTP(S) references and receive metadata/type inference only; do not fetch linked Google document contents in this change.

## Risks / Trade-offs

- [Default attachment can choose an unintended gig] → Show its actual destination in attached state and provide optional reassignment/deletion review; retain Manual only.
- [Save succeeds but response is interrupted] → Make the shared application command retry-safe and distinguish pending/failed/attached states.
- [Cleanup conflicts with the open attached presentation] → Keep authoritative application identifiers in session state after private-source cleanup; ordinary gig records support later correction.
- [Clean rebuild loses previously working behavior] → Inventory reusable services and tests first; retain meaningful ownership, source cleanup, analysis, category, and invoice regressions.
- [Provider latency/outage blocks mobile capture] → Preserve submitted source, stable progress, retries, and explicit treatment in the same dialog.
- [Replacement leaves duplicate workflows] → Track consumer migration and removal explicitly; do not keep retired branch contracts as convenience wrappers.

## Migration Plan

1. Inventory the uncommitted implementation and retain validated domain persistence/services; do not revert unrelated user work.
2. Replace intake orchestration/contracts with shared retry-safe application results and automatic default saving.
3. Build the three presentation phases with explicit state transitions, then connect existing saved receipt/resource correction.
4. Move consumers to the replacement and remove obsolete acquisition components, fast-path contracts, and redundant routes/tests.
5. Reverify preserved migrations, ownership, storage cleanup, analysis, field initialization, invoice behavior, and the new journey; update UAT and applicable documentation.
6. Deploy the rebuilt flow together with its API contract. Rollback restores the prior build; saved expenses/resources remain normal records and unapplied intakes remain private.
