## Why

The independent `Show past gigs` control silently constrains quick filters, so completed historical gigs can disappear from `Uninvoiced`, `Completed`, and even `All`. Each gig-list view must instead describe its complete result set without depending on a separate date-visibility setting.

## What Changes

- Replace the `Show past gigs` control with a `Work queue` quick-filter view, selected by default.
- Define self-contained result sets for `Work queue`, `Upcoming`, `Uninvoiced`, `Drafts`, `Completed`, and `All`.
- Make `Uninvoiced`, `Drafts`, `Completed`, and `All` include matching historical records as applicable.
- Update explicit gig reveal behavior to use the complete `All` view rather than enabling historical visibility.
- Replace list-filter tests, the gig-list visibility specification, and UAT coverage for the removed control with coverage for the explicit views.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `gig-list-visibility`: Replace the independent historical-gig visibility setting with complete, named gig-list views and revise explicit reveal behavior.

## Impact

- Frontend gig filtering, workspace state, filter controls, and explicit gig navigation.
- Frontend unit tests for pure gig-list state.
- Gig history and selection UAT journey and the `gig-list-visibility` OpenSpec requirement.
- No backend API, persisted data, or dependency changes.
