## MODIFIED Requirements

### Requirement: Cross-workspace browser navigation is automated
The UAT browser suite SHALL verify that cross-workspace shortcuts open the intended workspace and selected record without stale filters hiding the target, and SHALL verify that opening a gig from a generated invoice line hydrates an open gig editor from that target gig rather than a previously edited gig.

#### Scenario: User follows gig and invoice client shortcuts
- **WHEN** an authenticated UAT browser test opens a gig or invoice with a known client and activates the client shortcut
- **THEN** the Clients workspace SHALL open with that client selected and visible even if previous search filters would otherwise hide it

#### Scenario: User follows generated invoice line shortcuts
- **WHEN** an authenticated UAT browser test opens generated invoice lines linked to gigs and activates a line shortcut
- **THEN** the Gigs workspace SHALL open with the corresponding gig selected and visible

#### Scenario: Invoice-line shortcut replaces an open saved gig editor with the target gig
- **WHEN** an authenticated UAT browser test saves an invoice-relevant edit to one gig, regenerates its linked draft invoice, and follows a generated invoice line shortcut to a different linked gig without refreshing the browser
- **THEN** the second gig SHALL be selected and its editor fields and expenses SHALL reflect the second gig's persisted values rather than values from the first gig
- **AND THEN** saving an intended change to the second gig SHALL NOT alter the first gig or replace the second gig's unrelated fields and expenses with values from the first gig

#### Scenario: Manual adjustment lines are not gig shortcuts
- **WHEN** an authenticated UAT browser test opens invoice lines containing manual adjustments
- **THEN** manual adjustment lines SHALL NOT expose a gig navigation shortcut
