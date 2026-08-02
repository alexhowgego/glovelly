## MODIFIED Requirements

### Requirement: Explicit gig navigation reveals its target
The system SHALL treat an explicit request to select a gig that is not in the visible list as intent to reveal that gig. It SHALL clear active search, type, and quick filters; SHALL enable `Show past gigs` when the target is a normally hidden historical gig; SHALL preserve sort order; and SHALL display a workspace message explaining the changed view. When the explicit navigation is initiated by `Go to gig` after quick receipt or quick attachment capture, the system SHALL scroll the selected Gig overview into the viewport using smooth, start-aligned positioning while keeping that gig selected.

#### Scenario: Invoice-line navigation opens a hidden historical gig
- **WHEN** a user follows an invoice-line link to a past `Completed` or `Cancelled` gig hidden by the current view
- **THEN** the workspace enables `Show past gigs`, clears incompatible filters, selects the target, and displays an explanation

#### Scenario: Saved gig is hidden by active filters
- **WHEN** a newly saved gig is intentionally selected but is excluded by the current list filters
- **THEN** the workspace clears incompatible filters, reveals and selects the saved gig, and displays an explanation

#### Scenario: Explicit selection preserves sort order
- **WHEN** explicit navigation reveals a gig that was hidden by filters
- **THEN** the target is shown at its position in the existing sort order

#### Scenario: Quick receipt navigation scrolls to its selected gig
- **WHEN** a user chooses `Go to gig` after saving a quick receipt
- **THEN** the Gigs workspace opens with the receipt's associated gig selected and its Gig overview scrolled into the viewport

#### Scenario: Quick attachment navigation scrolls to its selected gig
- **WHEN** a user chooses `Go to gig` after saving a quick attachment
- **THEN** the Gigs workspace opens with the attachment's associated gig selected and its Gig overview scrolled into the viewport
