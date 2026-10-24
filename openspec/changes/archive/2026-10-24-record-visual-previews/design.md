## Context

Standard details are now inline in the middle of bounded record panes, with headers and applicable bottom docks. This change adds useful visual context without reversing that information architecture. There are no existing map-display or PDF canvas dependencies in the authenticated frontend.

`GET /invoices/{id}/pdf` enforces `WhereVisibleTo` and calls `InvoicePdfService.OpenCurrentReadAsync`, which checks document state/revision and opens stored bytes without generating anything. `useInvoicePreview` currently owns a modal-specific blob URL; it is not a suitable owner for automatic background thumbnails. Google Routes mileage estimation is server-side and returns distance/duration, not saved geocoding or map images. Maps Static API is a separate service requiring API enablement, credentials, billing, and policy review.

## Goals / Non-Goals

**Goals:**
- Use a shared side-by-side details/visual composition for Invoices, Clients, and Gigs, with details retaining priority.
- Show page one of the existing current invoice PDF locally, without implicit document mutations.
- Show lightweight, explicitly unverified Google map search previews and clear external Maps links.
- Automatically show map imagery for eligible saved locations without per-record approval or public/private classification.
- Keep passive loading bounded, private, cancellation-safe, and optional when space or provider availability is insufficient.

**Non-Goals:**
- Interactive maps, directions, saved coordinates, place autocomplete/selection, verified locations, or route estimation changes.
- A miniature PDF viewer, page navigation, remote rendering service, new PDF-generation workflow, or stored thumbnails.
- Adding visuals to Admin, moving normal fields into dialogs, stacking landscape strips, or implementing tips from #302.

## Decisions

### 1. A secondary visual rail, controlled by pane space

Keep existing headers and docks. Within an eligible middle region, use two content-sized facts tracks and a flexible visual track, with 24px between fact columns and 28px before the visual. Share the facts tracks through CSS subgrid. Spanning fields such as Notes and Location use inline-size containment so prose wraps within the width established by ordinary facts rather than widening both tracks. Clients opts into a single content-sized facts column: Email, City, Country and Billing address stack vertically and the map takes the remaining width. Its billing address contributes intrinsic width, retaining address line breaks and normal wrapping. Without imagery, Clients retains its single-column stack while other workspaces retain the equal-width facts grid. Maps measure their own reserved frame to request a matching shape, using contain positioning and a 12px rounded frame; PDF thumbnails remain centred within their rail. Preserve proportions, full map attribution and stable shared-overview loading behavior.

Start with eligibility thresholds of 620 CSS pixels of middle-region width and 300 CSS pixels of usable height, preserving at least 360px for facts and 180px for the visual after gaps. Tune with browser evidence, not viewport width alone. Observe the actual middle region with a small shared resize/visibility hook so CSS layout and fetch eligibility agree. Use stable size buckets and modest hysteresis to avoid threshold oscillation or resize-driven requests. Opening an editor often hides the visual; do not move it below the facts as a landscape strip.

Observe the middle region's content height as well as eligibility. Reserve a small border/focus allowance and expose the remaining height as the visual rail's budget. Centre each visual in its own bounded column. Maps use contain sizing in both dimensions, with no clipped attribution; PDF display width is the lesser of column width and height budget multiplied by page aspect ratio. Ready images have no visible caption row. Hover/focus help is positioned out of flow and does not cover map attribution. A sufficiently resolved bounded canvas can scale with pane geometry without a PDF redownload or a small fixed-height display cap.

When ineligible, unmount/cancel passive work and give facts full width. Long ordinary facts can still scroll in their own region, but supplementary visuals must not be the sole source of overflow. The linked saved address/location remains available regardless of imagery; the invoice Preview action remains available without its thumbnail. Loading reserves only eligible rail space; terminal map failure removes the image slot rather than leaving a large error panel.

### 2. Local page-one PDF rendering

