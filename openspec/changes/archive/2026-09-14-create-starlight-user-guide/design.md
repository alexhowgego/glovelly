## Context

`glovelly.net` is the public Astro landing site, `menu.glovelly.net` hosts the authenticated React application, and `handbook.glovelly.net` hosts the DocFX technical/operator handbook. `docs.glovelly.net` is reserved for a task-oriented user guide but has no site, Firebase target, deployment workflow, or content yet.

The existing staging Playwright UAT suite authenticates one shared regression account through a protected staging-only endpoint. Its fixture is upserted on login but run-created data persists, diagnostics are failure-only, and its timestamped records cannot produce repeatable documentation images.

## Goals / Non-Goals

**Goals:**
- Publish a fast, accessible, searchable Starlight guide with a practical "do this next" voice for working musicians and sole traders.
- Teach the currently shipped core loop without presenting accounting advice, unfinished features, or technical implementation detail.
- Reproduce the landing site's public visual vocabulary without sharing source code or coupling deployments.
- Generate selected, real application screenshots from a dedicated deterministic staging fixture and make visual drift visible in the normal PR flow.
- Keep image publication reviewed and versioned in the repository rather than automatically deploying ephemeral CI output.

**Non-Goals:**
- Replacing the DocFX handbook, application UI, access model, or the future entitlement work in #192.
- Documenting specialist imports, MCP, set lists, administrator operations, pricing, plans, or payment workflows in V1.
- Building a CMS, analytics, support-ticket system, tutorial video library, or generic visual-regression platform.
- Capturing real external-service accounts, sending emails, or using production/personal data for documentation images.

## Decisions

### Create an independent Starlight project and Firebase target

Create `frontend/glovelly-guide` as a static Astro/Starlight project with locally owned content, styles, assets, and scripts. Add a `user-guide` Firebase Hosting target that publishes its `dist` output, and a guide-specific preview/production workflow modelled on the landing workflow.

Starlight supplies content collections, responsive navigation, search, accessible defaults, and predictable Markdown/MDX authoring without a client application runtime. The project SHALL link to the front door, Menu, handbook, legal documents, and repository through canonical public URLs.

Alternatives considered:
- Extend DocFX: rejected because the handbook's information architecture and reference voice are intentionally technical/operator focused.
- Add routes to the authenticated React SPA: rejected because the guide must be public, independently deployed, and lightweight.
- Build custom Astro documentation navigation: rejected because Starlight already provides the guide-oriented primitives required by the issue.

### Organise the first release around the work loop

Use the following V1 navigation, with concise pages that begin with a user outcome, state prerequisites plainly, use short steps, and explain what happens next:

```text
Start here
  What Glovelly is for
  Sign in or request access
  Your first invoice

Core work
  Clients
  Gigs
  Expenses, receipts, and mileage
  Create, review, and send invoices
  Invoice status, payments, and income view

Set up
  Seller profile
  Settings and defaults

Help
  Common questions
  Privacy, terms, handbook, GitHub
```

`Your first invoice` SHALL span creating a client, recording a gig, optionally recording costs, generating an invoice, and reviewing its delivery. The guide can describe invitation/access-request behavior factually: Google sign-in identifies the user, while access is currently granted by invitation or request approval. It SHALL not promise self-service enrolment, review times, plans, or entitlement limits.

The guide adopts the landing site's warm paper palette, deep blue structure, restrained orange actions, editorial display typography, practical body typography, rounded surfaces, and visible keyboard focus. These are duplicated as guide-local tokens rather than a shared package. The guide must use prose and examples to teach tasks; the handbook retains concise reference language. Record the distinction in the guide design/specification and the durable agent orientation once the project exists.

### Make screenshots reproducible, reviewed guide assets

Checked-in images under the guide's static asset tree are the published screenshots. A dedicated Playwright capture suite recreates their source state and writes consistently named candidate PNGs to a configurable artifact directory. The guide may layer callouts or annotations in its own markup/CSS, so raw product captures remain reusable and image replacement does not require raster editing.

