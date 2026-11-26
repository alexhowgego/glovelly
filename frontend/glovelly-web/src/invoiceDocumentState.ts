import type { Invoice } from './types'

export type InvoiceDocumentAvailability = {
  isCurrent: boolean
  message: string
  canRetry: boolean
}

export function getInvoiceDocumentAvailability(
  invoice: Invoice | null
): InvoiceDocumentAvailability {
  const requiresWatermark = invoice?.status === 'Draft' ||
    (invoice?.status === 'Cancelled' && !(invoice.hasBeenIssued ?? Boolean(invoice.firstIssuedUtc)))
  const compatibleWatermark = requiresWatermark
    ? invoice?.pdfHasDraftWatermark === true
    : invoice?.pdfHasDraftWatermark !== true
  if (invoice?.documentState === 'Current' &&
      invoice.pdfDocumentRevision === invoice.documentRevision &&
      invoice.isPdfCurrent !== false && compatibleWatermark) {
    return { isCurrent: true, message: 'PDF current', canRetry: false }
  }

  if (invoice?.documentState === 'Regenerating') {
    return { isCurrent: false, message: 'PDF regenerating', canRetry: false }
  }

  if (invoice && !compatibleWatermark) {
    return {
      isCurrent: false,
      message: invoice.documentFailureMessage || 'Regenerate this invoice PDF to match its watermark status.',
      canRetry: true,
    }
  }

  return {
    isCurrent: false,
    message: invoice?.documentFailureMessage || 'PDF unavailable',
    canRetry: invoice?.documentState === 'Failed',
  }
}