Invoices opts into a dedicated balanced layout while its thumbnail is eligible: three equal-width tracks share the available width across two facts columns and the portrait preview. This gives facts more breathing room without stretching the PDF or assigning disproportionate spare space to its rail. Keep existing proportional, centred canvas sizing and loading/read lifecycles. The Clients and Gigs map layouts are unchanged.

Add `pdfjs-dist` as a lazy dependency with a locally bundled matching worker, not a CDN. Fetch bytes through `fetchWithSession` from the existing endpoint. Render page one into a canvas sized for the rail and capped device-pixel ratio; preserve the page's proportions and white paper background across themes. Expose a keyboard-operable button labelled with the invoice identity that invokes the existing full-preview action. The canvas is supplementary, not the sole accessible source of invoice information.

Do not use an iframe/object PDF viewer for the rail: browser-dependent toolbars, scrolling, and focus behavior conflict with a thumbnail. Do not generate server thumbnails or re-render invoices from data: the preview must represent the already-generated document.

Each mounted eligible thumbnail downloads the existing PDF independently, with a 15 MiB automatic-download limit and an eight-million-pixel canvas ceiling. Keep bytes only for its active render; there is no reusable byte/thumbnail cache, shared loader, server thumbnail cache, or coupling to modal visibility. Ordinary React renders do not repeat the download; revisiting/remounting an invoice may download and draw it again. The existing full-preview workflow performs its own authoritative PDF read. Oversized documents retain the explicit full-preview action. Resource limits are implementation constants, not runtime settings.

PDF.js owns the per-render byte buffer; no other consumer reuses it. Cancel loading/render tasks when selection, metadata identity, space eligibility, or session changes. Guard completions against the active render and immediately remove a thumbnail known to be outdated. Destroy renderer tasks/documents and release canvas memory on cleanup; bytes are not retained across unmounts or sessions. Thumbnail failures never disable valid explicit document actions.

After downloading bytes, re-read metadata through the existing authorized invoice endpoint and compare invoice ID, document state/revision, PDF document revision and generated-at identity with the selected metadata. Discard mismatches or newly unavailable states and show a quiet retry/refresh explanation, without an automatic retry loop. Keep existing PDF storage keys, generation, reads and backend contracts unchanged; no identity header or storage migration is required.

Metadata revalidation reduces but does not eliminate concurrent replacement races. A supplementary thumbnail can briefly disagree with a concurrently regenerated PDF until refresh, reselection or updated metadata triggers another render. This is an accepted consistency trade-off; neither revalidation nor a metadata label is described as proof that bytes have immutable revision identity. No inconsistent result is retained in a reusable cache.

Missing, stale, regenerating, failed, fetch-failed, or unrenderable PDFs use concise inline visual states while existing readiness/repair UI remains authoritative. Passive loading never invokes generation, regeneration, repair, issue/reissue, delivery, or audit mutations. A newly current revision becomes eligible naturally after the existing explicit workflow succeeds.

### 3. Lightweight Google Maps Static previews

Use Google Maps Static API, not an iframe or JavaScript Maps application. Build the client query from nonempty saved billing-address fields in a deterministic order; build the gig query from its saved venue/location text. Never silently substitute the client's address for a missing gig venue or the seller's address for a client. No coordinates, place IDs, or geocoding results are persisted.

#### Automatic loading and source binding

Automatic imagery requires deployment enablement, valid signing configuration, a usable saved query, and visible eligible pane geometry. Existing, new, imported, and copied records follow the same rule. There is no approval checkbox, public-location declaration, persisted consent flag, or approval-reset behavior in editors or write paths. Ordinary location edits invalidate the displayed image and its source identity; the new saved location becomes eligible automatically. Unsaved form text is not sent for a preview.

The full saved billing address or gig location is a Google Maps search link, independent of image configuration. A ready image links to the same search; both are keyboard accessible and use safe new-tab attributes. Remove the separate Open in Google Maps line. Missing imagery leaves full-width facts and the linked location without an empty rail or consent panel. Do not claim public/private address classification or a categorical residential-address prohibition. Provider privacy clarification remains a production-rollout requirement.

