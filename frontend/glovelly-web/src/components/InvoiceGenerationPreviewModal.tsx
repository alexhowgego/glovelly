import type { Invoice } from '../types'

type InvoiceGenerationPreviewModalProps = {
  invoice: Invoice | null
  isOpen: boolean
  isLoading: boolean
  onClose: () => void
  onDownload: () => void
  onOpenInvoice: () => void
  pdfUrl: string | null
  status: string
}

export function InvoiceGenerationPreviewModal({
  invoice,
  isOpen,
  isLoading,
  onClose,
  onDownload,
  onOpenInvoice,
  pdfUrl,
  status,
}: InvoiceGenerationPreviewModalProps) {
  if (!isOpen || !invoice) {
    return null
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-modal="true"
        className="settings-modal invoice-generation-preview-modal panel"
        data-testid="invoice-preview-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Invoice Preview</p>
            <h2>{invoice.invoiceNumber}</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button">
            Close
          </button>
        </div>

        <div className="invoice-generation-preview-frame">
          {pdfUrl ? (
            <iframe data-testid="invoice-preview-frame" src={pdfUrl} title={`Invoice ${invoice.invoiceNumber} PDF preview`} />
          ) : (
            <div className="empty-state roomy">
              <strong>{isLoading ? 'Preparing invoice preview...' : 'Preview unavailable.'}</strong>
              <p>{status || 'The invoice was created, but the PDF could not be shown here.'}</p>
            </div>
          )}
        </div>

        <div className="expense-statement-footer">
          <span className="status-pill" data-testid="invoice-preview-status">{status || 'Invoice generated.'}</span>
          <div className="actions">
            <button
              className="ghost-button"
              disabled={isLoading || !pdfUrl}
              onClick={onDownload}
              type="button"
            >
              Download PDF
            </button>
            <button className="primary-button" data-testid="open-invoice-button" onClick={onOpenInvoice} type="button">
              Open invoice
            </button>
          </div>
        </div>
      </section>
    </div>
  )
}
