## ADDED Requirements

### Requirement: Compact authenticated application chrome
The authenticated application SHALL provide compact branding, primary navigation, capture access, account controls, and the shared summary strip. Active navigation and headings within the workspace SHALL identify the current section without a duplicate shell workspace title. It SHALL NOT reserve separate large introductory areas for the slogan, repeated workspace descriptions, mascot introduction, or an Admin banner duplicating the summary strip. Navigation and controls SHALL retain accessible names and keyboard operation.

#### Scenario: Changing sections retains shared controls
- **WHEN** a user navigates between authenticated sections
- **THEN** navigation, capture, account access, and the summary strip remain available in the compact shared header
- **AND** the active section remains identifiable without repeated introductory content, a duplicate shell workspace title, or an Admin banner

### Requirement: Desktop record workspaces fit the viewport
At default text sizing in 1280×800 and 1440×900 CSS-pixel viewports, Gigs, Invoices, Clients, and admin Users SHALL fit their shared chrome, workspace, and footer within the viewport without vertical document scrolling. Workspace height SHALL be independent of record count, selected detail length, and editor visibility.

#### Scenario: Large record sets do not enlarge the page
- **WHEN** any supported desktop record workspace displays more records than fit in its list pane
- **THEN** its record list scrolls internally and the desktop document does not grow vertically
- **AND** the final record can be reached through list scrolling

#### Scenario: Long detail and open editor remain bounded
- **WHEN** a user selects a record with long details and opens its available editor at a supported desktop viewport
- **THEN** detail and editor content are independently reachable by pane scrolling without enlarging the document or the record list

### Requirement: List controls survive empty and filtered states
The record pane SHALL allocate space for its controls independently of the detail pane. Search, filter, sort, and applicable list actions SHALL remain reachable with no records, no selected detail, or no matching results. Scrolling records SHALL NOT scroll their search, filter, or primary action controls out of the pane; column headings SHALL remain visible while records scroll.

#### Scenario: New user has no records
- **WHEN** a supported record workspace contains no records and displays an empty detail state
- **THEN** the list controls and empty state remain reachable and untruncated

#### Scenario: Filters exclude every record
- **WHEN** a user applies filters or search that produce no results
- **THEN** the controls remain reachable so the user can restore the result set
- **AND** the workspace does not collapse to the detail empty-state height

### Requirement: Responsive layouts preserve access over containment
Stacked-width layouts, short-height windows, enlarged-text conditions that cannot accommodate controls, and document-like settings views SHALL permit natural document scrolling rather than clipping content to enforce viewport containment. Mobile SHALL retain its stacked workflow. Keyboard focus and explicit navigation SHALL reveal their target within the appropriate scroll region.

#### Scenario: Narrow or short viewport uses natural flow
- **WHEN** the application is viewed at 390×844 or 1280×600 CSS pixels
- **THEN** page scrolling can reach all workspace controls and content without clipped panes or horizontal document overflow

#### Scenario: Zoom does not strand controls
- **WHEN** the user views the application at 200 percent browser zoom
- **THEN** navigation, summary information, list controls, and editor actions remain reachable by keyboard and scrolling

#### Scenario: Explicit navigation reveals a gig in a bounded workspace
- **WHEN** capture completion or an invoice-line link explicitly navigates to a gig
- **THEN** the existing gig visibility and selection rules apply and its overview is revealed in the appropriate scroll container
- **AND** the desktop workspace remains bounded at supported viewport sizes
