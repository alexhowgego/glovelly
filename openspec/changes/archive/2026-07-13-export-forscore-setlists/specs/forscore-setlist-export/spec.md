## ADDED Requirements

### Requirement: Export Active Gig Set List as forScore File
The system SHALL allow an authenticated user to export a gig's active reviewed set list as a forScore-compatible `.4ss` file when every included song row is mapped to a selected forScore chart.

#### Scenario: Successful export downloads a 4SS file
- **WHEN** an authenticated user exports a visible gig whose active set list has included song rows and every included song row has a selected forScore chart
- **THEN** the system SHALL return a downloadable `.4ss` file using the forScore setlist XML root with `kind="setlist"` and `version="1.0"`

#### Scenario: Export requires visible gig
- **WHEN** an authenticated user exports a gig that is not visible to them
- **THEN** the system SHALL reject the export without disclosing set list contents

#### Scenario: Export requires active set list
- **WHEN** an authenticated user exports a visible gig with no active saved set list
- **THEN** the system SHALL reject the export and indicate that no active set list is available

### Requirement: Export Preserves Reviewed Set List Ordering
The system SHALL include only included song rows in the exported `.4ss` file and SHALL preserve their saved set list order.

#### Scenario: Included songs are exported in sort order
- **WHEN** an active set list contains included song rows, excluded song rows, separator rows, and comment rows
- **THEN** the exported `.4ss` file SHALL contain `<score>` entries only for included song rows ordered by their saved sort order

#### Scenario: Chart paths are preserved exactly
- **WHEN** an included song row is exported
- **THEN** the corresponding `<score>` entry SHALL use the selected forScore chart file path exactly as stored for that row

### Requirement: Export Blocks Unmapped Included Songs
The system SHALL prevent `.4ss` export while any included song row has no selected forScore chart or no selected forScore chart file path.

#### Scenario: Backend reports unmapped rows
- **WHEN** an authenticated user exports an active set list with one or more included song rows that lack a selected forScore chart
- **THEN** the system SHALL reject the export and identify the rows that still need chart selection

#### Scenario: Frontend enables export only when complete
- **WHEN** the reviewed set list modal displays an active set list
- **THEN** the export action SHALL be enabled only when there is at least one included song row and every included song row has a selected forScore chart

### Requirement: Export Produces Valid XML
The system SHALL generate well-formed UTF-8 XML that safely represents set list titles, chart titles, and chart paths containing XML-sensitive characters.

#### Scenario: XML-sensitive values are escaped
- **WHEN** a gig title, chart title, or chart path contains characters such as ampersands, quotes, apostrophes, or angle brackets
- **THEN** the exported `.4ss` file SHALL remain well-formed XML and preserve the intended text values when parsed
