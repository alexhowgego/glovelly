## Context

Gig cloning is a frontend workflow, not a dedicated API operation. The workspace constructs a new `POST /gigs` request from the selected gig, clears invoice fields, and optionally maps expenses without their attachments. It currently passes through the selected gig's status, so the regular create endpoint correctly persists the wrong lifecycle state for a clone.

The API intentionally permits callers to create gigs in supported lifecycle states. Changing that general create contract would affect imports, MCP, and other creation flows beyond cloning.

## Goals / Non-Goals

**Goals:**
- Ensure every clone request created by the Gigs workspace uses `Draft` status.
- Retain the existing copied core fields, optional expense mapping, invoice clearing, selection, and editor-opening behaviour.
- Exercise the browser workflow for every supported source status.

**Non-Goals:**
- Introduce a clone-specific API endpoint or alter the general `POST /gigs` lifecycle contract.
- Change lifecycle transition validation, source gigs, invoice handling, receipt handling, or clone field selection.
- Change the default status for normal new-gig creation.

## Decisions

### Set the lifecycle status in the frontend clone payload

The clone operation will submit the literal `Draft` status instead of reading the status from the selected source gig. This is the smallest change at the sole point where clone semantics are defined and leaves the API's reusable create contract unchanged.

Alternative considered: force `Draft` in `POST /gigs`. Rejected because the endpoint serves non-clone creation paths which legitimately supply their chosen status.

Alternative considered: add `POST /gigs/{id}/clone`. Rejected because cloning has no server-only requirements and a new endpoint would duplicate existing creation validation and persistence work for a one-field behavioural correction.

### Cover the complete UI-to-API clone path with parameterized browser UAT

A Playwright theory will create a source gig for each status, clone it, and assert the opened clone editor has `Draft` selected. This directly verifies the payload transformation, persistence result, and required review destination.

Alternative considered: backend endpoint tests. Rejected as they can test general gig creation but cannot establish how the frontend builds a clone request.

## Risks / Trade-offs

- [A source status might be accidentally reintroduced into the clone payload] → Parameterize the UAT test over Planned, Completed, Cancelled, and Draft sources.
- [Browser tests rely on a shared staging-style environment] → Use the existing UAT helpers, unique run identifiers, and response waits.
- [The UI calls `Confirmed` a planned gig while the API uses `Confirmed`] → Use the user-facing `Planned` label only for test selection and documentation; assert the clone UI's `Draft` value.

## Migration Plan

No data migration is required. The change affects only future clones; existing gigs retain their stored lifecycle status. Rollback is a frontend deployment rollback if necessary.

## Open Questions

None.
