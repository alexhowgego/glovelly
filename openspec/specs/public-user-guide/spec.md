# Public User Guide Specification

## Purpose
Define the public, task-oriented Glovelly guide for musicians and sole traders.

## Requirements

### Requirement: Public task-oriented guide is independently available
The system SHALL publish a static Starlight user guide at `https://docs.glovelly.net`, independently of the authenticated application and technical/operator handbook. The guide SHALL provide responsive navigation, search, keyboard-accessible controls, and canonical routes to the Glovelly front door, Menu, handbook, legal documents, and repository.

#### Scenario: Visitor opens the guide
- **WHEN** a visitor requests `https://docs.glovelly.net`
- **THEN** they can navigate and search the public guide without an authenticated application session

#### Scenario: Visitor needs another public surface
- **WHEN** a visitor follows a guide route to the front door, Menu, handbook, legal documents, or repository
- **THEN** the route uses the corresponding canonical public URL

### Requirement: Guide teaches the core work loop
The guide SHALL use a practical "do this next" voice and cover currently shipped V1 workflows for access, clients, gigs, expenses/receipts/mileage, creating/reviewing/sending invoices, invoice status/payment visibility, seller profile, and user settings. It SHALL include a first-invoice journey that connects the relevant core workspaces.

#### Scenario: New user completes the first-invoice journey
- **WHEN** a user reads the first-invoice guide
- **THEN** it explains the ordered path from client and gig through optional costs to invoice generation and reviewed delivery

#### Scenario: User needs a core task
- **WHEN** a user opens a V1 core-workflow page
- **THEN** the page states its outcome, prerequisites, concise actionable steps, and what happens next

### Requirement: Access guidance describes current behavior without future promises
The guide SHALL explain that users sign in with the Google account linked to their invitation and can request access when their account is not enabled. It SHALL state that Google sign-in alone does not grant access and SHALL not promise self-service enrolment, approval times, plans, entitlement limits, or payment behavior that is not shipped.

#### Scenario: Uninvited visitor reads access guidance
- **WHEN** a person whose Google account is not enabled reads the access page
- **THEN** they learn that they can request access after sign-in without being promised an approval outcome or timeframe

### Requirement: User guide and handbook retain distinct editorial boundaries
The guide SHALL use plain, outcome-led task guidance for musicians and sole traders. Technical implementation, deployment, administration, operator procedures, and detailed engineering reference SHALL remain in the handbook or repository documentation.

#### Scenario: Visitor needs technical detail
- **WHEN** a guide topic would require technical/operator information
- **THEN** the guide directs the visitor to the handbook or repository rather than embedding that material in the task guide
