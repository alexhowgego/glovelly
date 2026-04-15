# Domain Model (v0)

## Client
- id
- name
- email
- billingAddress

## Gig
- id
- clientId
- title
- date
- venue
- fee
- travelMiles
- notes
- invoiced (bool)

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