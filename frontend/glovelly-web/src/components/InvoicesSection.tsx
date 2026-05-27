import {
  formatCurrency,
  formatDate,
  formatDateTime,
  getAllowedInvoiceStatusTransitions,
} from '../formatters'
import type { Invoice, InvoiceStatus } from '../types'

type InvoicesSectionProps = {
  adjustmentAmount: string
  adjustmentReason: string
  clientNamesById: ReadonlyMap<string, string>
  draftInvoiceCount: number
  filteredInvoices: Invoice[]
  googleDrivePublishLink: { href: string; fileName: string | null } | null
  isEditorOpen: boolean
  invoiceSearchQuery: string
  invoiceStatus: string
  invoices: Invoice[]
  issuedInvoiceCount: number
  isGoogleDriveConnected: boolean
  isInvoiceLoading: boolean
  isSellerProfileConfigured: boolean
  onAdjustmentAmountChange: (value: string) => void
  onAdjustmentReasonChange: (value: string) => void
  onAddAdjustment: (invoice: Invoice) => Promise<void>
  onCloseEditor: () => void
  onDeleteInvoice: (invoice: Invoice) => Promise<void>
  onDownloadPdf: (invoice: Invoice) => Promise<void>
  onInvoiceStatusChange: (invoice: Invoice, status: InvoiceStatus) => Promise<Invoice | null>
  onOpenClient: (clientId: string) => void
  onOpenGig: (gigId: string) => void
  onOpenSellerProfile: () => void
  onPreviewPdf: (invoice: Invoice) => Promise<void>
  onPublishGoogleDrive: (invoice: Invoice) => Promise<Invoice | null>
  onReissue: (invoice: Invoice) => Promise<Invoice | null>
  onSendEmail: (invoice: Invoice) => Promise<Invoice | null>
  onSearchQueryChange: (value: string) => void
  onSelectInvoice: (invoiceId: string) => void
  onStartEditing: () => void
  sellerProfileNotice: string
  selectedInvoice: Invoice | null
}

