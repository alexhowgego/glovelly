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
- subtotal
- notes

## InvoiceLine
- id
- invoiceId
- description
- quantity
- unitPrice
- total

## Key Flow

Gig -> Generate Invoice -> InvoiceLines created -> Invoice tracked
