# Maps Static provider requirements

Review date: 2026-10-19. Technical guidance, not a legal determination.

## Product and account context

- Eligible saved client addresses and gig venues load automatically, without per-record approval or public/private classification. Location text may include residential addresses.
- The billing account is UK (user-confirmed), outside the EEA-specific billing-account regime. European end-user provisions can still cover UK users.
- Maps Static API is enabled and dedicated API/signing credentials are provisioned in Secret Manager and local .NET user-secrets. Image enablement remains a separate gate.

## Delivery and security requirements

- Street-address queries are explicitly supported by Maps Static documentation; the reviewed material does not establish a blanket residential-address prohibition or per-record public-address checkbox requirement.
- The Static Maps FAQ requires web image sources to point directly to Google. Glovelly issues authenticated, source-scoped signed URLs; the browser loads Google image bytes directly. No application byte proxy, rehosting, or durable/offline image cache is used.
- Signing happens server-side. The restricted API key and generated signature are visible in the image URL by design; the signing secret is private. Signed URLs are not inherently expiring or replay-proof.
- Apply Maps Static API and website/referrer restrictions, origin-only referrers, required signatures, disabled unsigned-request quota after verification, and Google-side request caps. Budget alerts are not spending caps.
- Preserve provider attribution and publish Google Maps end-user terms/privacy links. Direct requests disclose the location query and browser network/request information; do not promise anonymous or cookie-free delivery.

## Production privacy gate

Section 4.4(c)(ii) restricts customer provision of end-user personal data; section 4.4(c)(iii) concerns obtaining/caching an end user's location. A saved venue or client address is not automatically the user's own location, and a person mentioned in a record is not automatically a defined End User. These distinctions do not settle every case.

The Google EU user consent policy specifies consent for cookies/local storage where legally required and personal-data use for advertising personalization; it does not specify a public-address declaration for every map. Applicable disclosures and browser-storage requirements still need review.

A Google Data Privacy Inquiry has been submitted about automatic ordinary address queries, including residential locations, under section 4.4(c). Its response and any applicable requirements must be resolved before production enablement. The automatic-preview product design is not a claim that contractual compliance has already been established.

## Official sources

- Address queries: https://developers.google.com/maps/documentation/maps-static/start#Addresses
- Direct image delivery: https://developers.google.com/maps/faq#tos_staticmaps_reuse
- Signed URLs and authenticated issuance: https://developers.google.com/maps/api-security-best-practices#protect-static-web-api-usage and https://developers.google.com/maps/api-security-best-practices#proxy-server
- General terms and definitions: https://cloud.google.com/maps-platform/terms
- Service-specific terms: https://cloud.google.com/maps-platform/terms/maps-service-terms
- Consent policy: https://www.google.com/about/company/user-consent-policy/
- Privacy inquiry: https://support.google.com/cloud/contact/maps_api_privacy