Static Maps can resolve textual queries internally, but this is not a confidence-scored geocoder. Use accessible image/action descriptions and hover/focus tooltip text to explain the unverified search, without a permanent caption. Omit empty/unsupported queries and provider failures. Construct external HTTPS search links with `api=1` and the encoded saved query. The textual location link remains a separate affordance from the clickable image; neither implies a verified exact match.

Measure only the map's reserved frame through its own ResizeObserver, independently of shared eligibility and invoice rendering. Request the initial shape immediately after measurement, then coalesce subsequent changes for 300ms. Scale both axes together to 640px maximum, preserving the measured aspect ratio within pixel rounding. Accept canonical integer dimensions from 32 through 640 per axis, plus small/medium presets for environments without frame observation. Never delay or remount the shared overview for map sizing. Display the whole image without cover-cropping; rounded corners must retain readable provider logos/copyright. Verify provider policy, attribution, signing/security, billing and geographic-account terms before rollout.

### 4. Authenticated signed URLs and direct Google delivery

Protected endpoints under existing prefixes return a JSON signed-URL descriptor, not image bytes. Resolve records through `WhereVisibleTo`, including normal shared/null-owner semantics. Derive the location server-side; accept only record IDs, validated bounded dimensions or supported presets, and an expected source fingerprint, never arbitrary queries, destinations, provider parameters or credentials. URL issuance is a read and never modifies a record.

Use the versioned normalization/fingerprint contract with shared TypeScript/C# vectors. Bind the descriptor and every frontend completion to session, record type/ID, source identity, and size bucket. Reject obsolete source identity before signing. Only the location query and necessary rendering/authentication parameters belong in Google's URL; do not append client names, contact details, internal record IDs, or fingerprints. Arbitrary location text can itself contain personal information; payload minimization is not a content detector.

Build a fixed `https://maps.googleapis.com/maps/api/staticmap` URL with allowlisted dimensions and correctly encoded parameters. Sign the exact final path/query server-side using Google's documented URL-signing algorithm. Never expose the signing secret. The restricted API key and resulting signature are intentionally visible in the image URL. The browser assigns that URL directly to `<img src>`; it does not fetch image bytes through Glovelly, convert them to blob/data URLs, or rehost them. There is no outbound image HTTP client, image-response buffer, or server image timeout/concurrency pipeline.

Rate-limit authenticated URL issuance per user, initially ten descriptors per minute, and bound query/URL lengths and size buckets. Google-side quotas provide the provider usage boundary; issuance rate limits do not prevent replay of a copied signed URL. Signatures prevent parameter tampering but are not encryption, user-bound authorization, or expiring access tokens. Referrer/API restrictions, required signatures, disabled unsigned-request quota, and Google-side request caps protect actual use. Do not claim immediate revocation or invent unsupported expiry parameters.

Return descriptors with `private, no-store` and exclude them from service-worker/offline caches. Retain only the active URL in component memory, remove the image on source/selection/session/eligibility changes, and guard late descriptor/load/error completions. Do not persist/cache map images, geocoding, or signed URLs in Glovelly. Respect Google's delivery/cache headers rather than implementing an application image cache. Hiding/removing an image cannot recall a request already dispatched by the browser.

Bound loading with a client watchdog (initially 10 seconds) and quiet fallback on load failure or issuance limits. Do not add retry loops on render, resize, or hidden-pane transitions. Direct cross-origin images do not expose provider response headers/status to JavaScript; do not retain proxy-specific warning/status inspection requirements or claim detection of every provider-generated error image. The external Maps link remains usable.

Use a browser referrer policy that sends only the application's origin, compatible with configured website restrictions; do not use `no-referrer` on these image requests. Restrict production and staging origins, and separately authorize local development only where intended. Never log signed URLs, query text, or signing secrets. Direct delivery sends the location query and browser network/request information to Google and may interact with provider browser state; privacy/consent review must reflect that, without promising cookie-free or anonymous requests.

### 5. Configuration, privacy, and failure behavior

