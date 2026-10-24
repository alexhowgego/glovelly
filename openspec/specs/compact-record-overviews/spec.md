## Purpose

Provide complete inline record details in compact workspace panes while preserving readable long content and existing actions and editors.

## Requirements

### Requirement: Workspace-specific compact overviews
Clients, Gigs, Invoices, and Admin Users SHALL show standard record details directly in the selected-record pane, using the available space rather than permanent read-only detail dialogs. Layout SHALL target fitting normal records with editors closed or open, while unusually long records MAY scroll in the middle detail region. Labels and values SHALL be left-aligned in a structured facts layout; the group SHALL use the pane's middle space, with a top-anchored header and a bottom dock only for relevant collection/task controls. Empty selection and constrained viewports SHALL remain accessible.

#### Scenario: Browsing a gig with extensive associated content
- **WHEN** a user selects a gig containing long notes and many resources and expenses
- **THEN** the pane shows complete booking facts and notes inline, with scrolling available for exceptionally long notes
- **AND** resources and expenses remain separate dialogs, with their counts/totals shown in docked controls

#### Scenario: Client and Admin overviews preserve operational facts
- **WHEN** a user selects a client or Admin user
- **THEN** the pane shows complete contact/billing or role/access/enrolment/account metadata respectively
- **AND** reading a full address or account reference does not require opening a redundant read-only dialog
- **AND** existing monthly invoicing, settings, and access-editing actions remain reachable

#### Scenario: Invoice overview retains workflow essentials
- **WHEN** a user selects an invoice
- **THEN** client, status, dates, total, document readiness, full description, and issue/delivery metadata are available inline
- **AND** long values remain readable through detail-region scrolling without a separate read-only workflow dialog

### Requirement: Long standard details remain readable inline
Variable-length names, notes, descriptions, locations, addresses, and metadata SHALL remain readable in full without expanding the desktop document indefinitely. The middle detail region SHALL support scrolling for anomalous content; text SHALL NOT be silently clamped or reduced to tooltip-only access. Changing the selected record SHALL reset that region's scroll position. Compact layout SHALL NOT make critical workflow state or warnings inaccessible.

#### Scenario: Long text is fully readable on demand
- **WHEN** record text exceeds the usual amount of available detail space
- **THEN** the user can scroll to read the complete content without editing it or losing selection

#### Scenario: Workflow prerequisites need attention
- **WHEN** seller-profile configuration or document readiness prevents an invoice operation
- **THEN** the overview conveys the blocking state and provides access to the existing explanation or corrective action

### Requirement: Existing actions and editors remain authoritative
The overview SHALL preserve all existing record actions, restrictions, and confirmation behavior. Less frequent actions MAY be grouped in an accessible menu but SHALL remain discoverable and keyboard-operable. Record editing, invoice line items, adjustments, and invoice description editing SHALL continue in the existing independently scrollable editor areas.

#### Scenario: Invoice lines remain in the editor
- **WHEN** the user chooses Line items
- **THEN** the existing line-items editor opens with its current editing and gig-link navigation behavior intact
- **AND** the overview remains fitted at supported desktop sizes

#### Scenario: Compact actions retain eligibility rules
- **WHEN** an invoice is not eligible for download, delivery, or deletion, or a gig cannot be deleted
- **THEN** the corresponding action retains its existing restriction and explanation regardless of its compact placement

### Requirement: Optional visuals occupy a secondary side column
Invoices, Clients, and Gigs SHALL use a consistent side-by-side facts/visual composition when the actual middle pane provides enough width and height for readable facts and a useful visual. The visual SHALL be secondary to the full inline standard details, with existing headers, actions, and bottom docks preserved. PDF thumbnails SHALL preserve page proportions and maps SHALL use an appropriately sized rectangle without a mandatory landscape shape. Admin SHALL remain unchanged.

#### Scenario: Wide record pane has spare space
- **WHEN** the selected record has eligible visual content and the middle pane satisfies the minimum facts/visual dimensions
- **THEN** the preview appears beside, not below, the standard details
- **AND** full text and existing task/collection controls remain available

