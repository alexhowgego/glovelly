## Purpose

Provide supplementary, accessible invoice thumbnails from existing stored PDFs without changing invoice workflows, retaining reusable document bytes, or presenting known outdated imagery.

## Requirements

### Requirement: Thumbnail renders the existing current PDF locally
When an eligible invoice visual rail is visible, the application SHALL render only page one of the existing current invoice PDF locally into a canvas using authenticated stored PDF bytes. It SHALL preserve page proportions and SHALL NOT embed a miniature PDF viewer or send document bytes to an external renderer. Passive selection, loading, rendering, and preview retries SHALL NOT generate, regenerate, repair, issue/reissue, deliver, or otherwise mutate the invoice.

#### Scenario: Viewing a current invoice
- **WHEN** the selected invoice has a current readable PDF and its visual rail is eligible
- **THEN** page one is rendered from the existing authenticated PDF read endpoint
- **AND** no PDF-generation or invoice-mutation endpoint is called

#### Scenario: Document is unavailable
- **WHEN** the selected invoice PDF is missing, stale, regenerating, or failed
- **THEN** no known outdated thumbnail is presented as current and no automatic generation/repair is attempted
- **AND** existing document-state explanations and explicit recovery actions remain available

### Requirement: Thumbnail navigation preserves the full-preview workflow
The thumbnail SHALL be a keyboard-operable affordance with an accessible invoice-specific name that opens the existing full-PDF preview workflow. Page-one/action help SHALL appear on hover/focus without a visible ready-thumbnail caption consuming layout height. It SHALL be supplementary to readable invoice facts and existing Preview controls. A thumbnail-specific fetch/render/size failure SHALL NOT disable an otherwise valid explicit preview or other record actions.

#### Scenario: Opening the document from the thumbnail
- **WHEN** the user activates a ready thumbnail by pointer or keyboard
- **THEN** the existing full-preview workflow opens for that invoice under its normal readiness rules
- **AND** no invoice is regenerated as part of opening it

#### Scenario: Local thumbnail rendering fails
- **WHEN** existing PDF bytes cannot be rendered as a thumbnail or exceed the thumbnail resource limits
- **THEN** the application shows a quiet inline unavailable state and retains applicable explicit document actions

### Requirement: PDF loading is bounded and uncached
Passive PDF loading/rendering SHALL be gated on actual visual eligibility and selection. Bytes SHALL belong only to the active thumbnail render, without a reusable browser/server byte or thumbnail cache or modal byte sharing. Selection, known revision/readiness changes, session changes and obsolete completions SHALL NOT populate the wrong invoice or retain known outdated imagery. Renderer tasks/documents and canvas memory SHALL be released on cleanup. Ordinary component renders SHALL NOT repeat the download; revisiting/remounting an invoice MAY download/render it again. Existing PDF storage and generation SHALL remain unchanged.

#### Scenario: Selection changes before rendering completes
- **WHEN** invoice A's load/render is pending and invoice B is selected
- **THEN** A's completion cannot populate B's canvas or preview state
- **AND** obsolete rendering work is cancelled or ignored safely

#### Scenario: Current revision changes
- **WHEN** a selected invoice's document revision changes or becomes non-current
- **THEN** the old thumbnail is cleared immediately and only the new current identity can become visible

#### Scenario: Revisiting an invoice
- **WHEN** an invoice thumbnail is unmounted and later selected again in an eligible pane
- **THEN** the thumbnail obtains fresh bytes rather than reusing a retained cache entry

### Requirement: Thumbnail freshness uses best-effort metadata revalidation
After downloading the stored PDF, the frontend SHALL re-read authorized invoice metadata and compare invoice ID, document state/revision, PDF revision and generation identity with the selected document. Known changes or unavailable states SHALL discard the bytes with a quiet refresh/retry explanation, not an automatic retry loop. Metadata checks SHALL NOT be represented as immutable byte-to-revision guarantees: a residual concurrent-replacement race is accepted for supplemental imagery and is corrected on refresh, revisit or metadata-driven reload. No inconsistent result SHALL be retained in a reusable cache.

#### Scenario: Metadata changes during the download
- **WHEN** the post-download read reports changed or non-current document metadata
- **THEN** the downloaded bytes are not rendered as the selected current document
- **AND** explicit invoice actions remain available without an automatic generation or retry loop

#### Scenario: Opening the full preview after seeing a thumbnail
- **WHEN** the user activates full preview
- **THEN** the existing workflow performs its own authoritative PDF read rather than reusing thumbnail bytes

#### Scenario: Preview is hidden or session ends
- **WHEN** pane dimensions make the rail ineligible or the session ends
- **THEN** passive rendering stops and hidden-only work is cancelled
- **AND** session end releases document bytes and visual resources
