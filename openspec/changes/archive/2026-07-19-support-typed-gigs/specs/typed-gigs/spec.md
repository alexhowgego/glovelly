## ADDED Requirements

### Requirement: Gigs have a required type
The system SHALL assign every gig one of the supported gig types: `Performance`, `Teaching`, `Rehearsal`, `Recording`, `Admin`, or `Other`.

#### Scenario: Existing gigs are backfilled
- **WHEN** the post-baseline database migration is applied to a database with existing gigs
- **THEN** every existing gig has type `Performance`

#### Scenario: New gig type is validated
- **WHEN** a user-facing gig create or update request supplies an unsupported type
- **THEN** the system rejects the request with validation feedback for the type field

#### Scenario: Type remains separate from lifecycle status
- **WHEN** a gig is created or edited
- **THEN** the system stores the gig type independently from the gig status values `Draft`, `Confirmed`, `Completed`, and `Cancelled`

### Requirement: Users can create and edit typed gigs
The system SHALL let users choose and edit gig type through the primary gig creation and editing workflow.

#### Scenario: Create typed gig
- **WHEN** a user creates a gig for teaching, rehearsal, recording, admin, performance, or other work
- **THEN** the saved gig uses the selected type and remains available in the normal gig workspace

#### Scenario: Edit gig type
- **WHEN** a user edits an existing gig and changes its type
- **THEN** the saved gig reflects the new type without changing its status, invoice link, expenses, mileage, notes, or external resources

#### Scenario: Legacy inferred creation defaults type
- **WHEN** a quick-capture, calendar/import, MCP draft, seed, or other inferred creation path cannot determine a type explicitly
- **THEN** the system assigns `Performance` as the default type

### Requirement: Gig presentation and filtering are type-aware
The system SHALL display gig type in gig lists, gig details, and relevant selection surfaces, and SHALL let users filter gigs by type.

#### Scenario: Gig list shows type
- **WHEN** a user views the gig workspace list
- **THEN** each gig row or card clearly shows its type alongside the existing gig summary information

#### Scenario: Gig detail shows type
- **WHEN** a user selects a gig in the gig workspace
- **THEN** the detail panel clearly shows the gig type

#### Scenario: User filters by type
- **WHEN** a user applies a gig type filter
- **THEN** the gig list includes only gigs matching the selected type while preserving existing search, status filter, and sort behavior

#### Scenario: Search includes type
- **WHEN** a user searches the gig list by a type label such as `teaching` or `recording`
- **THEN** matching gigs of that type are included in the search results

### Requirement: Generic gig language is neutral
The system SHALL avoid generic UI wording that implies every gig is a performance while keeping `Gig` as the primary entity name.

#### Scenario: Generic location labels are neutral
- **WHEN** a user creates, edits, imports, or views a generic gig
- **THEN** the UI uses neutral location wording rather than requiring the work to be a venue-based performance

#### Scenario: Performance-specific language remains contextual
- **WHEN** the UI describes a feature that genuinely applies to set lists, shows, or performance-specific material
- **THEN** the UI may use performance-specific language in that context

### Requirement: Import drafts carry editable gig type
The system SHALL store, display, and commit an editable proposed gig type for each staged gig import draft.

#### Scenario: Review imported draft type
- **WHEN** a user reviews a staged gig import draft
- **THEN** the draft shows an editable type field with one of the supported gig type values

#### Scenario: Commit imported draft type
- **WHEN** a user commits an accepted import draft
- **THEN** the created gig has the draft's selected type

#### Scenario: Imported draft defaults type
- **WHEN** an import draft is created without an explicit type
- **THEN** the draft type defaults to `Performance`

#### Scenario: Invalid draft type is rejected
- **WHEN** a user or MCP client updates or creates an import draft with an unsupported type
- **THEN** the system rejects the draft change with validation feedback

### Requirement: All gig types use shared financial workflows
The system SHALL allow every gig type to use existing fee, invoice, expense, mileage, receipt, tax-summary, notes, and external-resource behavior.

#### Scenario: Teaching gig is invoiced
- **WHEN** a teaching gig has a fee and a user generates an invoice from it
- **THEN** the system creates an invoice through the existing invoice workflow and links it to the teaching gig

#### Scenario: Admin gig expenses are counted
- **WHEN** an admin gig has expenses included in existing expense or tax-summary flows
- **THEN** the system includes those expenses according to the same rules used for performance gigs

#### Scenario: Non-chargeable gig remains supported
- **WHEN** any gig type has a zero fee
- **THEN** the gig can still be saved and can still carry expenses, mileage, notes, and resources

### Requirement: Invoice fee descriptions are type-aware
The system SHALL generate fee line descriptions from gig type, title, and date.

#### Scenario: Generate performance fee description
- **WHEN** a performance gig with a non-zero fee generates invoice lines
- **THEN** the fee line description uses the existing performance wording pattern

#### Scenario: Generate teaching fee description
- **WHEN** a teaching gig with a non-zero fee generates invoice lines
- **THEN** the fee line description describes a teaching fee for that gig

### Requirement: Set-list workflows remain available across gig types
The system SHALL keep set-list resources, set-list imports, chart matching, and set-list exports available regardless of gig type.

#### Scenario: Rehearsal gig uses set-list resource
- **WHEN** a rehearsal gig has a Google Sheet external resource with purpose `SetList`
- **THEN** the user can manage the set-list import through the existing set-list workflow

#### Scenario: Recording gig set-list is not rejected by type
- **WHEN** a recording gig calls an existing set-list import endpoint with otherwise valid input
- **THEN** the system does not reject the request solely because the gig type is not `Performance`

### Requirement: Calendar output remains unchanged by type
The system SHALL NOT add gig type to Google Calendar event titles or descriptions as part of typed gigs.

#### Scenario: Calendar event maps existing fields
- **WHEN** a typed gig is mapped to a Google Calendar event
- **THEN** the event title and description follow the existing calendar mapping without adding a type label

### Requirement: Typed gig schema changes use EF migrations
The system SHALL introduce gig type persistence through a checked-in EF Core migration in the migrations project.

#### Scenario: Migration is checked in
- **WHEN** the gig type model changes are implemented
- **THEN** the corresponding migration and model snapshot changes are stored under the EF migrations project

#### Scenario: PostgreSQL deployment applies migration through bundle
- **WHEN** the typed gig change is deployed to PostgreSQL environments
- **THEN** the existing migration bundle and Cloud Run Job workflow applies the migration before the application service revision is deployed
