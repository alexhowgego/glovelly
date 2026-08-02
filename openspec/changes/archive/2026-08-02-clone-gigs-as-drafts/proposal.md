## Why

Cloning currently copies the source gig's lifecycle status, which can make a newly created gig appear already planned, completed, or cancelled. A clone is a starting point for new work and must be reviewed as a draft before it progresses through its lifecycle.

## What Changes

- Make every gig clone start with the `Draft` lifecycle status, regardless of the source status.
- Preserve the existing clone behaviour for reusable gig details, optional expenses, and cleared invoice linkage.
- Verify cloning from Planned, Completed, Cancelled, and Draft source gigs produces a Draft clone opened for review.
- Document the Draft-status expectation in the gig-cloning regression guidance.

## Capabilities

### New Capabilities
- `gig-cloning`: Defines how users create a new draft gig from an existing gig while preserving only reusable details.

### Modified Capabilities

- None.

## Impact

- Frontend clone request construction in `frontend/glovelly-web/src/hooks/useGigsWorkspace.ts`.
- Browser UAT coverage in `tests/Glovelly.Uat.Tests`.
- Manual regression guidance in `docs/uat/pre-merge-regression.md`.
