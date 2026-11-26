## Purpose

Ensure draft invoice PDFs visibly identify their lifecycle state and that issuance, recovery, and delivery consistently use watermark-compatible stored documents.

## Requirements

### Requirement: Every draft PDF page visibly identifies the document as draft
The system SHALL embed a prominent `DRAFT` watermark across the invoice content of every page of generated Draft invoice PDFs. The watermark SHALL be ordinary PDF page content, remain visible in preview, downloaded bytes, and printing, and preserve readability of invoice content. This behavior SHALL be independent of user administrative permissions or subscription tier.

#### Scenario: Single-page draft generation
- **WHEN** a draft invoice PDF is generated
- **THEN** its stored PDF bytes contain a visible `DRAFT` watermark across the page body without obscuring invoice details

#### Scenario: Multi-page draft generation
- **WHEN** draft invoice content overflows onto multiple pages
- **THEN** every page has the watermark, including continuation pages with opaque content boxes

#### Scenario: Draft bytes are opened outside Glovelly
- **WHEN** a user downloads an available current draft PDF and opens it in another PDF viewer
- **THEN** its pages remain watermarked without any Glovelly UI overlay

### Requirement: Draft watermark removal is explained in the interface
The invoice workspace SHALL show a concise visible hint for Draft invoices explaining that issuing the invoice removes the watermark from the resulting PDF, for example: "The DRAFT watermark is removed when you issue this invoice." The hint SHALL appear near the document or issuance controls, be readable on desktop and mobile without hovering, and SHALL NOT imply that issuance sends the invoice or changes previously downloaded copies.

#### Scenario: Viewing a draft invoice
- **WHEN** a user views a Draft invoice in the invoice workspace
- **THEN** a visible hint explains that issuing the invoice produces a PDF without the DRAFT watermark
- **AND** the explanation is accessible without a tooltip or hover interaction on desktop and mobile

#### Scenario: Invoice is issued
- **WHEN** the invoice successfully transitions out of Draft
- **THEN** the draft-only hint is no longer shown

#### Scenario: Reissue returns the invoice to Draft
- **WHEN** an invoice returns to Draft through reissue
- **THEN** the hint is shown again and explains how to obtain the clean issued PDF

### Requirement: Clean documents require an eligible lifecycle state
The system SHALL generate Issued, Paid, Overdue, and previously issued Cancelled invoice PDFs without a draft watermark while preserving invoice data, layout, and monetary formatting. Cancelled invoices without `FirstIssuedUtc` SHALL retain the DRAFT watermark; cancellation SHALL NOT make a never-issued invoice eligible for a clean PDF. Draft invoices SHALL remain watermarked even when previously issued.

#### Scenario: First issue
- **WHEN** a draft invoice is successfully issued
- **THEN** its current replacement PDF has no draft watermark and the normal first-issue audit fields are retained

#### Scenario: Non-draft regeneration
- **WHEN** an existing Issued, Paid, Overdue, or previously issued Cancelled invoice PDF is explicitly regenerated
- **THEN** its replacement has no draft watermark and regeneration does not alter issue, reissue, or delivery history

#### Scenario: Cancel a never-issued draft
- **WHEN** a never-issued draft is cancelled and its PDF is read or explicitly regenerated
- **THEN** an available current PDF SHALL remain watermarked and SHALL NOT acquire first-issue audit fields

#### Scenario: Cancel a previously issued invoice
- **WHEN** an invoice with a populated first-issue timestamp is cancelled and its PDF is explicitly regenerated
- **THEN** the resulting PDF SHALL be clean and its original first-issue timestamp SHALL be preserved

### Requirement: Invoice transitions generate documents for their intended state
The system SHALL determine watermark mode using the intended resulting invoice lifecycle state for creation, issue, redraft, and reissue, and the current state for adjustment, linked-source refresh, and ordinary regeneration. Watermarking SHALL NOT introduce additional lifecycle transitions or change existing audit semantics.

#### Scenario: Reissue returns an issued invoice to Draft
- **WHEN** a user successfully reissues an issued invoice through the existing reissue workflow
- **THEN** the invoice returns to Draft with a watermarked replacement PDF, preserving its first-issue identity and recording the existing reissue history

#### Scenario: Redraft or update a draft
- **WHEN** a draft is redrafted, adjusted, or refreshed from linked gig data and PDF replacement succeeds
- **THEN** the current replacement remains watermarked and contains the latest invoice content

#### Scenario: Issue a previously reissued draft
- **WHEN** a previously issued invoice now in Draft is successfully issued again
- **THEN** its current PDF is clean and its original first-issue timestamp is not replaced

#### Scenario: Restore a cancelled invoice to Draft
- **WHEN** a user changes a Cancelled invoice back to Draft
- **THEN** the explicit status operation SHALL regenerate the current watermarked PDF if needed, including pending linked-source changes, without adding a reissue or changing first-issue history
- **AND** regeneration failure SHALL leave Draft status saved with actionable document retry rather than exposing a clean PDF as current

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

### Requirement: All supported document surfaces use the stored watermark mode
The system SHALL use the same watermark-compatible stored PDF for thumbnail, full preview, download, email, and Drive publishing where those actions are otherwise permitted. Viewing documents SHALL NOT generate, issue, repair, or otherwise mutate an invoice.

#### Scenario: Preview and download agree
- **WHEN** a current draft is viewed as a thumbnail, opened in full preview, or downloaded
- **THEN** each surface obtains watermarked stored bytes under its existing independent-read and freshness rules

#### Scenario: Cancelled invoice delivery
- **WHEN** a Cancelled invoice is selected
- **THEN** application-managed email and Drive delivery SHALL be unavailable until it is restored to Draft and issued, regardless of whether its saved PDF is clean

#### Scenario: Existing external copies
- **WHEN** an invoice changes lifecycle state after a copy was downloaded, emailed, or published
- **THEN** the system replaces its current document as appropriate but does not claim to recall or modify previously distributed copies
