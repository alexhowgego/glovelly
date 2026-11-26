## Purpose

Define continuous monthly invoice issuance accounting and safe, authoritative publication of issued invoice documents.

## Requirements

### Requirement: Only first issuance consumes allowance
The system SHALL consume one unit for each genuinely first-issued invoice, using a server-owned stable invoice identity. Draft creation/editing/download, PDF generation, later issue/correction/reissue, delivery/retry, cancellation, and deletion SHALL NOT consume another unit or refund previous usage. Events SHALL be tracked on unlimited plans as well as Free.

#### Scenario: First issue succeeds
- **WHEN** an eligible user successfully issues a new invoice for the first time
- **THEN** exactly one usage event SHALL commit with its first-issue identity

#### Scenario: Previously issued invoice is corrected next month
- **WHEN** an invoice returns to Draft and is issued again, even in a later month or after a downgrade
- **THEN** no additional unit SHALL be consumed and original first-issue identity SHALL be retained

#### Scenario: Previously issued draft is deleted
- **WHEN** an invoice that has consumed a unit is reissued into Draft and deleted
- **THEN** its usage event SHALL remain for the billing user and SHALL NOT refund allowance

#### Scenario: Delivery fails after issue
- **WHEN** issuance commits but email or Drive delivery fails
- **THEN** usage SHALL remain consumed and delivery retry SHALL NOT repeat issuance or charge

### Requirement: Monthly usage is continuous across plan changes
Free SHALL enforce two first-issued invoices within a Europe/London calendar month, using half-open UTC boundaries derived from that timezone. Unused allowance SHALL NOT roll over. Accounting SHALL use the authoritative first-issue timestamp, not editable invoice dates. Plan change, renewal, cancellation, and expiry SHALL NOT reset the period or usage.

#### Scenario: Downgrade after unlimited-plan use
- **WHEN** a user issues ten invoices on Pro and changes to Free in the same month
- **THEN** usage SHALL remain ten, remaining allowance SHALL be zero, and new first issuance SHALL be blocked

#### Scenario: UK month resets across daylight saving time
- **WHEN** the Europe/London calendar month changes
- **THEN** the new month's allowance SHALL use the correctly converted local-midnight boundaries rather than fixed UTC offsets or a rolling thirty-day window

#### Scenario: User backdates an invoice
- **WHEN** a caller changes the invoice's business date before issue
- **THEN** usage SHALL still be attributed to the protected issuance operation's authoritative timestamp

#### Scenario: Operation crosses a month boundary
- **WHEN** PDF generation completes after the month changes
- **THEN** first-issue identity and usage SHALL use the same timestamp captured once inside the protected operation, without double consumption or differing months

### Requirement: First issuance is serialized with entitlement changes
The system SHALL serialize first issuance and subscription mutations for the same billing user across application instances, re-reading authoritative entitlement, usage, invoice lifecycle, and revision state after acquiring protection. Usage source identity SHALL be unique. Different users SHALL NOT share an unnecessary global quota lock.

#### Scenario: Concurrent requests compete for one remaining unit
- **WHEN** two instances first-issue different invoices for one user with one remaining unit
- **THEN** at most one SHALL succeed and the other SHALL receive an actionable allowance response

#### Scenario: Duplicate requests issue the same invoice
- **WHEN** concurrent/retried requests attempt the same first issue
- **THEN** at most one event SHALL be recorded and later requests SHALL NOT spend another unit

#### Scenario: Plan change races issuance
- **WHEN** a downgrade or paid grant and first issuance occur concurrently for one user
- **THEN** they SHALL have a consistent serialized order and issuance SHALL use the effective plan from that order

### Requirement: Clean PDF publication and usage commit together
Uncommitted clean PDF output SHALL NOT replace or become accessible through the committed draft's document pointer. The system SHALL prepare private generation-specific bytes and atomically commit invoice lifecycle/document metadata and usage in the database before publishing success. Failure before commit SHALL preserve committed allowance and document state. No new PDF generation SHALL overwrite bytes of a previously published generation.

