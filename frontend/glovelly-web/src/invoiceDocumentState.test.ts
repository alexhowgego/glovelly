import { describe, expect, it } from 'vitest'
import { getInvoiceDocumentAvailability } from './invoiceDocumentState'
import type { Invoice } from './types'

const invoice = (overrides: Partial<Invoice> = {}) =>
  ({
    id: 'invoice-1',
    documentState: 'Current',
    documentRevision: 2,
    pdfDocumentRevision: 2,
    documentFailureMessage: null,
    ...overrides,
  }) as Invoice

describe('getInvoiceDocumentAvailability', () => {
  it('enables document actions only for a matching current PDF', () => {
    expect(getInvoiceDocumentAvailability(invoice())).toEqual({
      isCurrent: true,
      message: 'PDF current',
      canRetry: false,
    })
    expect(getInvoiceDocumentAvailability(invoice({ pdfDocumentRevision: 1 }))).toMatchObject({
      isCurrent: false,
      canRetry: false,
    })
  })

  it('explains a failed document and exposes inline recovery', () => {
    expect(getInvoiceDocumentAvailability(invoice({
      documentState: 'Failed',
      documentFailureMessage: 'Invoice PDF could not be regenerated. Try again.',
    }))).toEqual({
      isCurrent: false,
      message: 'Invoice PDF could not be regenerated. Try again.',
      canRetry: true,
    })
  })
})
