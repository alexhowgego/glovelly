## MODIFIED Requirements

### Requirement: Static privacy-conscious operation
The landing site SHALL operate without visitor account registration, a CMS, advertising technology, or embedded tracking media. It SHALL use analytics only as defined by the public-site-analytics capability, present clear routes to the current privacy policy and terms of service, and provide an accessible analytics-cookie consent control.

#### Scenario: A visitor loads the landing page without analytics consent
- **WHEN** a visitor loads the landing page without having accepted analytics cookies
- **THEN** the site does not initialise analytics, advertising, or tracking services and presents the visitor's analytics-consent choices

#### Scenario: A visitor needs legal information
- **WHEN** a visitor uses the landing-page footer
- **THEN** they can reach the current privacy policy and terms of service at their canonical public destinations
