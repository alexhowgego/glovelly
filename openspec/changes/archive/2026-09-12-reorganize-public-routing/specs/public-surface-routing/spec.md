## ADDED Requirements

### Requirement: Public surface ownership
The system SHALL assign each public hostname one canonical purpose and delivery owner: `glovelly.net` for the public landing site on Firebase Hosting, `menu.glovelly.net` for the authenticated application on Cloud Run, `docs.glovelly.net` for the public user guide on Firebase Hosting, and `handbook.glovelly.net` for the technical/operator handbook on GitHub Pages.

#### Scenario: A maintainer identifies a public surface
- **WHEN** a maintainer consults the deployment documentation for a public hostname
- **THEN** the documentation identifies its canonical purpose, delivery platform, deployment path, and owner

#### Scenario: The handbook is published
- **WHEN** the DocFX handbook is published from the main branch
- **THEN** it is available at `https://handbook.glovelly.net` and not at the user-guide hostname

### Requirement: Canonical application origin
The deployed application SHALL use an explicit, validated `App:PublicBaseUrl` as its canonical public origin. Production SHALL configure this value as `https://menu.glovelly.net`; non-production deployed environments SHALL configure their own explicit public application origin.

#### Scenario: Production application deployment
- **WHEN** the production application starts
- **THEN** it validates and uses `https://menu.glovelly.net` as its canonical public origin

#### Scenario: Invalid deployed application origin
- **WHEN** a non-development deployment has a missing, non-absolute, or non-HTTPS public base URL
- **THEN** the application fails configuration validation rather than generating public URLs from an incoming request host

### Requirement: Canonical public application URLs
The application SHALL construct deployed-environment OAuth redirect URIs, integration callbacks, post-sign-in destinations, invitation links, access-request review links, email application links, and MCP issuer/resource URLs from its configured canonical application origin rather than the incoming request host.

#### Scenario: An administrator sends an invitation
- **WHEN** an administrator sends an invitation in production
- **THEN** the invitation login link starts with `https://menu.glovelly.net`

#### Scenario: A user connects a Google integration
- **WHEN** a production user starts a Google Drive, Google Sheets, or Google Calendar connection
- **THEN** the provider redirect URI uses the corresponding callback under `https://menu.glovelly.net`

#### Scenario: A user completes Google sign-in
- **WHEN** Google returns a production user to `/signin-oidc`
- **THEN** the configured Menu origin is used for the provider redirect URI and the user is returned only to an allowed Menu destination

### Requirement: Google OAuth registration migration
The deployment SHALL register the Menu origin and all required Menu callbacks with Google before Menu becomes the production application origin, and SHALL retain the old apex registrations through the documented overlap and rollback window.

#### Scenario: Menu is prepared for production traffic
- **WHEN** the Menu Cloud Run domain mapping is ready for production verification
- **THEN** Google authorizes the Menu sign-in callback and each configured Menu integration callback before users are directed to Menu

#### Scenario: The migration is still reversible
- **WHEN** the documented overlap or rollback window remains open
- **THEN** the prior apex OAuth registrations remain configured alongside the Menu registrations

### Requirement: Safe apex migration
The production migration SHALL establish and verify Menu before reassigning the apex to Firebase Hosting. During the defined overlap, legacy application requests to the apex SHALL preserve their path and query string when redirected to Menu, while the apex root remains available for the public landing site once cut over.

#### Scenario: A user opens a legacy application link during overlap
- **WHEN** a user requests a legacy apex application path with query parameters during the overlap window
- **THEN** the user is redirected to the equivalent Menu URL with its query parameters preserved

#### Scenario: The apex cutover is approved
- **WHEN** Menu sign-in, sign-out, application links, integration callbacks, and MCP smoke checks have passed and the callback overlap has elapsed
- **THEN** `glovelly.net` can be assigned to Firebase Hosting without making Menu unavailable

#### Scenario: The migration requires rollback
- **WHEN** a post-deployment smoke check fails during the documented rollback window
- **THEN** operators can restore the apex application mapping and prior application-origin configuration using the documented rollback procedure

### Requirement: Public-surface verification
The deployment documentation SHALL define post-deployment smoke checks covering every public hostname, Menu authentication, generated application links, Google integration callbacks, MCP discovery metadata, and rollback readiness.

#### Scenario: An operator deploys the routing change
- **WHEN** the operator reaches the post-deployment verification stage
- **THEN** the documented checks identify the expected URL and successful outcome for each public surface and application flow
