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
