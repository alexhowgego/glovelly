## MODIFIED Requirements

### Requirement: Issuance has one explicit confirmation before mutation
Direct issuance SHALL require a single accessible confirmation explaining watermark removal and that issuing alone does not send an invoice. The confirmation SHALL offer optional linked-gig completion when applicable instead of a second post-issue prompt. Cancelling SHALL perform no issue or gig mutation. The same confirmation SHALL explain the actual first-issue allowance impact: one unit for a new metered first issue, no additional unit for a previously issued correction, and no finite-credit claim for unlimited plans. A stale UI estimate SHALL NOT replace authoritative backend enforcement.

#### Scenario: Confirm direct issuance
- **WHEN** a user selects Issued and confirms with optional linked-gig completion
- **THEN** issuance SHALL precede the selected gig updates and no further gig-completion confirmation SHALL appear

#### Scenario: Cancel issuance
- **WHEN** a user cancels or dismisses the issuance confirmation
- **THEN** invoice status, PDF, and linked gigs SHALL remain unchanged

#### Scenario: Free first-issue confirmation
- **WHEN** a Free user reviews first issuance
- **THEN** the same dialog SHALL explain that one monthly allowance unit will be consumed without adding another prompt

#### Scenario: Previously issued correction
- **WHEN** a previously issued invoice is reviewed for issuance again
- **THEN** the confirmation SHALL explain that no additional unit is consumed, including after a plan downgrade

### Requirement: Combined issue and delivery sends the issued document
Email review SHALL disclose mandatory issuance for Drafts and offer optional linked-gig completion without stacking another issuance prompt. Drive publishing SHALL use the same styled action confirmation for Draft and already-issued invoices. Both channels SHALL share issue-before-delivery coordination: a Draft must issue successfully through the authoritative entitlement/allowance and publication boundary before delivery begins, and no draft-delivery opt-out SHALL exist. Existing reviews SHALL disclose actual first-issue allowance impact rather than add a separate billing prompt. Delivery failure after issuance SHALL retain Issued status and consumed usage, explain partial success, and allow retry without issuing or consuming allowance again.

#### Scenario: Issue and send email
- **WHEN** a user confirms issue-and-send in email review
- **THEN** the invoice SHALL be issued and its clean PDF stored and committed with any required usage event before email preparation/delivery uses the attachment

#### Scenario: Review draft delivery
- **WHEN** a user reviews email or Drive delivery for a Draft invoice
- **THEN** the final action SHALL clearly say Issue and send or Issue and publish and SHALL NOT offer watermarked draft delivery

#### Scenario: Deliver an already-issued invoice
- **WHEN** an Issued, Paid, or Overdue invoice is delivered
- **THEN** the channel's styled review/confirmation SHALL be used without browser confirmation, another issuance, or another allowance unit

#### Scenario: Draft or Cancelled delivery is attempted directly
- **WHEN** actual email or Drive delivery is requested for a Draft or Cancelled invoice
- **THEN** the backend SHALL reject delivery before external side effects and without incrementing delivery history
- **AND** read-only Draft email preparation and authorized PDF preview/download SHALL remain available

#### Scenario: Issue or delivery fails
- **WHEN** issuance fails during issue-and-deliver, including allowance exhaustion
- **THEN** no delivery SHALL be attempted
- **AND** if issuance succeeds but delivery fails, Issued status and consumed usage SHALL be retained and retry SHALL NOT repeat issuance or consumption