### Requirement: Preview eligibility follows pane size and visibility
Visual eligibility SHALL depend on actual pane dimensions and visibility, not solely browser width. When an editor, narrow layout, short pane, or unavailable visual makes the rail unhelpful, the application SHALL omit it and restore full-width facts rather than squeeze columns or stack a landscape strip. The same eligibility SHALL govern passive fetching/rendering so hidden previews do not consume work.

#### Scenario: Opening an editor narrows the details pane
- **WHEN** opening the existing editor reduces space below useful facts/visual minimums
- **THEN** the visual rail is removed, obsolete passive work is cancelled or ignored, and standard details retain their space
- **AND** existing Preview or Open in Google Maps actions remain available where applicable

#### Scenario: Mobile and short windows retain usable details
- **WHEN** the pane is too narrow or short for a useful secondary column
- **THEN** the record uses its facts-first layout with its existing scrolling/accessibility behavior and no automatic landscape preview strip

#### Scenario: Location imagery is unavailable
- **WHEN** map configuration or a usable saved location is unavailable, or image loading fails
- **THEN** facts use the available width without a blank image or consent rail
- **AND** the external Maps link remains available for a usable saved location without an approval control in the editor

### Requirement: Supplementary visuals fit the measured middle region
Ready visuals SHALL be constrained by both the actual middle-region height and column width. PDF thumbnails SHALL be centred in their visual column; maps SHALL be left-aligned beside content-sized facts with a consistent gap. They SHALL NOT introduce scrolling solely through image height or captions. The full map image and attribution SHALL remain visible without clipping or cover-cropping; PDF page proportions SHALL be preserved. Invoice display size SHALL use available space rather than a small fixed-height cap. Ready-preview help SHALL be out-of-flow hover/focus text with accessible descriptions, not permanent captions. Long ordinary details MAY still require their existing middle-region scrolling.

#### Scenario: A tall map shares a short eligible pane
- **WHEN** a portrait map is loaded beside facts that otherwise fit in the middle region
- **THEN** the entire image fits its height budget without adding a scrollbar or hiding attribution
- **AND** its caption does not take a separate row

#### Scenario: Pane size changes while a PDF thumbnail is ready
- **WHEN** an eligible pane is resized without changing invoice metadata
- **THEN** the thumbnail display adapts to available width/height and stays centred
- **AND** resizing its display alone does not redownload the PDF

#### Scenario: Portrait invoice preview shares available width with facts
- **WHEN** an eligible invoice thumbnail appears beside its two facts columns
- **THEN** its dedicated layout shares available width evenly across the two facts tracks and the preview track, rather than assigning spare width only to the preview
- **AND** the PDF remains centred and proportional without changes to loading, reads or map-workspace layouts

#### Scenario: Ordinary facts establish the notes width
- **WHEN** an eligible record pane displays two facts columns beside a visual
- **THEN** ordinary fact content sizes those columns instead of a fixed proportional split
- **AND** Notes spans both facts columns and wraps within their combined width without widening them
- **AND** the whole map is aligned beside the facts with a consistent gap, without a centring gutter or changes to passive-preview loading

#### Scenario: Client contact and address facts share one column
- **WHEN** client contact details and billing address are displayed
- **THEN** Email, City, Country and Billing address stack in one content-sized facts column, with an eligible map using the remaining horizontal space
- **AND** billing-address content contributes to that column's width rather than receiving spanning-prose size containment
- **AND** saved address line breaks and readable wrapping remain intact with a map, without imagery, and on mobile

#### Scenario: Map fills its available frame
- **WHEN** an eligible map frame has been measured
- **THEN** its requested shape fills the available frame within pixel rounding without stretching or cover-cropping
- **AND** facts have roomier column spacing and map corners are rounded without obscuring attribution
- **AND** initial measurement does not debounce, delay or remount the shared overview or invoice thumbnails
- **AND** subsequent map-frame resizes are coalesced and subject to bounded sizes and existing issuance limits