#### Scenario: PDF storage fails
- **WHEN** first issuance cannot render/store its candidate
- **THEN** no usage/first-issue transition SHALL commit and the previously committed draft PDF SHALL remain unchanged

#### Scenario: Database commit fails after candidate storage
- **WHEN** clean candidate bytes were stored but the invoice/usage transaction fails
- **THEN** no authorized invoice PDF read or delivery SHALL discover that uncommitted clean candidate
- **AND** quota usage SHALL remain unchanged

#### Scenario: Published generation is replaced later
- **WHEN** an invoice is redrafted, reissued, regenerated, or issued
- **THEN** new output SHALL use a distinct private generation key and SHALL NOT mutate the old published bytes

#### Scenario: Stale render finishes after a newer mutation
- **WHEN** an obsolete document generation attempts to publish
- **THEN** revision/lifecycle concurrency checks SHALL prevent it from replacing the newer committed document

### Requirement: All writable invoice paths preserve authoritative issuance identity
Invoice creation SHALL begin as Draft. Incoming requests SHALL NOT set/clear first-issue identity, publication metadata, usage attribution, legacy exemptions, or lifecycle audit fields. Non-Draft creation and explicit injection of protected fields SHALL be rejected. Generic updates, status changes, and any other clean-document entrypoints SHALL delegate issuance to the same protected workflow.

#### Scenario: Caller creates Issued or Paid directly
- **WHEN** invoice creation supplies a non-Draft status
- **THEN** the server SHALL reject the request rather than create an unmetered clean-document path

#### Scenario: Caller forges prior issuance
- **WHEN** a caller supplies first-issue timestamps, legacy exemption, or PDF publication metadata
- **THEN** the server SHALL reject those fields and SHALL NOT alter usage or trusted document identity

#### Scenario: Generic update requests Issued
- **WHEN** an allowed generic invoice update changes Draft to Issued
- **THEN** the same quota, publication, ownership, and audit guarantees SHALL apply as on the dedicated status route

### Requirement: Legacy issuance is preserved without fabricated usage
Migration SHALL backfill accurate historical usage only from trustworthy first-issue timestamps and unambiguous billing subjects. Existing evidence of prior issuance without a timestamp SHALL receive a trusted server-owned legacy marker, not a fabricated timestamp or a new charge for correction. Intentionally shared records SHALL retain visibility semantics and SHALL NOT have usage duplicated across every user.

#### Scenario: Existing invoice has a known first issue
- **WHEN** an existing invoice has a trustworthy FirstIssuedUtc and attributable owner/first issuer
- **THEN** its usage SHALL be backfilled once at that actual instant and remain continuous on plan changes

#### Scenario: Legacy issued invoice lacks its timestamp
- **WHEN** a trusted existing Issued/Paid/Overdue invoice or invoice with reissue history lacks first-issue time
- **THEN** the migration SHALL preserve its prior-issuance exemption without inventing a date or granting clients control of the exemption

#### Scenario: Never-issued Cancelled invoice
- **WHEN** a Cancelled invoice has no trustworthy issuance evidence
- **THEN** cancellation or historical draft delivery alone SHALL NOT grant a prior-issuance exemption

### Requirement: Exhaustion is actionable without blocking drafts or old documents
An exhausted first-issue request SHALL return a stable allowance error with effective limit, usage, remaining allowance, and reset information, without publishing a clean PDF or attempting combined delivery. Draft editing/download and previously issued corrections/documents SHALL remain available.

#### Scenario: Free user attempts a third first issue
- **WHEN** Free has already used two units in the current UK calendar month
- **THEN** first issuance SHALL fail with `invoice_allowance_exhausted`, remaining zero, and the next reset instant
- **AND** draft data/PDF and delivery history SHALL remain unchanged
