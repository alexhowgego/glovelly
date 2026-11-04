## Purpose

Provide a privacy-conscious public introduction to Glovelly, independently delivered from the authenticated application.

## Requirements

### Requirement: Public product introduction
The system SHALL serve a static landing page at `https://glovelly.net` that introduces Glovelly as a calm business-administration tool for working musicians and small creative businesses.

#### Scenario: A prospective visitor reaches the apex
- **WHEN** a visitor loads `https://glovelly.net`
- **THEN** they can understand that Glovelly keeps gigs, clients, expenses, and invoices together without making administration the main event

#### Scenario: The landing page presents its primary promise
- **WHEN** a visitor reads the page hero
- **THEN** it presents the promise "Music work, less admin" and identifies the intended audience

### Requirement: Outcome-led product story
The landing page SHALL communicate tangible product outcomes through a concise three-step journey covering planning paid work, recording the associated business information, and understanding invoices, payments, and the tax year. It SHALL not present an exhaustive feature catalogue or claim to provide accounting, tax, or professional advice.

#### Scenario: A visitor explores how Glovelly works
- **WHEN** a visitor reaches the product-story section
- **THEN** they can follow the three outcome-led steps without needing to understand internal application concepts

#### Scenario: A visitor encounters financial product messaging
- **WHEN** the page describes invoices, expenses, or tax-year visibility
- **THEN** it does not claim automated tax calculation, legal compliance, accounting advice, or professional advice

### Requirement: Art-directed, accessible visual identity
The landing page SHALL use responsive, accessible art-directed interface compositions to depict product moments, rather than stock imagery or screenshots represented as current product promises. Its visual direction SHALL combine editorial display typography, practical interface typography, warm neutral surfaces, deep blue structure, and restrained action accents.

#### Scenario: A visitor views a product moment on a large screen
- **WHEN** a visitor views the hero or outcome sections on a desktop viewport
- **THEN** the interface composition supports the accompanying product message without obscuring text or primary navigation

#### Scenario: A visitor views a product moment on a small screen
- **WHEN** a visitor views the landing page on a narrow viewport
- **THEN** compositions, content, and navigation reflow without horizontal scrolling, clipped controls, or loss of reading order

#### Scenario: A visitor uses assistive technology or reduced motion
- **WHEN** a visitor navigates with a keyboard, screen reader, or reduced-motion preference
- **THEN** page structure, links, controls, alternative text, focus states, and motion treatment remain understandable and usable

### Requirement: System-driven landing appearance
The landing page SHALL use a light appearance by default and SHALL follow the browser or device light/dark colour preference, including changes while the page is open. It SHALL not expose a manual theme selector, store a theme preference, or require theme-specific client-side JavaScript. Its appearance SHALL be independent of the user guide's manual theme selection.

#### Scenario: Visitor prefers a dark appearance
- **WHEN** a visitor loads the landing page with a dark system colour preference
- **THEN** the page renders its dark appearance without requiring a manual action

#### Scenario: Visitor prefers a light appearance or no preference is available
- **WHEN** a visitor loads the landing page with a light preference or without a supported colour preference
- **THEN** the page renders its light appearance

#### Scenario: System preference changes while browsing
- **WHEN** the system colour preference changes while the landing page is open
- **THEN** the page updates to the matching appearance without a reload

#### Scenario: Guide preference differs from system preference
- **WHEN** a visitor has selected a guide theme that differs from the system preference and opens the landing page
- **THEN** the landing page follows the system preference without reading or changing the guide selection

### Requirement: Accessible themed visual identity
Both landing-page appearances SHALL preserve the established warm Glovelly visual direction, remain visually consistent with the public guide's palettes, and provide accessible contrast for text, interactive elements and focus indicators. Paper-like illustrations MAY retain light surfaces in dark mode provided their content remains legible. Existing responsive layout and reduced-motion behavior SHALL remain usable in both appearances.

#### Scenario: Visitor reads or navigates in either appearance
- **WHEN** a visitor views either theme at a mobile or desktop viewport
- **THEN** text, links, controls and focus indicators remain distinguishable with accessible contrast and the content and navigation remain usable

#### Scenario: Visitor prefers reduced motion
- **WHEN** a visitor uses the landing page in either appearance with reduced motion enabled
- **THEN** existing nonessential motion remains reduced

### Requirement: Warm, clear product voice
The landing page SHALL use concise, concrete, lightly humorous copy that recognises the awkward reality of freelance and creative-work administration. The primary promise, calls to action, navigation labels, legal information, and critical product boundaries SHALL remain clear and direct.

#### Scenario: A visitor reads a supporting section
- **WHEN** a visitor reads the supporting story, including the "A gig is rarely just a gig" framing
- **THEN** the copy connects real work with the product outcome using a warm, practical voice

#### Scenario: A visitor chooses an action
- **WHEN** a visitor encounters a primary or navigational call to action
- **THEN** the label describes the destination or action without relying on humour to convey meaning

### Requirement: Public-surface navigation
The landing page SHALL provide a prominent primary route to `https://menu.glovelly.net` labelled "Open Glovelly Menu" and clear routes to the user guide at `https://docs.glovelly.net`, the technical/operator handbook at `https://handbook.glovelly.net`, and the Glovelly GitHub repository.

#### Scenario: An existing user opens the product
- **WHEN** an existing user activates the primary hero call to action
- **THEN** they are taken to `https://menu.glovelly.net`

#### Scenario: A visitor needs guidance or technical information
- **WHEN** a visitor uses the landing-page navigation or footer
- **THEN** they can identify and reach the user guide, technical/operator handbook, and repository without entering the authenticated application

### Requirement: Static privacy-conscious operation
The landing site SHALL operate without visitor account registration, a CMS, advertising technology, or embedded tracking media. It SHALL use analytics only as defined by the public-site-analytics capability, present clear routes to the current privacy policy and terms of service, and provide an accessible analytics-cookie consent control.

#### Scenario: A visitor loads the landing page without analytics consent
- **WHEN** a visitor loads the landing page without having accepted analytics cookies
- **THEN** the site does not initialise analytics, advertising, or tracking services and presents the visitor's analytics-consent choices

#### Scenario: A visitor needs legal information
- **WHEN** a visitor uses the landing-page footer
- **THEN** they can reach the current privacy policy and terms of service at their canonical public destinations

### Requirement: Independent static delivery
The landing site SHALL be built and deployed independently of the authenticated application to the Firebase Hosting `landing` target (`glovelly-landing`). It SHALL be deployable before the `glovelly.net` custom domain is attached and SHALL not alter Cloud Run API or SPA delivery.

#### Scenario: A maintainer deploys the landing site
- **WHEN** the landing-site deployment workflow runs with valid build and deployment credentials
- **THEN** it publishes only the landing-site artifact to the Firebase Hosting landing target

#### Scenario: The apex cutover is not yet approved
- **WHEN** the landing site has been deployed to its Firebase Hosting target but routing migration checks remain incomplete
- **THEN** the production apex remains unattached to placeholder landing content