export function InvoicesSection({
  adjustmentAmount,
  adjustmentReason,
  clientNamesById,
  draftInvoiceCount,
  filteredInvoices,
  googleDrivePublishLink,
  isEditorOpen,
  invoiceSearchQuery,
  invoiceStatus,
  invoices,
  issuedInvoiceCount,
  isGoogleDriveConnected,
  isInvoiceLoading,
  isSellerProfileConfigured,
  onAdjustmentAmountChange,
  onAdjustmentReasonChange,
  onAddAdjustment,
  onCloseEditor,
  onDeleteInvoice,
  onDownloadPdf,
  onInvoiceStatusChange,
  onOpenClient,
  onOpenGig,
  onOpenSellerProfile,
  onPreviewPdf,
  onPublishGoogleDrive,
  onReissue,
  onSendEmail,
  onSearchQueryChange,
  onSelectInvoice,
  onStartEditing,
  sellerProfileNotice,
  selectedInvoice,
}: InvoicesSectionProps) {
  const selectedInvoiceClientName =
    (selectedInvoice ? clientNamesById.get(selectedInvoice.clientId) : null) ??
    'Unknown client'

  return (
    <section className="section-layout">
      <div className="gig-workspace">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Billing</p>
              <h2>Invoices</h2>
            </div>
            <span className="status-pill" data-testid="invoice-status">
              <span>{invoiceStatus}</span>
              {googleDrivePublishLink ? (
                <a
                  href={googleDrivePublishLink.href}
                  target="_blank"
                  rel="noreferrer"
                  title={
                    googleDrivePublishLink.fileName
                      ? `Open ${googleDrivePublishLink.fileName} in Google Drive`
                      : 'Open file in Google Drive'
                  }
                >
                  Open file
                </a>
              ) : null}
            </span>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              data-testid="invoice-search-input"
              type="search"
              placeholder="Invoice number, client or description..."
              value={invoiceSearchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
            />
          </label>

          <div className="gig-summary-grid">
            <article>
              <span>{invoices.length}</span>
              <p>saved invoices</p>
            </article>
            <article>
              <span>{draftInvoiceCount}</span>
              <p>draft</p>
            </article>
            <article>
              <span>{issuedInvoiceCount}</span>
              <p>issued</p>
            </article>
          </div>

          <div className="client-list">
            {filteredInvoices.map((invoice) => {
              const clientName = clientNamesById.get(invoice.clientId) ?? 'Unknown client'

              return (
                <button
                  key={invoice.id}
                  className={`client-card ${selectedInvoice?.id === invoice.id ? 'selected' : ''}`}
                  data-testid="invoice-card"
                  onClick={() => onSelectInvoice(invoice.id)}
                  type="button"
                >
                  <div>
                    <strong>{invoice.invoiceNumber}</strong>
                    <span>{clientName}</span>
                  </div>
                  <small className="gig-card-meta">
                    {formatDate(invoice.invoiceDate)} · {invoice.status}
                  </small>
                  <small className="gig-card-meta">{formatCurrency(invoice.total)}</small>
                </button>
              )
            })}

            {filteredInvoices.length === 0 && (
              <div className="empty-state">
                <strong>No invoices yet.</strong>
                <p>Generate one from a gig and it will appear here immediately.</p>
              </div>
            )}
          </div>
        </div>

        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Invoice Overview</p>
              <h2>{selectedInvoice?.invoiceNumber ?? 'No invoice selected'}</h2>
            </div>
            <div className="actions">
              <button
                className="ghost-button"
                onClick={onStartEditing}
                type="button"
                disabled={!selectedInvoice}
              >
                Line items
              </button>
              <button
                className="ghost-button"
                onClick={() => selectedInvoice && void onReissue(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading || selectedInvoice.status === 'Cancelled'}
                title={
                  selectedInvoice?.status === 'Cancelled'
                    ? 'Move cancelled invoices back to Draft before redrafting.'
                    : undefined
                }
              >
                {selectedInvoice?.status === 'Draft' ? 'Redraft' : 'Re-issue'}
              </button>
              <button
                className="ghost-button"
                data-testid="invoice-preview-button"
                onClick={() => selectedInvoice && void onPreviewPdf(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading}
              >
                Preview
              </button>
              <button
                className="ghost-button"
                onClick={() => selectedInvoice && void onPublishGoogleDrive(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading || !isGoogleDriveConnected}
                title={
                  isGoogleDriveConnected
                    ? undefined
                    : 'Connect Google Drive from your profile menu first.'
                }
              >
                Publish to Drive
              </button>
              <button
                className="ghost-button"
                data-testid="invoice-send-button"
                onClick={() => selectedInvoice && void onSendEmail(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading}
              >
                Send to client
              </button>
              <button
                className="danger-button"
                onClick={() => selectedInvoice && void onDeleteInvoice(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading || selectedInvoice?.status !== 'Draft'}
                title={
                  selectedInvoice?.status !== 'Draft'
                    ? 'Only Draft invoices can be deleted.'
                    : 'Delete draft invoice'
                }
              >
                Delete draft
              </button>
              <button
                className="primary-button"
                onClick={() => selectedInvoice && void onDownloadPdf(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading}
              >
                Download PDF
              </button>
            </div>
          </div>

          {selectedInvoice ? (
            <>
              {!isSellerProfileConfigured && (
                <div className="settings-note">
                  {sellerProfileNotice}
                  {' '}
                  <button
                    className="ghost-button"
                    onClick={onOpenSellerProfile}
                    type="button"
                  >
                    Set up seller profile
                  </button>
                </div>
              )}

              <div className="detail-grid">
                <article>
                  <p className="detail-label">Client</p>
                  <button
                    className="link-button detail-link"
                    onClick={() => onOpenClient(selectedInvoice.clientId)}
                    type="button"
                  >
                    {selectedInvoiceClientName}
                  </button>
                </article>
                <article>
                  <p className="detail-label">Status</p>
                  <div className="field-with-inline-help">
                    <select
                      value={selectedInvoice.status}
                      onChange={(event) =>
                        void onInvoiceStatusChange(
                          selectedInvoice,
                          event.target.value as InvoiceStatus
                        )
                      }
                      disabled={isInvoiceLoading}
                    >
                      <option value={selectedInvoice.status}>{selectedInvoice.status}</option>
                      {getAllowedInvoiceStatusTransitions(selectedInvoice.status).map(
                        (statusOption) => (
                          <option key={statusOption} value={statusOption}>
                            {statusOption}
                          </option>
                        )
                      )}
                    </select>
                  </div>
                </article>
                <article>
                  <p className="detail-label">Invoice date</p>
                  <strong>{formatDate(selectedInvoice.invoiceDate)}</strong>
                </article>
                <article>
                  <p className="detail-label">Due date</p>
                  <strong>{formatDate(selectedInvoice.dueDate)}</strong>
                </article>
                <article>
                  <p className="detail-label">First issued on</p>
                  <strong>
                    {selectedInvoice.firstIssuedUtc
                      ? formatDateTime(selectedInvoice.firstIssuedUtc)
                      : 'Not issued'}
                  </strong>
                </article>
                <article>
                  <p className="detail-label">Re-issued</p>
                  <strong>
                    {selectedInvoice.reissueCount}{' '}
                    {selectedInvoice.reissueCount === 1 ? 'time' : 'times'}
                  </strong>
                </article>
                <article className="full-width">
                  <p className="detail-label">In respect of</p>
                  <span>{selectedInvoice.description?.trim() || 'No description set.'}</span>
                </article>
                <article>
                  <p className="detail-label">Total</p>
                  <strong>{formatCurrency(selectedInvoice.total)}</strong>
                </article>
                <article>
                  <p className="detail-label">Line items</p>
                  <strong>{selectedInvoice.lines.length}</strong>
                </article>
                <article>
                  <p className="detail-label">Deliveries</p>
                  <strong>{selectedInvoice.deliveryCount}</strong>
                </article>
                <article>
                  <p className="detail-label">Last delivery</p>
                  <strong>{formatDateTime(selectedInvoice.lastDeliveredUtc)}</strong>
                  {selectedInvoice.lastDeliveryRecipient ? (
                    <span>{selectedInvoice.lastDeliveryRecipient}</span>
                  ) : null}
                </article>
              </div>

              <div className="gig-timeline-note">
                <p className="detail-label">Invoice workflow</p>
                <span>
                  Review invoice details here, then open line items when you need to make changes.
                </span>
              </div>
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select an invoice to review its details.</strong>
              <p>The generated PDF and line items will be available here.</p>
            </div>
          )}
        </div>

        <div className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
          <div aria-hidden={!isEditorOpen} className="panel editor-panel">
            <div className="panel-heading">
              <div>
                <p className="section-label">Management Pane</p>
                <h2>{selectedInvoice ? 'Line items' : 'No invoice selected'}</h2>
              </div>
              <div className="actions">
                <button className="ghost-button" onClick={onCloseEditor} type="button">
                  Done
                </button>
              </div>
            </div>

            {selectedInvoice ? (
              <>
                <div className="gig-timeline-note">
                  <p className="detail-label">Adjustments</p>
                  <span>
                    Add adjustments as separate line items so the invoice stays clear and easy to follow.
                  </span>
                </div>

                <form
                  className="invoice-adjustment-form"
                  onSubmit={(event) => {
                    event.preventDefault()
                    void onAddAdjustment(selectedInvoice)
                  }}
                >
                  <label>
                    Amount
                    <input
                      type="number"
                      step="0.01"
                      value={adjustmentAmount}
                      onChange={(event) => onAdjustmentAmountChange(event.target.value)}
                      placeholder="-25.00"
                      disabled={isInvoiceLoading}
                    />
                  </label>
                  <label>
                    Reason
                    <input
                      value={adjustmentReason}
                      onChange={(event) => onAdjustmentReasonChange(event.target.value)}
                      placeholder="Goodwill discount, surcharge, correction..."
                      disabled={isInvoiceLoading}
                    />
                  </label>
                  <button className="ghost-button" type="submit" disabled={isInvoiceLoading}>
                    Add adjustment
                  </button>
                </form>

                <div className="invoice-line-list">
                  {selectedInvoice.lines
                    .slice()
                    .sort((left, right) => left.sortOrder - right.sortOrder)
                    .map((line) => {
                      const gigId = line.gigId

                      return (
                        <div className="invoice-line-item" key={line.id}>
                          <div>
                            {gigId ? (
                              <button
                                className="link-button invoice-line-link"
                                onClick={() => onOpenGig(gigId)}
                                type="button"
                              >
                                {line.description}
                              </button>
                            ) : (
                              <strong>{line.description}</strong>
                            )}
                            <span>
                              {line.type} · {line.quantity} x {formatCurrency(line.unitPrice)}
                            </span>
                            {line.type === 'ManualAdjustment' ? (
                              <span>Audit: {formatDateTime(line.createdUtc)}</span>
                            ) : null}
                          </div>
                          <strong>{formatCurrency(line.lineTotal)}</strong>
                        </div>
                      )
                    })}
                </div>
              </>
            ) : (
              <div className="empty-state roomy">
                <strong>Select an invoice to inspect its line items.</strong>
                <p>Line items and adjustments will appear here.</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  )
}
