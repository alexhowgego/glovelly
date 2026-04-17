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
- issueDate
- dueDate
- status
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
