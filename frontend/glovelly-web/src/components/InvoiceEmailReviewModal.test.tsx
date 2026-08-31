// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { createRef } from 'react'
import type { ComponentProps } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { InvoiceEmailReviewModal } from './InvoiceEmailReviewModal'
import type { Invoice, InvoiceEmailReview } from '../types'

const invoice: Invoice = {
  id: 'invoice-1',
  invoiceNumber: 'GLV-001',
  clientId: 'client-1',
  invoiceDate: '2026-08-30',
  dueDate: '2026-09-13',
  status: 'Draft',
  paidOn: null,
  lines: [],
  total: 100,
  deliveryCount: 0,
  lastDeliveryChannel: null,
  lastDeliveryRecipient: null,
  lastDeliveredUtc: null,
  lastDeliveredByUserId: null,
  description: null,
  pdfStorageKey: 'invoices/GLV-001.pdf',
  pdfFileName: 'GLV-001.pdf',
  pdfContentType: 'application/pdf',
  pdfSizeBytes: 1024,
  pdfGeneratedAt: '2026-08-30T12:00:00Z',
  documentState: 'Current',
  documentRevision: 1,
  pdfDocumentRevision: 1,
  documentFailureMessage: null,
  firstIssuedUtc: null,
  firstIssuedByUserId: null,
  reissueCount: 0,
  lastReissuedUtc: null,
  lastReissuedByUserId: null,
}

const review: InvoiceEmailReview = {
  recipientName: 'Fox & Finch Events',
  recipientEmail: 'bookings@example.com',
  subject: 'Invoice GLV-001',
  plainTextBody: 'Hello <client>,\n\nInvoice GLV-001 is attached.',
  pdfFileName: 'GLV-001.pdf',
  pdfSizeBytes: 1024,
  receiptCount: 1,
  receiptZipFileName: 'Invoice-GLV-001-Receipts.zip',
  receiptNote: 'Expense receipts are attached in a separate ZIP file.',
  additionalMessageHeading: 'Additional message:',
}

function renderModal(overrides: Partial<ComponentProps<typeof InvoiceEmailReviewModal>> = {}) {
  const props = {
    error: '',
    includeReceipts: false,
    invoice,
    isLoading: false,
    issueAfterSend: false,
    message: '',
    onClose: vi.fn(),
    onDownloadPdf: vi.fn(),
    onDownloadReceipts: vi.fn(),
    onIncludeReceiptsChange: vi.fn(),
    onIssueAfterSendChange: vi.fn(),
    onMessageChange: vi.fn(),
    onSend: vi.fn(),
    review,
    triggerRef: createRef<HTMLButtonElement>(),
    ...overrides,
  }
  render(<InvoiceEmailReviewModal {...props} />)
  return props
}

describe('InvoiceEmailReviewModal', () => {
  afterEach(cleanup)

  it('does not send when editing the message, pressing Enter, or cancelling', () => {
    const props = renderModal()
    const message = screen.getByTestId('invoice-email-review-message')

    fireEvent.change(message, { target: { value: 'A note' } })
    fireEvent.keyDown(message, { key: 'Enter' })
    fireEvent.click(screen.getAllByRole('button', { name: 'Cancel' })[0])

    expect(props.onMessageChange).toHaveBeenCalledWith('A note')
    expect(props.onSend).not.toHaveBeenCalled()
    expect(props.onClose).toHaveBeenCalledOnce()
  })

  it('requires the explicit send action and displays email text safely', () => {
    const props = renderModal()

    expect(screen.getByText('Hello <client>,', { exact: false })).toBeTruthy()
    fireEvent.click(screen.getByTestId('invoice-email-review-send-button'))

    expect(props.onSend).toHaveBeenCalledOnce()
  })

  it('disables duplicate submission while delivery is pending', () => {
    renderModal({ isLoading: true })

    expect(screen.getByTestId('invoice-email-review-send-button').getAttribute('disabled')).not.toBeNull()
    expect(screen.getByTestId('invoice-email-review-message').getAttribute('disabled')).not.toBeNull()
  })
})
