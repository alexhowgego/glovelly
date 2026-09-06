import { describe, expect, it } from 'vitest'
import { buildInvoiceEmailReviewPreview } from './invoiceEmailReviewPreview'
import type { InvoiceEmailReview } from './types'

const review: InvoiceEmailReview = {
  recipientName: 'Fox & Finch Events',
  recipientEmail: 'bookings@example.com',
  subject: 'Invoice GLV-001',
  plainTextBody: 'Hello Fox & Finch,\n\nInvoice GLV-001 is attached as a PDF.\n',
  pdfFileName: 'GLV-001.pdf',
  pdfSizeBytes: 1024,
  receiptCount: 1,
  receiptZipFileName: 'Invoice-GLV-001-Receipts.zip',
  receiptNote: 'Expense receipts are attached in a separate ZIP file.',
  additionalMessageHeading: 'Additional message:',
}

describe('buildInvoiceEmailReviewPreview', () => {
  it('updates the additional message synchronously', () => {
    expect(buildInvoiceEmailReviewPreview(review, 'Please process this week.', false)).toContain(
      'Additional message:\nPlease process this week.'
    )
  })

  it('adds the receipt note before the additional message', () => {
    expect(buildInvoiceEmailReviewPreview(review, 'Thanks.', true)).toBe(
      'Hello Fox & Finch,\n\nInvoice GLV-001 is attached as a PDF.\nExpense receipts are attached in a separate ZIP file.\n\nAdditional message:\nThanks.'
    )
  })
})