The capture context SHALL pin Chromium through the restored Playwright version and set fixed viewport, locale, timezone, colour scheme, reduced motion, and a frozen browser clock. It SHALL wait for fonts and settled application data, use selected element screenshots where possible, and capture only controlled product states. Screenshot coverage begins with representative core workflow states, not every UI control.

Alternatives considered:
- Manual screenshots: rejected because stale images are hard to detect and reproduce.
- Auto-commit screenshots from CI: rejected because generated UI changes need human review and CI credentials should not mutate contributor branches.
- Deploy captures directly from staging: rejected because user-guide availability would become coupled to staging health and unreviewed visual changes could publish.
- Reuse failure diagnostics: rejected because those are full-page, failure-only, and based on shared mutable UAT data.

### Isolate and reset a staging-only Glovelly Docs fixture

Add a fixed `Glovelly Docs` identity separate from the existing UAT regression account. A protected staging-only fixture operation SHALL allow only the predefined documentation account, reset all its owned data to a known blank state, seed the fixed screenshot scenario, and issue an authenticated session for that account.

The reset must account for owned relational records and storage-backed attachments. It SHALL remove associated blob objects as well as database rows, reset user/profile/default values that influence captured UI, and be safe to run repeatedly. The capture suite SHALL reset and seed before capture and attempt a final reset in `finally`; a subsequent run's initial reset remains the recovery path after cancellation or infrastructure failure. It shall not delete or alter UAT or non-documentation user data.

Capture scenarios shall avoid delivery and external side effects. For example, the invoice email review dialog is captured before send, while Drive, Calendar, real email outcomes, and live uploaded receipt images remain outside initial screenshot coverage.

### Report screenshot drift without gating merges

After a same-repository PR's staging deployment and normal UAT run, run the documentation capture suite as a separate informational job. It SHALL compare candidate images with checked-in guide images, upload candidates and any comparison report as a named artifact, and update one marker-based PR comment with the current/stale result, changed asset names, and the workflow artifact location.

The job SHALL report drift successfully rather than fail the pipeline in the initial release. It must not run with staging secrets or comment permissions for forks. It requires narrowly scoped pull-request/issue-comment permission only in the calling workflow path that meets this condition.

## Risks / Trade-offs

- [Fixture reset misses a newly added user-owned entity] -> Keep reset ownership-scoped, test it against a populated scenario, and update it whenever captured workflows gain dependent storage/data.
- [A cancelled capture leaves data behind] -> Reset before every run as well as in final cleanup.
- [Visual comparisons are noisy] -> Pin browser/runtime conditions, freeze time, capture stable elements, and begin as informational reporting.
- [The guide falsely promises product behavior] -> Derive V1 content from currently shipped UI/UAT journeys and keep specialist or planned features out of scope.
- [Guide and landing visual styles drift] -> Share documented tokens and patterns, not source code; review public surfaces together where a change affects them.
- [Staging credentials or PR commenting are exposed to untrusted code] -> Run capture/comment jobs only for same-repository PRs after the trusted staging deployment path.

## Migration Plan

1. Add and locally validate the Starlight project and initial guide content before attaching it to Firebase Hosting.
2. Configure the Firebase `user-guide` target and publish a preview/platform-domain build; verify navigation, search, accessibility, and public URLs.
3. Add the isolated fixture/reset endpoint and capture suite, then prove it does not mutate the existing UAT account.
4. Add informational capture artifacts and same-repository PR comments after staging UAT; review the first candidate set and commit approved images.
5. Deploy the guide target to `docs.glovelly.net`, verify the landing, Menu, handbook, legal, and repository links, and retain ordinary Firebase rollback by redeploying the last known-good static artifact.

## Open Questions

- Which fixed desktop and mobile viewports best match the guide layouts and documented user tasks?
- Should candidate comparison use Playwright's snapshot assertions adapted to report-only behavior or a small dedicated pixel-diff utility?
- Which subset of the initial guide pages merits screenshots before the capture fixture expands beyond the core workflow?
