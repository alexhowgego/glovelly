## Purpose

Define the canonical ownership, routing, and verification of Glovelly's public web surfaces.

## Requirements

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

### Requirement: Canonical Google OAuth registrations
The production Google OAuth configuration SHALL authorize the Menu origin and its sign-in, Drive, Sheets, and Calendar callback URLs. It SHALL not retain apex application callback registrations after the apex has moved to Firebase Hosting.

#### Scenario: A production user starts a Google flow
- **WHEN** a production user starts sign-in or a configured Google integration flow
- **THEN** Google authorizes the corresponding callback under `https://menu.glovelly.net`

### Requirement: Apex landing isolation
The apex `glovelly.net` SHALL be served by Firebase Hosting as the public landing site and SHALL not route requests through the Cloud Run application.

#### Scenario: A visitor opens the apex
- **WHEN** a visitor requests `https://glovelly.net`
- **THEN** Firebase Hosting serves the public landing site without an application-host redirect

### Requirement: Public-surface verification
The deployment documentation SHALL define post-deployment smoke checks covering every public hostname, Menu authentication, generated application links, Google integration callbacks, and MCP discovery metadata.

#### Scenario: An operator deploys the routing change
- **WHEN** the operator reaches the post-deployment verification stage
- **THEN** the documented checks identify the expected URL and successful outcome for each public surface and application flow
