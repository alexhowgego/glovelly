# Domain Model

## Client
- id
- name
- email
- billingAddress

Client deletion is intentionally narrow: the UI and API block deletion while the
client has any gig or invoice records. When deletion is available, the user must
confirm the action before the record is removed.

## Gig
- id
- clientId
- invoiceId?
- sourceImportBatchId?
- sourceImportDraftId?
- title
- date
- venue
- fee
- travelMiles
- notes
- wasDriving
- status
- invoicedAt?
- isInvoiced (derived from invoiceId)

## GigImportBatch
- id
- sourceName
- sourceFingerprint?
- status (`Draft`, `Committed`, `Abandoned`)
- createdAtUtc
- createdByUserId?
- notes?

Gig import batches are staging containers for AI/MCP-extracted candidate gigs. They are user-scoped and intentionally separate extraction from production gig records.

## GigImportDraft
- id
- batchId
- proposedClientId?
- proposed client/contact/project fields
- proposed title/date/time/venue/fee/per-diem fields
- notes/accommodation/travel/source reference
- confidence (`Low`, `Medium`, `High`)
- warnings
- status (`Pending`, `Accepted`, `Rejected`, `Committed`)

Draft rows can be incomplete. The user reviews them in the Imported gigs modal. Edits autosave, accepted rows can be committed into real gigs, and rejected rows are deleted from the import when decisions are committed. Pending rows remain staged for later review. Created gigs keep source import IDs for auditability and duplicate protection.

## Invoice
- id
- invoiceNumber
- clientId
- invoiceDate (last issue/re-issue date shown on the invoice)
- dueDate
- status
- firstIssuedUtc?
- firstIssuedByUserId?
- reissueCount
- lastReissuedUtc?
- lastReissuedByUserId?
- pdfStorageKey?
- pdfFileName?
- pdfContentType?
- pdfSizeBytes?
- pdfGeneratedAt?
- subtotal
- notes

Generated invoice PDFs are written through the domain-agnostic blob store using an invoice-layer key:

```text
users/{userId}/invoices/{invoiceId}/invoice.pdf
```

The `{userId}` segment uses the same dashless GUID format as receipt attachment keys. Download and delivery paths read PDFs from blob storage through `pdfStorageKey`.

## InvoiceLine
- id
- invoiceId
- description
- quantity
- unitPrice
- total

## Key Flow

Gig -> Generate Invoice -> InvoiceLines created -> Invoice tracked

MCP extraction -> GigImportBatch/GigImportDraft staging -> user review -> accepted rows commit to Gig records
