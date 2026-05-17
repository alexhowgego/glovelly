# Domain Model

## Client
- id
- name
- email
- billingAddress

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
- pdfBlob? (legacy fallback during migration)
- subtotal
- notes

Generated invoice PDFs are written through the domain-agnostic blob store using an invoice-layer key:

```text
users/{userId}/invoices/{invoiceId}/invoice.pdf
```

The `{userId}` segment uses the same dashless GUID format as receipt attachment keys. During the migration, download and delivery paths prefer `pdfStorageKey` and fall back to `pdfBlob` for older invoices. The backfill path is to enumerate invoices where `pdfBlob` is present and `pdfStorageKey` is null, save each blob through `IBlobStore` with the same key shape, populate the PDF metadata, verify downloads/delivery, and remove `PdfBlob` in a later cleanup.

## InvoiceLine
- id
- invoiceId
- description
- quantity
- unitPrice
- total

## Key Flow

Gig -> Generate Invoice -> InvoiceLines created -> Invoice tracked
