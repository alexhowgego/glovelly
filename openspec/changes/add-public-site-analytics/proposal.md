## Why

Glovelly has no insight into how visitors discover its public landing page or use its user and operator documentation. Privacy-conscious, consent-gated analytics will provide aggregate public-site traffic information without tracking people in the authenticated Menu application.

## What Changes

- Add Google Analytics 4 measurement to the public landing site, user guide, and technical/operator handbook after a visitor accepts analytics cookies.
- Provide a consistent public-site consent experience that allows visitors to accept, reject, and later change their analytics choice.
- Exclude the authenticated Menu application, local development, staging deployments, Firebase preview channels, and automated/UAT traffic.
- Update the public privacy policy to describe the analytics processing and visitor choices.

## Capabilities

### New Capabilities
- `public-site-analytics`: Consent-gated, production-only analytics for Glovelly's public web surfaces.

### Modified Capabilities
- `public-landing-site`: Replace the no-analytics constraint with a consent-gated public analytics requirement.

## Impact

- Landing Astro page, Starlight user-guide configuration, and DocFX handbook template assets.
- Public privacy policy and public-site footers or equivalent consent controls.
- Public-site deployment/build verification; no API, authenticated application, or database changes.
