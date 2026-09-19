## ADDED Requirements

### Requirement: Generated invoice content changes only while a linked invoice is draft
The system SHALL rebuild system-generated invoice lines from linked gig data only when the linked invoice has `Draft` status. It SHALL NOT automatically alter generated lines, document revisions, or PDFs for linked issued, overdue, paid, or cancelled invoices.

#### Scenario: A draft linked gig source changes
- **WHEN** a shared generated-line synchronization caller processes a gig linked to a draft invoice
- **THEN** the system SHALL replace that gig's generated lines using its current gig data

#### Scenario: A finalized linked gig source changes
- **WHEN** a shared generated-line synchronization caller processes a gig linked to an issued, overdue, paid, or cancelled invoice
- **THEN** the system SHALL leave the invoice's generated lines, document revision, and PDF metadata unchanged

### Requirement: Refreshing changed gig sources produces one current draft document per invoice
The system SHALL provide a shared operation that refreshes the draft invoices affected by one or more changed gigs. For each distinct affected draft invoice, the system SHALL rebuild all affected generated lines, advance its document revision once, and generate one PDF representing the final line collection.

#### Scenario: Several changed gigs share a draft invoice
- **WHEN** a refresh operation receives two or more changed gigs linked to the same draft invoice
- **THEN** the system SHALL rebuild generated lines for each changed gig and advance and regenerate that invoice once

#### Scenario: Changed gigs link to separate draft invoices
- **WHEN** a refresh operation receives changed gigs linked to different draft invoices
- **THEN** the system SHALL refresh each distinct draft invoice independently

### Requirement: Quick receipt changes automatically refresh affected draft invoices
The system SHALL refresh affected draft invoices after quick receipt details are saved or a quick receipt is moved between gigs. The successful response SHALL identify the affected invoice updates so the initiating workspace can immediately reflect them.

#### Scenario: Receipt details are saved for a selected-gig draft invoice
- **WHEN** a user saves a quick receipt description and amount on a gig linked to a draft invoice
- **THEN** the response SHALL include the regenerated invoice lines and a current PDF whose content includes the receipt-derived line

#### Scenario: Receipt details are saved for a manually linked monthly draft invoice
- **WHEN** a user saves a quick receipt on a gig manually linked to a draft invoice shared by multiple gigs
- **THEN** the system SHALL regenerate the shared draft invoice once using its final combined line collection

#### Scenario: A receipt moves between linked gigs
- **WHEN** a user moves a quick receipt from one linked gig to another
- **THEN** the system SHALL refresh each distinct affected draft invoice and leave unrelated or non-draft invoices unchanged

### Requirement: Quick receipt guidance explains automatic-refresh scope
The quick-receipt workflow SHALL visibly state that receipt changes automatically refresh linked draft invoices only and do not automatically change issued or other non-draft invoices.

#### Scenario: User reviews quick receipt details
- **WHEN** a user views the quick-receipt details workflow
- **THEN** the workflow SHALL display the draft-only automatic-refresh guidance near the receipt save controls or linked-invoice context

### Requirement: Invoice workspaces converge after automatic refresh
The system SHALL publish invoice workspace updates for automatic draft-invoice refreshes, and connected invoice workspaces SHALL refresh their invoice state on those updates.

#### Scenario: Another session receives an automatic invoice refresh event
- **WHEN** a quick receipt refreshes a draft invoice in one authenticated session
- **THEN** another connected session for that user SHALL refresh its invoice workspace to show the updated invoice state
