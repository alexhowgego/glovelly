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
  it('recognizes trusted legacy issuance without a fabricated first-issue timestamp', () => {
    expect(getInvoiceDocumentAvailability(invoice({
      status: 'Cancelled', hasBeenIssued: true, firstIssuedUtc: null, pdfHasDraftWatermark: false,
    })).isCurrent).toBe(true)
    expect(getInvoiceDocumentAvailability(invoice({
      status: 'Draft', hasBeenIssued: true, firstIssuedUtc: null, pdfHasDraftWatermark: false,
    })).isCurrent).toBe(false)
  })
  it('keeps never-issued cancelled invoices watermarked and allows clean previously issued cancellations', () => {
    expect(getInvoiceDocumentAvailability(invoice({ status: 'Cancelled', firstIssuedUtc: null, pdfHasDraftWatermark: false })).isCurrent).toBe(false)
    expect(getInvoiceDocumentAvailability(invoice({ status: 'Cancelled', firstIssuedUtc: null, pdfHasDraftWatermark: true })).isCurrent).toBe(true)
    expect(getInvoiceDocumentAvailability(invoice({ status: 'Cancelled', firstIssuedUtc: '2026-11-01T09:00:00Z', pdfHasDraftWatermark: false })).isCurrent).toBe(true)
    expect(getInvoiceDocumentAvailability(invoice({ isPdfCurrent: false })).isCurrent).toBe(false)
  })
  it.each([null, false])('blocks drafts with watermark provenance %s and offers explicit recovery', pdfHasDraftWatermark => {
    expect(getInvoiceDocumentAvailability(invoice({ status: 'Draft', pdfHasDraftWatermark }))).toMatchObject({
      isCurrent: false, canRetry: true, message: expect.stringContaining('Regenerate'),
    })
  })

  it('accepts watermarked drafts and legacy clean issued documents but rejects a watermarked issued document', () => {
    expect(getInvoiceDocumentAvailability(invoice({ status: 'Draft', pdfHasDraftWatermark: true })).isCurrent).toBe(true)
    expect(getInvoiceDocumentAvailability(invoice({ status: 'Issued', pdfHasDraftWatermark: null })).isCurrent).toBe(true)
    expect(getInvoiceDocumentAvailability(invoice({ status: 'Issued', pdfHasDraftWatermark: true }))).toMatchObject({
      isCurrent: false, canRetry: true,
    })
  })
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
