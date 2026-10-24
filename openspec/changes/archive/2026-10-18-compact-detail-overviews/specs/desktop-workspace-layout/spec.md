## MODIFIED Requirements

### Requirement: Desktop record workspaces fit the viewport
At default text sizing in 1280×800 and 1440×900 CSS-pixel viewports, Gigs, Invoices, Clients, and admin Users SHALL fit their shared chrome, workspace, and footer within the viewport without vertical document scrolling. Workspace height SHALL be independent of record count, selected detail length, and editor visibility. Layout SHALL allocate normal working space for standard record details inline, aiming for list-only scrolling during ordinary browsing without requiring it for every record. Anomalous detail content SHALL scroll in the middle detail region, while headers and applicable bottom controls remain available. Editors and collection/task dialogs SHALL retain independent scrolling. Exceptional viewport/text conditions SHALL favor reachable content over forced containment.

#### Scenario: Large record sets do not enlarge the page
- **WHEN** any supported desktop record workspace displays more records than fit in its list pane
- **THEN** its record list scrolls internally and the desktop document does not grow vertically
- **AND** the final record can be reached through list scrolling

#### Scenario: Long detail and open editor remain bounded
- **WHEN** a user selects a record with long details and opens its available editor at a supported desktop viewport
- **THEN** full standard details remain available inline through detail-region scrolling as necessary
- **AND** the editor can scroll independently without enlarging the document or the record list

#### Scenario: Long details remain independently reachable
- **WHEN** a supported desktop workspace displays an exceptionally long record with editors and collection dialogs closed
- **THEN** the detail body scrolls without enlarging the document or hiding standard fields in a separate dialog
- **AND** scrolling either records or details does not scroll the other region or document
