# Gig List Visibility Specification

## Purpose

Define how the gig workspace prioritizes active work, includes historical gigs on demand, and maintains selection during list changes and explicit navigation.

## Requirements

### Requirement: Default gig visibility prioritizes active work
The system SHALL exclude a gig from the default gig workspace list only when its date is before the user's local calendar date and its status is `Completed` or `Cancelled`. Gigs dated today or later, and past `Draft` or `Confirmed` gigs, SHALL remain visible. Gig date comparisons SHALL use the local `YYYY-MM-DD` calendar date.

#### Scenario: Past completed and cancelled gigs are hidden
- **WHEN** the gig workspace opens with `Show past gigs` disabled
- **THEN** past `Completed` and `Cancelled` gigs are excluded while other gigs remain eligible for the list

#### Scenario: Past actionable gigs remain visible
- **WHEN** the gig workspace contains past `Draft` or `Confirmed` gigs
- **THEN** those gigs remain visible with `Show past gigs` disabled

#### Scenario: A gig dated today remains visible
- **WHEN** a gig's date equals the user's local calendar date
- **THEN** the gig is not treated as historical for default visibility

### Requirement: Users can include historical gigs
The system SHALL provide a visually apparent `Show past gigs` control in the gig workspace. Enabling the control SHALL include otherwise hidden historical gigs while preserving the active search, type filter, quick filter, and sort order. The control state SHALL apply only to the current workspace session.

#### Scenario: Historical visibility is enabled
- **WHEN** a user enables `Show past gigs`
- **THEN** normally hidden past `Completed` and `Cancelled` gigs participate in the current filtered and sorted list

#### Scenario: Existing filters remain active
- **WHEN** a user enables `Show past gigs` while search, type, or quick filters are active
- **THEN** the historical gigs are evaluated by those active filters rather than replacing them

### Requirement: Gig selection follows the visible ordered list
The system SHALL derive initial and fallback gig selection from the same filtered and sorted collection rendered in the gig list. A selected gig SHALL remain selected while it remains visible, regardless of list reordering. If the selected gig is no longer visible, the system SHALL select the first visible gig, or clear selection when no gigs are visible.

#### Scenario: Initial selection uses the first rendered gig
- **WHEN** gig data is first loaded into the workspace
- **THEN** the selected gig is the first gig after active visibility, filtering, and sorting are applied

#### Scenario: Existing visible selection is retained
- **WHEN** sorting or a non-excluding list change occurs while the selected gig remains visible
- **THEN** the system keeps that gig selected instead of selecting the new first row

#### Scenario: Hidden or deleted selection falls back
- **WHEN** a selected gig is removed from the visible list by a filter, update, deletion, or refresh
- **THEN** the system selects the first remaining visible gig or clears selection if the result is empty

### Requirement: Explicit gig navigation reveals its target
The system SHALL treat an explicit request to select a gig that is not in the visible list as intent to reveal that gig. It SHALL clear active search, type, and quick filters; SHALL enable `Show past gigs` when the target is a normally hidden historical gig; SHALL preserve sort order; and SHALL display a workspace message explaining the changed view.

#### Scenario: Invoice-line navigation opens a hidden historical gig
- **WHEN** a user follows an invoice-line link to a past `Completed` or `Cancelled` gig hidden by the current view
- **THEN** the workspace enables `Show past gigs`, clears incompatible filters, selects the target, and displays an explanation

#### Scenario: Saved gig is hidden by active filters
- **WHEN** a newly saved gig is intentionally selected but is excluded by the current list filters
- **THEN** the workspace clears incompatible filters, reveals and selects the saved gig, and displays an explanation

#### Scenario: Explicit selection preserves sort order
- **WHEN** explicit navigation reveals a gig that was hidden by filters
- **THEN** the target is shown at its position in the existing sort order