Use `Maps:GoogleStatic:Enabled` (default false), `ApiKey`, and `SigningSecret` settings. Valid API key and signing secret are both required to issue an image URL; never fall back to unsigned URLs. Use dedicated Maps Static API/website restrictions rather than reusing the Routes key. Cloud Run binds `glovelly-maps-static-api-key` and `glovelly-maps-static-url-signing-secret`; local Development loads the corresponding keys from API-project .NET user-secrets. Configuration examples contain no values. Missing, invalid, or disabled configuration makes imagery unavailable without breaking core records, mileage, or external Maps links, and without repeated calls every render.

Update appsettings, both staging/production workflow blocks, README, relevant engineering configuration documentation, and AGENTS.md where operating constraints change. Verify whether existing `/clients` and `/gigs` proxy/SW prefix coverage is sufficient; add bypass coverage if any new prefix is introduced. Update frontend types if capability metadata or response shapes change.

Eligible previews load automatically without per-record activation. Privacy/terms and the user guide explain the saved location query, direct Google browser request, independent external link, and lack of durable application caching. Review public URLs, providers, browser storage/consent behavior, retention, and terms requirements; update Last updated dates on edited legal pages. No analytics loader is added. The billing account is UK, outside the EEA-specific account regime, while European end-user restrictions can still apply. Provider clarification about ordinary address queries is pending; production loading remains disabled until the response and applicable requirements are resolved. Do not claim a public-address checkbox is a documented Google requirement.

Supplementary preview failures stay inline or quietly fall back to details, not global error-toast spam. Authentication expiry still follows the existing session-expiry path. Do not make preview errors disable primary record actions, and do not hide document-readiness warnings or repair controls.

## Risks / Trade-offs

- [A visual squeezes readable facts] → Gate on actual pane dimensions, preserve minimum facts width, and omit rather than stack the rail.
- [Automatic PDF reads/rendering consume memory] → Lazy worker, eligibility gating, bounded per-render bytes/canvas, cancellation and cleanup; no reusable cache.
- [Concurrent PDF replacement briefly disagrees with metadata] → Post-download metadata recheck, obsolete-result guards and fresh reads on revisit. A residual race is accepted rather than adding immutable storage; normal render input is not shared between consumers.
- [Map query points somewhere unexpected] → Explicit search-preview wording, original textual location/link, and no verified-pin claim or saved geocoding state.
- [Location queries disclose information to Google] → Minimized query payloads, accurate automatic/direct-delivery disclosures, and provider privacy clarification before production. No reliable public/private classification is assumed.
- [Signed URLs are copied or replayed] → Restricted API key, server-only signing secret, exact-parameter signatures, origin restrictions, bounded issuance, and Google-side request caps. Signatures alone do not cap usage or expire links.
- [Maps policy/privacy differs from assumptions] → Review current provider terms and disclosures before rollout; preserve attribution and avoid durable caching. Pause for user decision if the selected delivery/activation model is not permissible.
- [Old browser geometry is treated as proven] → Run Ubuntu CI tests for this change; preceding archive waivers do not validate this layout.

## Migration Plan

Implement PDF rendering independently of Maps configuration. PDF storage/generation and schema remain unchanged; thumbnails add only browser-side reads/rendering. Map previews require no approval schema or business-record mutations. Remove approval UI/contracts/write hooks and the byte-proxy service. Check migration deployment state before removing approval schema: an unapplied change-local migration can be removed with its snapshot changes, while an applied migration requires a normal forward cleanup migration. Never rewrite deployed migration history or discard business locations/documents. Deploy imagery disabled; validate signed direct delivery on staging and enable production only after privacy/provider and operational gates pass. Roll back imagery by disabling the deployment setting.

## Open Questions

No PDF storage decision remains open: use existing mutable storage, uncached browser rendering and best-effort post-download metadata checks. Pane thresholds, render limits and image size buckets require browser validation. The Static Maps API and named secrets are provisioned; staged restrictions/signature enforcement, quotas and provider privacy response remain rollout checks. The automatic-preview model is the product contract, not a legal determination. References are in `provider-review.md`.
