## MODIFIED Requirements

### Requirement: Thumbnail renders the existing current PDF locally
When an eligible invoice visual rail is visible, the application SHALL render only page one of the existing current invoice PDF locally into a canvas using authenticated stored PDF bytes. It SHALL preserve page proportions and SHALL NOT embed a miniature PDF viewer or send document bytes to an external renderer. Passive selection, loading, rendering, and preview retries SHALL NOT generate, regenerate, repair, issue/reissue, deliver, or otherwise mutate the invoice. During a same-invoice update, the previous displayed pixels MAY remain solely as a blurred, clearly labelled, non-interactive updating placeholder.

#### Scenario: Viewing a current invoice
- **WHEN** the selected invoice has a current readable PDF and its visual rail is eligible
- **THEN** page one is rendered from the existing authenticated PDF read endpoint
- **AND** no PDF-generation or invoice-mutation endpoint is called

#### Scenario: Document is unavailable
- **WHEN** the selected invoice PDF is missing, stale, regenerating, or failed
- **THEN** no known outdated thumbnail is presented as current and no automatic generation/repair is attempted
- **AND** existing document-state explanations and explicit recovery actions remain available

### Requirement: PDF loading is bounded and uncached
Passive PDF loading/rendering SHALL be gated on actual visual eligibility and selection. Bytes SHALL belong only to the active thumbnail render, without a reusable browser/server byte or thumbnail cache or modal byte sharing. Selection, known revision/readiness changes, session changes and obsolete completions SHALL NOT populate the wrong invoice or retain known outdated imagery as current. Renderer tasks/documents and staging canvas memory SHALL be released on cleanup. Only the last displayed pixels of the same mounted selected invoice MAY persist during an update placeholder. Ordinary component renders SHALL NOT repeat the download; revisiting/remounting an invoice MAY download/render it again. Existing PDF storage and generation SHALL remain unchanged.

#### Scenario: Selection changes before rendering completes
- **WHEN** invoice A's load/render is pending and invoice B is selected
- **THEN** A's completion cannot populate B's canvas or preview state
- **AND** obsolete rendering work is cancelled or ignored safely

#### Scenario: Current revision changes
- **WHEN** a selected invoice's document revision changes or becomes non-current
- **THEN** the old image immediately becomes disabled and visibly blurred/labelled as updating rather than current
- **AND** only a completed render for the new validated document identity can become interactive

#### Scenario: Revisiting an invoice
- **WHEN** an invoice thumbnail is unmounted and later selected again in an eligible pane
- **THEN** the thumbnail obtains fresh bytes rather than reusing a retained cache entry

## ADDED Requirements

### Requirement: Preview replacement preserves layout
The eligible selected invoice preview SHALL retain a stable paper-shaped frame through loading, mutation, replacement, and failure. Updating SHALL show an accessible status overlay, without replacing the paper frame with a short text paragraph or collapsing the facts/preview grid. Previous pixels SHALL be removed on failure, invoice/session change, or unmount. Motion SHALL respect reduced-motion preferences.

#### Scenario: Same invoice is regenerating
- **WHEN** a user changes an invoice state and a replacement is pending
- **THEN** the old rendered pixels MAY remain blurred and non-interactive under Updating preview while the frame dimensions remain stable

#### Scenario: Replacement fails
- **WHEN** the fresh thumbnail cannot be loaded or rendered
- **THEN** the previous image SHALL be removed and the error SHALL occupy the existing frame without an automatic retry loop
