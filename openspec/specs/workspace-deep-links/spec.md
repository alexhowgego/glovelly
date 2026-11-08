# Workspace Deep Links Specification

## Purpose
Define canonical, safe workspace entrypoints and navigation behavior for Menu.

## Requirements

### Requirement: Canonical workspace entrypoints
The application SHALL provide canonical workspace entry paths `/workspaces/clients`, `/workspaces/gigs`, and `/workspaces/invoices` for Clients, Gigs, and Invoices respectively. A direct visit or refresh SHALL select the corresponding workspace after normal session handling. These document paths SHALL not conflict with existing API routes.

#### Scenario: Direct visit to a workspace
- **WHEN** an authenticated user opens a canonical Clients, Gigs, or Invoices path directly or refreshes it
- **THEN** Menu displays the corresponding workspace and its normal authorized data

#### Scenario: API paths retain their purpose
- **WHEN** a client requests an existing Clients, Gigs, or Invoices API endpoint
- **THEN** the endpoint retains its existing API and authorization behavior rather than serving the workspace document

### Requirement: URL follows workspace navigation without history entries
When the user explicitly navigates to Clients, Gigs, or Invoices, including through a cross-workspace action, the application SHALL replace the current history entry with the corresponding canonical workspace path. It SHALL NOT add a history entry for each workspace switch. When explicitly navigating to an unsupported section, the application SHALL NOT leave a supported workspace URL falsely identifying the selected section.

#### Scenario: User switches workspace
- **WHEN** a user switches from Clients to Invoices through Menu navigation
- **THEN** Invoices is selected and the address is `/workspaces/invoices` without an additional history entry

#### Scenario: Cross-workspace action changes selection
- **WHEN** a user performs an existing action that selects a different supported workspace
- **THEN** the address identifies the resulting workspace and refreshing it returns to that workspace

#### Scenario: User enters Admin
- **WHEN** an administrator explicitly selects Admin from a supported workspace
- **THEN** the address no longer claims the previous supported workspace is selected, without establishing an Admin deep link

### Requirement: Safe sign-in and fallback
Workspace paths SHALL use the existing authentication and authorization controls. An unauthenticated user SHALL be able to sign in and return to the requested workspace, subject to existing safe-return validation. An unknown or invalid application path that reaches the SPA SHALL select the normal application entry state without interpreting arbitrary path content as a workspace. Existing server and API routes retain their own responses.

#### Scenario: Sign in after opening a workspace
- **WHEN** a signed-out user opens a canonical workspace path and successfully signs in
- **THEN** the user returns to the requested path and sees its workspace only if authorized

#### Scenario: Unsafe return destination
- **WHEN** sign-in receives an external or otherwise unsafe return destination
- **THEN** it does not redirect the user to that destination

#### Scenario: Unknown SPA path
- **WHEN** a user opens an unknown or invalid application path that reaches the SPA
- **THEN** Menu uses the normal entry state, currently Gigs, without selecting a workspace from arbitrary path content

### Requirement: Access-request review links remain functional
Workspace URL synchronization SHALL preserve an incoming access-request review URL through authentication and review initialization and while the review surface remains open. Existing admin and non-admin review behavior SHALL remain intact. After review dismissal or explicit navigation away, the application SHALL no longer retain a stale review path that would reopen the request on refresh.

#### Scenario: Administrator opens a review link after sign-in
- **WHEN** a signed-out administrator opens an access-request review link and signs in
- **THEN** the same request opens for review and workspace synchronization does not overwrite the link prematurely

#### Scenario: Non-administrator opens a review link
- **WHEN** a non-administrator opens an access-request review link
- **THEN** the existing access-denied behavior remains in effect and request details are not disclosed

#### Scenario: Review is dismissed
- **WHEN** the user dismisses the review surface or explicitly navigates away
- **THEN** the address reflects the resulting normal application state and refreshing does not automatically reopen the dismissed review

### Requirement: Workspace navigation is verified in a browser
Browser coverage SHALL verify direct document navigation to each supported workspace and the interactions between navigation, refresh, authentication, fallback, and access-request review. Coverage SHALL exercise the deployed application routing rather than only unit-testing a path parser.

#### Scenario: Browser checks supported entrypoints
- **WHEN** the browser suite opens each canonical workspace URL directly
- **THEN** it verifies the matching selected workspace and successful refresh

#### Scenario: Browser checks existing navigation
- **WHEN** the browser suite exercises workspace switching, sign-in return, an unknown SPA path, and access-request review
- **THEN** it verifies their specified URL and selection behavior without regressions
