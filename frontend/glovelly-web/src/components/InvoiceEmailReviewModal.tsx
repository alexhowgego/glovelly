import { useEffect, useRef } from 'react'
import type { RefObject } from 'react'
import type { Invoice, InvoiceEmailReview } from '../types'
import { buildInvoiceEmailReviewPreview } from '../invoiceEmailReviewPreview'

type InvoiceEmailReviewModalProps = {
  error: string
  includeReceipts: boolean
  invoice: Invoice | null
  isLoading: boolean
  issueAfterSend: boolean
  message: string
  onClose: () => void
  onDownloadPdf: (invoice: Invoice) => void
  onDownloadReceipts: (invoice: Invoice) => void
  onIncludeReceiptsChange: (value: boolean) => void
  onIssueAfterSendChange: (value: boolean) => void
  onMessageChange: (value: string) => void
  onSend: () => void
  review: InvoiceEmailReview | null
  triggerRef: RefObject<HTMLButtonElement | null>
}

export function InvoiceEmailReviewModal({
  error,
  includeReceipts,
  invoice,
  isLoading,
  issueAfterSend,
  message,
  onClose,
  onDownloadPdf,
  onDownloadReceipts,
  onIncludeReceiptsChange,
  onIssueAfterSendChange,
  onMessageChange,
  onSend,
  review,
  triggerRef,
}: InvoiceEmailReviewModalProps) {
  const messageRef = useRef<HTMLTextAreaElement | null>(null)

  useEffect(() => {
    if (!invoice) {
      return
    }
    messageRef.current?.focus()
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isLoading) {
        event.preventDefault()
        onClose()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [invoice, isLoading, onClose])

  if (!invoice) {
    return null
  }

  const handleClose = () => {
    onClose()
    window.setTimeout(() => triggerRef.current?.focus(), 0)
  }

  return (
    <div className="settings-overlay" onClick={!isLoading ? handleClose : undefined} role="presentation">
      <section
        aria-labelledby="invoice-email-review-title"
        aria-modal="true"
        className="settings-modal invoice-email-review-modal panel"
        data-testid="invoice-email-review-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Invoice delivery</p>
            <h2 id="invoice-email-review-title">Review and send invoice</h2>
          </div>
          <button className="ghost-button" disabled={isLoading} onClick={handleClose} type="button">
            Cancel
          </button>
        </div>

        {error ? <div className="settings-note" role="alert">{error}</div> : null}
        {review ? (
          <div className="invoice-email-review-content">
            <div className="detail-grid">
              <article className="full-width"><p className="detail-label">To</p><strong>{review.recipientName}</strong><span>{review.recipientEmail}</span></article>
              <article className="full-width"><p className="detail-label">Subject</p><strong>{review.subject}</strong></article>
              <article className="full-width">
                <p className="detail-label">Attachments</p>
                <div className="invoice-email-review-attachments">
                  <div>
                    <button className="link-button" disabled={isLoading} onClick={() => onDownloadPdf(invoice)} type="button">
                      {review.pdfFileName}
                    </button>
                    <span>PDF</span>
                  </div>
                  {includeReceipts && review.receiptZipFileName ? (
                    <div>
                      <button className="link-button" disabled={isLoading} onClick={() => onDownloadReceipts(invoice)} type="button">
                        {review.receiptZipFileName}
                      </button>
                      <span>ZIP · {review.receiptCount} {review.receiptCount === 1 ? 'receipt' : 'receipts'}</span>
                    </div>
                  ) : null}
                </div>
              </article>
            </div>
            <label>
              Email message
              <textarea
                data-testid="invoice-email-review-message"
                ref={messageRef}
                value={message}
                onChange={(event) => onMessageChange(event.target.value)}
                placeholder="Optional note for the client"
                disabled={isLoading}
              />
            </label>
            <div className="gig-timeline-note">
              <p className="detail-label">Email preview</p>
              <pre className="invoice-email-review-body">
                {buildInvoiceEmailReviewPreview(review, message, includeReceipts)}
              </pre>
            </div>
            <label className="checkbox-field">
              <input checked={includeReceipts} disabled={isLoading} onChange={(event) => onIncludeReceiptsChange(event.target.checked)} type="checkbox" />
              <span>Include receipt attachments{review.receiptCount ? ` (${review.receiptCount})` : ''}</span>
            </label>
            {invoice.status === 'Draft' ? (
              <label className="checkbox-field">
                <input checked={issueAfterSend} disabled={isLoading} onChange={(event) => onIssueAfterSendChange(event.target.checked)} type="checkbox" />
                <span>Mark invoice as issued after sending</span>
              </label>
            ) : null}
          </div>
        ) : (
          <div className="empty-state roomy"><strong>{isLoading ? 'Preparing email review...' : 'Email review unavailable.'}</strong></div>
        )}

        <div className="expense-statement-footer">
          <span className="status-pill">{isLoading ? 'Preparing delivery...' : 'Review before sending.'}</span>
          <div className="actions">
            <button className="ghost-button" disabled={isLoading} onClick={handleClose} type="button">Cancel</button>
            <button className="primary-button" data-testid="invoice-email-review-send-button" disabled={isLoading || !review} onClick={onSend} type="button">
              Send invoice
            </button>
          </div>
        </div>
      </section>
    </div>
  )
}
