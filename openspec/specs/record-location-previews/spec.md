## Purpose

Provide supplementary, unverified location previews and independent Maps navigation for saved client and gig locations while preserving record visibility, provider attribution, privacy, and bounded signed-image delivery.

## Requirements

### Requirement: Location previews are lightweight unverified searches
Eligible Clients and Gigs panes SHALL automatically display non-interactive Google Maps Static location-query previews derived respectively from saved billing-address fields or saved venue/location text when deployment/signing configuration is available. Accessible image/link text and hover/focus help SHALL identify the image as an unverified map search, without a visible ready-image caption occupying layout height. No coordinates, place identities, or geocoding results SHALL be persisted. Missing or unsupported queries SHALL omit the image; provider-returned imagery SHALL NOT imply an application-verified match.

#### Scenario: Client address and gig location have different sources
- **WHEN** a client or gig preview is requested
- **THEN** the corresponding saved billing address or gig venue is used
- **AND** a missing gig venue is not silently replaced with the client's address

#### Scenario: Location text is ambiguous
- **WHEN** a saved venue name could identify multiple places
- **THEN** the image's accessible description and hover/focus help identify it as an unverified search and the original location remains readable
- **AND** the application does not claim confidence or a confirmed venue pin

### Requirement: Maps navigation does not depend on imagery
For a usable saved location query, the full billing-address or gig-location text SHALL be a keyboard-accessible HTTPS link using the encoded Google Maps search query. A ready map image SHALL also link to that same search, with an accessible action name and safe new-tab behavior. The textual link SHALL remain available when imagery is disabled, hidden or unavailable. No separate Open in Google Maps navigation line SHALL consume space. Rendering either link itself SHALL NOT contact Google; activation SHALL NOT modify the record or preview configuration.

#### Scenario: Activating image or location text
- **WHEN** a user activates the ready map or the saved address/location link by pointer or keyboard
- **THEN** both targets open the same Google Maps search safely in a new tab
- **AND** the written location remains an independent navigation target when no image is shown

#### Scenario: Maps image is not configured
- **WHEN** a saved location exists but map-image configuration is disabled or unavailable
- **THEN** record details and the external Maps link remain usable without repeated provider calls or large error panels

### Requirement: Automatic previews require no per-record approval
Clients and Gigs SHALL have no map-approval checkbox, persisted approval state, approval API intent, or source-change consent reset. Existing, new, imported and copied records SHALL use the same saved-location eligibility rules. The application SHALL NOT classify locations as public/private or claim verified residential-address detection. Preview reads SHALL NOT mutate records or require edit permission beyond the existing visibility authorization.

#### Scenario: A visible saved record is selected
- **WHEN** deployment/signing configuration is available and a selected record has a usable saved location and eligible pane space
- **THEN** its map preview loads automatically without a per-record activation step or record mutation

#### Scenario: A saved location changes
- **WHEN** the selected record's saved normalized location changes
- **THEN** obsolete imagery is removed and the new source becomes eligible automatically
- **AND** no approval/reset write or additional confirmation is required

#### Scenario: Location text is only being edited
- **WHEN** unsaved location text is entered in an editor
- **THEN** it is not used for a preview request

### Requirement: Backend preview reads enforce record visibility and source identity
Signed image URLs SHALL be issued through authenticated record-scoped backend endpoints. The backend SHALL enforce existing owner visibility, derive the query from the authorized saved record, restrict sizes and destinations, and reject an obsolete expected source fingerprint before signing. Descriptor and image completion guards SHALL bind display to the session, record type/ID, source identity and size bucket.

#### Scenario: Another user's record is requested
- **WHEN** a caller requests a map for a record outside their visibility
- **THEN** no signed URL or location data is disclosed

#### Scenario: Address changes during loading
- **WHEN** the saved source no longer matches the requested fingerprint or the selected source changes before completion
- **THEN** the obsolete request cannot populate the current image slot
- **AND** the backend does not issue a signed URL for a known obsolete source

#### Scenario: Caller supplies arbitrary provider parameters
- **WHEN** a caller requests an unsupported size or attempts to select an arbitrary query/destination
- **THEN** the request cannot produce an arbitrary signed provider URL

### Requirement: Google serves image bytes directly
The backend SHALL return a private, non-storeable signed-URL descriptor, not image bytes. The browser SHALL load the image directly from the fixed Google Maps Static HTTPS endpoint using an img source and an origin-only referrer policy compatible with website restrictions. Glovelly SHALL NOT proxy/rehost image bytes, convert map images into blob/data URLs, or persist/cache maps or signed URLs for offline/shared use. Obsolete images SHALL be removed; the application SHALL NOT promise cancellation of requests already dispatched to Google.

#### Scenario: A signed descriptor is received
- **WHEN** the descriptor matches the current selected source and eligible pane
- **THEN** the browser uses the signed Google URL directly as the image source
- **AND** no Glovelly image-byte endpoint or blob URL is used

### Requirement: Maps delivery protects credentials and bounds provider usage
Maps Static configuration SHALL default disabled. URL issuance SHALL require a valid API key and signing secret and SHALL sign the exact final encoded path/query server-side. The restricted API key and generated signature MAY appear in the browser image URL; the signing secret SHALL NOT appear in frontend code, responses, URLs or logs. URL/query lengths, image size buckets and per-user descriptor issuance SHALL be bounded. Hidden/ineligible previews SHALL NOT initiate descriptor or image requests, and loading failures/resizing SHALL NOT cause uncontrolled retries. Loading SHALL have a bounded client watchdog. Signatures SHALL NOT be represented as encrypted, expiring or user-bound URLs, or as preventing replay; provider usage SHALL be controlled by Google-side restrictions and quotas as well as issuance limits.

#### Scenario: Signing configuration is absent or invalid
- **WHEN** the API key or signing secret is missing/invalid, or imagery is disabled
- **THEN** no unsigned fallback URL is issued and record workflows remain usable

#### Scenario: Provider fails or a usage limit is reached
- **WHEN** a map request times out, fails, or is rate-limited
- **THEN** the image falls back quietly while textual details and the Maps link remain available
- **AND** automatic retry loops or failures of the core record workflow do not occur

#### Scenario: Record browsing crosses a hidden pane boundary
- **WHEN** the preview rail is hidden or too small
- **THEN** selection and resizing do not cause invisible map requests

### Requirement: Maps attribution and privacy are preserved
Displayed images SHALL preserve Google/provider attribution and SHALL NOT crop, obscure, or filter it away. Production enablement SHALL satisfy current API/billing/security/delivery requirements and resolve the pending provider privacy clarification and applicable browser-storage/consent requirements. Published disclosures SHALL describe automatic location queries and direct browser delivery, including browser network/request information, without claiming anonymous or cookie-free requests. Provider URLs SHALL contain only location text and necessary rendering/authentication parameters, not appended client/contact identity, internal record IDs or fingerprints. No per-record public-address declaration SHALL be represented as a documented provider requirement.

#### Scenario: A map image is resized for a portrait rail
- **WHEN** the static image is displayed in the side column
- **THEN** the full image and required attribution remain visible, without an interactive map or scroll-capturing viewer

#### Scenario: Maps rollout lacks required prerequisites
- **WHEN** provider setup, policy review, or required privacy/activation decisions remain unresolved
- **THEN** automatic image loading remains disabled while the core workspace and external link remain usable
