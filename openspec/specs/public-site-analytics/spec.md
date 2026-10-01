## Purpose

Provide consent-gated, privacy-conscious analytics on Glovelly's production public sites.

## Requirements

### Requirement: Public analytics is consent-gated
The system SHALL load Google Analytics 4 using measurement ID `G-ZTRBP9KE40` on `https://glovelly.net`, `https://docs.glovelly.net`, and `https://handbook.glovelly.net` only after the visitor gives affirmative analytics consent. It SHALL not initialise Google Analytics, request Google tag resources, or send analytics data before consent.

#### Scenario: Visitor accepts analytics on a public site
- **WHEN** a visitor at a production public hostname accepts analytics cookies
- **THEN** the site loads Google Analytics 4 and records ordinary page-view analytics for that public hostname

#### Scenario: Visitor rejects analytics on a public site
- **WHEN** a visitor rejects analytics cookies
- **THEN** the site does not load Google Analytics or send analytics data

### Requirement: Analytics is limited to production public surfaces
The system SHALL enable public analytics only for `glovelly.net`, `docs.glovelly.net`, and `handbook.glovelly.net`. It SHALL exclude `menu.glovelly.net`, local development, staging deployments, preview deployments, and automated test environments.

#### Scenario: Visitor opens the authenticated application
- **WHEN** a visitor loads `https://menu.glovelly.net`
- **THEN** no public-site analytics consent prompt or Google Analytics integration is available

#### Scenario: A non-production public build is opened
- **WHEN** a visitor opens a local, staging, or preview deployment
- **THEN** the deployment does not load Google Analytics or send analytics data

### Requirement: Visitors can control analytics consent
The system SHALL present an accessible analytics consent choice to a visitor with no saved choice and SHALL provide a persistent, accessible control on each public surface for changing a saved choice. A consent choice SHALL apply across Glovelly's public subdomains.

#### Scenario: Visitor has not made a consent choice
- **WHEN** a visitor first opens a production public hostname
- **THEN** they can accept or reject analytics without being prevented from accessing the site's content

#### Scenario: Visitor withdraws analytics consent
- **WHEN** a visitor changes an accepted analytics choice to reject through Cookie settings
- **THEN** the site stops future analytics loading and removes accessible Google Analytics cookies

#### Scenario: Visitor moves between public surfaces
- **WHEN** a visitor who saved an analytics choice opens another production public hostname
- **THEN** that surface observes the same saved choice without presenting a conflicting prompt

### Requirement: Public analytics excludes personal and business content
The system SHALL limit analytics collection to ordinary public-site page views. It SHALL not send custom analytics events, user-entered content, account information, authenticated-application data, business records, or advertising-personalisation data.

#### Scenario: Visitor reads public documentation
- **WHEN** a visitor navigates public guide or handbook pages after accepting analytics
- **THEN** analytics records page-view information without custom events or visitor-entered content

### Requirement: Public privacy information describes analytics
The published privacy policy SHALL describe the use of Google Analytics on the public landing site, user guide, and handbook; the purpose of the processing; analytics-cookie consent and withdrawal controls; and the relevant Google privacy information.

#### Scenario: Visitor seeks privacy information
- **WHEN** a visitor follows a public privacy-policy link after analytics is introduced
- **THEN** the policy accurately describes the public-site analytics processing and their choices
