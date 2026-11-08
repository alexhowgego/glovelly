## ADDED Requirements

### Requirement: Task guides link directly to relevant workspaces
The public user guide SHALL link task-specific Menu handoffs to the canonical workspace where the reader should begin. Client tasks SHALL link to Clients; gig and expense tasks SHALL link to Gigs; invoice review and status tasks SHALL link to Invoices; and the first-invoice journey SHALL link to Clients as its first workspace. General sign-in and Menu links without a single relevant workspace SHALL remain appropriate general entry links.

#### Scenario: Reader opens a task workspace
- **WHEN** a reader follows the Menu handoff on a Clients, Gigs, expenses, invoice review, or invoice status guide page
- **THEN** the link targets the canonical Menu workspace for that task

#### Scenario: Reader starts the first-invoice journey
- **WHEN** a reader follows the Menu handoff on the first-invoice journey
- **THEN** the link opens the Clients workspace, where the described journey begins

#### Scenario: Reader seeks general access
- **WHEN** a reader follows a general Menu or sign-in link without a specific workspace task
- **THEN** the link remains an appropriate general Menu entrypoint
