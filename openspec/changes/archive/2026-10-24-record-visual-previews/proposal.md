## Why

The compact record panes now make standard details accessible but can still feel visually sparse. Useful previews of the actual invoice document and saved client/gig locations can add context without introducing decorative filler, hiding standard fields, or embedding miniature interactive applications.

## What Changes

- Add a consistent visual column beside content-sized standard-details columns in Invoices, Gigs, and Clients when space permits. Spanning notes wrap within the combined facts width; maps sit alongside with a deliberate gap rather than a fixed proportional facts allocation. Keep headers and existing bottom docks intact.
- Render a clickable first-page invoice thumbnail locally on canvas from existing authenticated PDF bytes. Do not generate, regenerate, repair, issue, or otherwise mutate invoices merely because a record is viewed.
- Reuse the existing full-PDF preview action and readiness rules. Download/render thumbnails per mounted eligible invoice without a reusable cache; recheck metadata after download and clean up obsolete work. Accept the small remaining concurrent-replacement race rather than changing PDF storage.
- Automatically display non-interactive Google Maps Static API location-query previews for saved client billing addresses and gig venues when deployment configuration and pane space permit. Make the image and full saved address/location text links to the same Google Maps search, without a separate navigation line. Convey the unverified-search meaning through accessible text and hover/focus help rather than a visible caption. No per-record approval, public/private address classification, or location-edit consent state is required.
- Centre visuals in their secondary column and fit them within the measured middle-region width and height. Remove ready-preview captions, preserve full map attribution and PDF proportions, and prevent visuals alone from introducing scrolling. Invoice thumbnails use available space rather than small fixed-height display caps; activation still opens full preview.
- Generate signed image URLs through authenticated, owner-scoped backend endpoints. The browser loads images directly from Google; Glovelly does not proxy, store, or rehost image bytes. Use restricted API credentials, server-only signing secrets, bounded URL issuance, preserved attribution, and accurate privacy disclosures.
- Omit visuals when the pane is too narrow or short, including editor-open layouts; do not stack landscape strips, squeeze standard details, or request invisible previews.
- Keep Admin unchanged. No saved coordinates, place selection, verified geocoding, directions UI, map interaction, or header tips in this change.

## Capabilities

### New Capabilities
- `invoice-pdf-thumbnails`: Read-only, uncached local first-page PDF canvas previews with best-effort metadata checks, obsolete-result guards and full-preview navigation.
- `record-location-previews`: Automatic Google static location-query images and independent explicit Maps links, with authenticated source-bound signed URLs, direct Google delivery, provider/privacy controls, and graceful fallback.

### Modified Capabilities
- `compact-record-overviews`: Add optional pane-responsive side-by-side visuals without displacing full inline standard details or existing task/collection controls.

## Impact

- Frontend workspace sections, record overview CSS/components, a lazy PDF.js renderer/worker dependency, bounded per-thumbnail download/render lifecycle, and frontend package/lock files.
- Backend signed-URL endpoints under existing `/clients` and `/gigs` prefixes, signing service/settings/DI, private non-storeable URL responses, rate limiting, and tests. Client/Gig contracts carry source identity, not approval state; normal writes require no map-specific approval handling. Existing invoice metadata/PDF reads remain unchanged and provide the thumbnail's data.
- New optional Google Maps Static configuration and secrets, distinct from Google Routes mileage settings. Update appsettings, staging and production workflow configuration, README, relevant engineering docs, and agent operating guidance where needed; never commit secret values.
- Review and update privacy/terms/provider wording and dates as applicable: eligible automatic previews send location text and browser network/request information to Google. Do not append client names, contact details, or application record identifiers to queries. The billing account is UK, outside the EEA-specific account regime; European end-user restrictions can still apply. Provider clarification of ordinary address-query privacy requirements remains a production-rollout check, not an assumed requirement for per-record approval.
- Unit/integration tests, Ubuntu browser UAT, user guide, and real staging light/dark capture review. The preceding layout changes' browser reviews were waived, not passed; this change must validate its own geometry.
- No persistent map approval, coordinates, geocoding results, or reusable thumbnail/PDF-byte cache is required. Remove approval schema safely according to deployment state. No document upload to a third-party renderer, PDF storage redesign, or new generation workflow.
