import type { Invoice } from './types'

export type InvoiceDocumentAvailability = {
  isCurrent: boolean
  message: string
  canRetry: boolean
}

export function getInvoiceDocumentAvailability(
  invoice: Invoice | null
): InvoiceDocumentAvailability {
  if (invoice?.documentState === 'Current' &&
      invoice.pdfDocumentRevision === invoice.documentRevision) {
    return { isCurrent: true, message: 'PDF current', canRetry: false }
  }

  if (invoice?.documentState === 'Regenerating') {
    return { isCurrent: false, message: 'PDF regenerating', canRetry: false }
  }

  return {
    isCurrent: false,
    message: invoice?.documentFailureMessage || 'PDF unavailable',
    canRetry: invoice?.documentState === 'Failed',
  }
}
