## ADDED Requirements

### Requirement: Named gig-list views define complete result sets
The system SHALL provide the following terse named views in the gig workspace: `Work queue`, `Upcoming`, `Uninvoiced`, `Drafts`, `Completed`, and `All`. A selected view SHALL determine its complete candidate set without an additional historical-date visibility control. Search, type filtering, and sorting SHALL refine or order that candidate set without changing the view definition.

#### Scenario: Work queue is selected by default
- **WHEN** the gig workspace opens
- **THEN** `Work queue` is selected and includes all Draft gigs, non-cancelled gigs dated on or after the user's local calendar date, and completed gigs that are not invoiced

#### Scenario: Upcoming includes all non-cancelled future-dated records
- **WHEN** a user selects `Upcoming`
- **THEN** the list includes every non-cancelled gig dated on or after the user's local calendar date, regardless of whether its status is Draft, Confirmed, or Completed

#### Scenario: Historical invoice-ready work is visible
- **WHEN** a user selects `Uninvoiced`
- **THEN** the list includes every completed gig that is not invoiced, irrespective of its date

#### Scenario: Status views include historical records
- **WHEN** a user selects `Drafts` or `Completed`
- **THEN** the list includes every gig with the selected status, irrespective of its date or invoicing state

#### Scenario: All has no implicit exclusion
- **WHEN** a user selects `All`
- **THEN** the list includes every stored gig, including historical and cancelled gigs

## MODIFIED Requirements

### Requirement: Default gig visibility prioritizes active work
The system SHALL use the `Work queue` view as the default gig workspace view. The Work queue SHALL include all Draft gigs, non-cancelled gigs dated on or after the user's local calendar date, and completed gigs that are not invoiced. Gig date comparisons SHALL use the local `YYYY-MM-DD` calendar date.

#### Scenario: Historical completed uninvoiced gigs remain actionable
- **WHEN** the gig workspace contains a completed, uninvoiced gig dated before the user's local calendar date
- **THEN** the gig is included in the default Work queue

#### Scenario: Past drafts remain visible
- **WHEN** the gig workspace contains a Draft gig dated before the user's local calendar date
- **THEN** the gig is included in the default Work queue

#### Scenario: A gig dated today is upcoming
- **WHEN** a non-cancelled gig's date equals the user's local calendar date
- **THEN** the gig is included in the default Work queue and the Upcoming view

### Requirement: Explicit gig navigation reveals its target
The system SHALL treat an explicit request to select a gig that is not in the visible list as intent to reveal that gig. It SHALL clear active search and type filters, select the `All` view, preserve sort order, and display a workspace message explaining the changed view. When the explicit navigation is initiated by `Go to gig` after quick receipt or quick attachment capture, the system SHALL scroll the selected Gig overview into the viewport using smooth, start-aligned positioning while keeping that gig selected.

#### Scenario: Invoice-line navigation opens a hidden historical gig
- **WHEN** a user follows an invoice-line link to a past Completed or Cancelled gig hidden by the current view
- **THEN** the workspace selects `All`, clears incompatible filters, selects the target, and displays an explanation

#### Scenario: Saved gig is hidden by active filters
- **WHEN** a newly saved gig is intentionally selected but is excluded by the current list filters
- **THEN** the workspace clears incompatible filters, selects `All`, reveals and selects the saved gig, and displays an explanation

#### Scenario: Explicit selection preserves sort order
- **WHEN** explicit navigation reveals a gig that was hidden by filters
- **THEN** the target is shown at its position in the existing sort order

#### Scenario: Quick receipt navigation scrolls to its selected gig
- **WHEN** a user chooses `Go to gig` after saving a quick receipt
- **THEN** the Gigs workspace opens with the receipt's associated gig selected and its Gig overview scrolled into the viewport

#### Scenario: Quick attachment navigation scrolls to its selected gig
- **WHEN** a user chooses `Go to gig` after saving a quick attachment
- **THEN** the Gigs workspace opens with the attachment's associated gig selected and its Gig overview scrolled into the viewport

## REMOVED Requirements

### Requirement: Users can include historical gigs
**Reason**: The separate `Show past gigs` control makes named quick filters incomplete and can conceal historical records that the selected filter claims to show.

**Migration**: Use `Work queue`, `Upcoming`, `Uninvoiced`, `Drafts`, `Completed`, or `All`; select `All` to inspect every gig.
