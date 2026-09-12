## ADDED Requirements

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
The landing site SHALL operate without visitor account registration, a CMS, analytics, advertising technology, or embedded tracking media. It SHALL present clear routes to the current privacy policy and terms of service, and SHALL not require a cookie-consent interaction when it does not set non-essential cookies.

#### Scenario: A visitor loads the landing page
- **WHEN** a visitor loads the landing page
- **THEN** the site does not initialise application authentication, analytics, advertising, or tracking services

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
