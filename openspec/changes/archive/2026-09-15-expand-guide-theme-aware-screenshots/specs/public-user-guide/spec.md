## ADDED Requirements

### Requirement: Selected workflow guidance uses theme-aware product imagery
The guide SHALL use real, reviewed product screenshots to support the Clients, Gigs, Expenses, Create/review/send invoices, Invoice status/payments/income, Seller profile, and Settings/defaults workflows. Each screenshot state SHALL have a light Glovelly UI asset and an actual-dark Glovelly UI asset. The active Starlight light or dark theme SHALL select its matching asset while each figure retains one meaningful alternative text and one shared explanatory caption. Screenshot figures SHALL remain responsive on narrow and wide layouts.

#### Scenario: Visitor reads a documented workflow in light mode
- **WHEN** a visitor views a selected workflow page while the guide has its light theme active
- **THEN** the page presents the corresponding light Glovelly UI screenshot with its meaningful alternative text and shared caption

#### Scenario: Visitor changes the guide to dark mode
- **WHEN** a visitor views a selected workflow page while the guide has its dark theme active
- **THEN** the page presents the corresponding actual-dark Glovelly UI screenshot without duplicating the figure's explanatory content

#### Scenario: Visitor reads the guide on a narrow screen
- **WHEN** a visitor opens a page containing a product screenshot on a narrow viewport
- **THEN** the visible screenshot scales within the guide content without horizontal overflow or loss of its caption
