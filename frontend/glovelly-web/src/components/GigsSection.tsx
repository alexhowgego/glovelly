import type { FormEvent } from 'react'
import { formatCurrency, formatDate, formatGigStatus } from '../formatters'
import type { Client, Gig, GigExpenseForm, GigForm, GigStatus, SellerProfile } from '../types'

type GigsSectionProps = {
  clientNamesById: ReadonlyMap<string, string>
  clients: Client[]
  completedGigCount: number
  filteredGigs: Gig[]
  gigExpenseAmount: string
  gigExpenseDescription: string
  gigForm: GigForm
  isEditorOpen: boolean
  gigMode: 'create' | 'edit'
  gigSearchQuery: string
  gigStatus: string
  gigs: Gig[]
  isGigLoading: boolean
  isInvoiceLoading: boolean
  onAddGigExpense: () => void
  onCloseEditor: () => void
  onExpenseAmountChange: (value: string) => void
  onExpenseDescriptionChange: (value: string) => void
  onGenerateInvoice: () => void
  onDownloadExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onOpenLinkedInvoice: () => void
  onOpenSellerProfile: () => void
  onUploadExpenseAttachment: (index: number, file: File) => void
  onDeleteExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onRemoveGigExpense: (index: number) => void
  onResetForm: () => void
  onSearchQueryChange: (value: string) => void
  onSelectGig: (gigId: string) => void
  onToggleGigSelection: (gigId: string) => void
  onStartEditing: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateGigExpenseField: (
    index: number,
    field: keyof Pick<GigExpenseForm, 'description' | 'amount'>,
    value: string
  ) => void
  onUpdateGigField: (
    field: keyof GigForm,
    value: string | boolean | GigExpenseForm[]
  ) => void
  plannedGigCount: number
  sellerProfile: SellerProfile
  sellerProfileNotice: string
  selectedGig: Gig | null
  selectedGigIds: string[]
  selectedGigs: Gig[]
}

export function GigsSection({
  clientNamesById,
  clients,
  completedGigCount,
  filteredGigs,
  gigExpenseAmount,
  gigExpenseDescription,
  gigForm,
  isEditorOpen,
  gigMode,
  gigSearchQuery,
  gigStatus,
  gigs,
  isGigLoading,
  isInvoiceLoading,
  onAddGigExpense,
  onCloseEditor,
  onExpenseAmountChange,
  onExpenseDescriptionChange,
  onGenerateInvoice,
  onDownloadExpenseAttachment,
  onOpenLinkedInvoice,
  onOpenSellerProfile,
  onUploadExpenseAttachment,
  onDeleteExpenseAttachment,
  onRemoveGigExpense,
  onResetForm,
  onSearchQueryChange,
  onSelectGig,
  onToggleGigSelection,
  onStartEditing,
  onSubmit,
  onUpdateGigExpenseField,
  onUpdateGigField,
  plannedGigCount,
  sellerProfile,
  sellerProfileNotice,
  selectedGig,
  selectedGigIds,
  selectedGigs,
}: GigsSectionProps) {
  const selectedGigClientName =
    (selectedGig ? clientNamesById.get(selectedGig.clientId) : null) ?? 'Unknown client'
  const hasCrossClientSelection = new Set(selectedGigs.map((gig) => gig.clientId)).size > 1

  return (
    <section className="section-layout">
      <div className="gig-workspace">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Bookings</p>
              <h2>Gigs</h2>
            </div>
            <button className="ghost-button" onClick={onResetForm} type="button">
              New gig
            </button>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              type="search"
              placeholder="Client, title, venue..."
              value={gigSearchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
            />
          </label>

          <div className="gig-summary-grid">
            <article>
              <span>{gigs.length}</span>
              <p>saved gigs</p>
            </article>
            <article>
              <span>{plannedGigCount}</span>
              <p>planned</p>
            </article>
            <article>
              <span>{completedGigCount}</span>
              <p>completed</p>
            </article>
          </div>

          <div className="client-list">
            {filteredGigs.map((gig) => {
              const clientName = clientNamesById.get(gig.clientId) ?? 'Unknown client'

              return (
                <button
                  key={gig.id}
                  className={`client-card ${selectedGig?.id === gig.id ? 'selected' : ''}`}
                  onClick={() => onSelectGig(gig.id)}
                  type="button"
                >
                  <label
                    className="gig-select-toggle"
                    onClick={(event) => event.stopPropagation()}
                  >
                    <input
                      type="checkbox"
                      checked={selectedGigIds.includes(gig.id)}
                      disabled={gig.isInvoiced}
                      onChange={() => onToggleGigSelection(gig.id)}
                    />
                    <span>{gig.isInvoiced ? 'Invoiced' : 'Select'}</span>
                  </label>
                  <div>
                    <strong>{gig.title}</strong>
                    <span>{clientName}</span>
                  </div>
                  <small className="gig-card-meta">
                    {formatDate(gig.date)} · {gig.venue}
                  </small>
                  <small className="gig-card-meta">
                    {formatCurrency(gig.fee)} · {formatGigStatus(gig.status)}
                  </small>
                </button>
              )
            })}

            {filteredGigs.length === 0 && (
              <div className="empty-state">
                <strong>No gigs match that search.</strong>
                <p>Create the first gig or try a different term.</p>
              </div>
            )}
          </div>
        </div>

        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Gig Overview</p>
              <h2>{selectedGig?.title ?? 'No gig selected'}</h2>
            </div>
            <div className="actions">
              <button
                className="primary-button"
                onClick={onGenerateInvoice}
                type="button"
                disabled={
                  isInvoiceLoading ||
                  (selectedGigIds.length === 0 &&
                    (!selectedGig || selectedGig.isInvoiced)) ||
                  hasCrossClientSelection
                }
              >
                {selectedGigIds.length > 0
                  ? `Generate invoice (${selectedGigIds.length})`
                  : selectedGig?.isInvoiced
                    ? 'Already invoiced'
                    : 'Generate invoice'}
              </button>
              <button
                className="ghost-button"
                onClick={onStartEditing}
                type="button"
                disabled={!selectedGig}
              >
                Edit gig
              </button>
            </div>
          </div>

          {selectedGig ? (
            <>
              <div className="detail-grid">
                <article>
                  <p className="detail-label">Client</p>
                  <strong>{selectedGigClientName}</strong>
                </article>
                <article>
                  <p className="detail-label">Status</p>
                  <strong>{formatGigStatus(selectedGig.status)}</strong>
                </article>
                <article>
                  <p className="detail-label">Date</p>
                  <strong>{formatDate(selectedGig.date)}</strong>
                </article>
                <article>
                  <p className="detail-label">Fee</p>
                  <strong>{formatCurrency(selectedGig.fee)}</strong>
                </article>
                <article className="full-width">
                  <p className="detail-label">Location</p>
                  <strong>{selectedGig.venue}</strong>
                </article>
                <article>
                  <p className="detail-label">Driving</p>
                  <strong>{selectedGig.wasDriving ? 'Yes' : 'No'}</strong>
                </article>
                <article>
                  <p className="detail-label">Invoice link</p>
                  {selectedGig.isInvoiced ? (
                    <button className="ghost-button" onClick={onOpenLinkedInvoice} type="button">
                      Open invoice
                    </button>
                  ) : (
                    <strong>Not invoiced yet</strong>
                  )}
                </article>
                <article className="full-width">
                  <p className="detail-label">Notes</p>
                  <span>{selectedGig.notes?.trim() || 'No notes yet.'}</span>
                </article>
              </div>

              <div className="gig-timeline-note">
                <p className="detail-label">Invoice workflow</p>
                <span>
                  {selectedGig.isInvoiced
                    ? 'This gig is already linked to an invoice. Open Invoices to review or download it.'
                    : 'This gig has everything needed to create an invoice when you are ready.'}
                </span>
                <span>{sellerProfileNotice}</span>
                {selectedGigIds.length > 0 && (
                  <span>
                    {hasCrossClientSelection
                      ? ' Selected gigs need to belong to the same client before they can be invoiced together.'
                      : ` ${selectedGigIds.length} gig(s) selected for a combined invoice.`}
                  </span>
                )}
                <button
                  className="ghost-button"
                  onClick={onOpenSellerProfile}
                  type="button"
                >
                  {sellerProfile.isConfigured ? 'Review seller profile' : 'Set up seller profile'}
                </button>
              </div>
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a gig to review its details.</strong>
              <p>Key booking and billing details will appear here.</p>
            </div>
          )}
        </div>

        <div className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
          <form
            aria-hidden={!isEditorOpen}
            className="editor-panel panel"
            onSubmit={onSubmit}
          >
            <div className="panel-heading">
              <div>
                <p className="section-label">Management Pane</p>
                <h2>{gigMode === 'create' ? 'Create gig' : 'Update gig'}</h2>
              </div>
              <span className="status-pill">{gigStatus}</span>
            </div>

            <div className="form-grid">
              <label>
                <span>Client</span>
                <select
                  required
                  value={gigForm.clientId}
                  onChange={(event) => onUpdateGigField('clientId', event.target.value)}
                >
                  <option value="">Select a client</option>
                  {clients.map((client) => (
                    <option key={client.id} value={client.id}>
                      {client.name}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                <span>Date</span>
                <input
                  required
                  type="date"
                  value={gigForm.date}
                  onChange={(event) => onUpdateGigField('date', event.target.value)}
                />
              </label>

              <label className="full-width">
                <span>Title / description</span>
                <input
                  required
                  value={gigForm.title}
                  onChange={(event) => onUpdateGigField('title', event.target.value)}
                  placeholder="Spring product launch"
                />
              </label>

              <label className="full-width">
                <span>Location / venue</span>
                <input
                  required
                  value={gigForm.venue}
                  onChange={(event) => onUpdateGigField('venue', event.target.value)}
                  placeholder="Albert Hall, Manchester"
                />
              </label>

              <label>
                <span>Fee</span>
                <input
                  required
                  inputMode="decimal"
                  value={gigForm.fee}
                  onChange={(event) => onUpdateGigField('fee', event.target.value)}
                  placeholder="650"
                />
              </label>

              <label>
                <span>Status</span>
                <select
                  value={gigForm.status}
                  onChange={(event) =>
                    onUpdateGigField('status', event.target.value as GigStatus)
                  }
                >
                  <option value="Confirmed">Planned</option>
                  <option value="Completed">Completed</option>
                  <option value="Cancelled">Cancelled</option>
                  <option value="Draft">Draft</option>
                </select>
              </label>

              <label className="checkbox-field full-width">
                <input
                  type="checkbox"
                  checked={gigForm.wasDriving}
                  onChange={(event) => onUpdateGigField('wasDriving', event.target.checked)}
                />
                <span>I was driving for this gig</span>
              </label>

              <label className="full-width">
                <span>Notes</span>
                <textarea
                  rows={5}
                  value={gigForm.notes}
                  onChange={(event) => onUpdateGigField('notes', event.target.value)}
                  placeholder="Optional commercial or logistics notes"
                />
              </label>
            </div>

            <div className="gig-timeline-note">
              <p className="detail-label">Expenses</p>
              <span>
                Add chargeable out-of-pocket costs here and they will flow through when the gig is invoiced.
              </span>
            </div>

            <div className="invoice-adjustment-form">
              <label>
                Amount
                <input
                  inputMode="decimal"
                  value={gigExpenseAmount}
                  onChange={(event) => onExpenseAmountChange(event.target.value)}
                  placeholder="45.00"
                  disabled={isGigLoading}
                />
              </label>
              <label>
                Description
                <input
                  value={gigExpenseDescription}
                  onChange={(event) => onExpenseDescriptionChange(event.target.value)}
                  placeholder="Parking, hotel, equipment hire..."
                  disabled={isGigLoading}
                />
              </label>
              <button
                className="ghost-button"
                onClick={onAddGigExpense}
                type="button"
                disabled={isGigLoading}
              >
                Add expense
              </button>
            </div>

            {gigForm.expenses.length > 0 ? (
              <div className="gig-expense-list">
                {gigForm.expenses.map((expense, index) => (
                  <div className="gig-expense-item" key={`${expense.id || 'new'}-${index}`}>
                    <label>
                      <span>Description</span>
                      <input
                        value={expense.description}
                        onChange={(event) =>
                          onUpdateGigExpenseField(index, 'description', event.target.value)
                        }
                        disabled={isGigLoading}
                      />
                    </label>
                    <label>
                      <span>Amount</span>
                      <input
                        inputMode="decimal"
                        value={expense.amount}
                        onChange={(event) =>
                          onUpdateGigExpenseField(index, 'amount', event.target.value)
                        }
                        disabled={isGigLoading}
                      />
                    </label>
                    <button
                      className="ghost-button"
                      onClick={() => onRemoveGigExpense(index)}
                      type="button"
                      disabled={isGigLoading}
                    >
                      Remove
                    </button>
                    <div className="expense-attachments">
                      <div className="expense-attachment-header">
                        <span>
                          {expense.attachments.length === 1
                            ? '1 receipt'
                            : `${expense.attachments.length} receipts`}
                        </span>
                        <label className="ghost-button file-button">
                          Add receipt
                          <input
                            type="file"
                            accept="application/pdf,image/jpeg,image/png,image/webp,image/heic,image/heif"
                            disabled={isGigLoading || !expense.id}
                            onChange={(event) => {
                              const file = event.target.files?.[0]
                              event.target.value = ''
                              if (file) {
                                onUploadExpenseAttachment(index, file)
                              }
                            }}
                          />
                        </label>
                      </div>
                      {expense.id ? (
                        expense.attachments.length > 0 ? (
                          <div className="expense-attachment-list">
                            {expense.attachments.map((attachment) => (
                              <div className="expense-attachment-item" key={attachment.id}>
                                <button
                                  className="link-button"
                                  type="button"
                                  onClick={() => onDownloadExpenseAttachment(expense, attachment.id)}
                                  disabled={isGigLoading}
                                >
                                  {attachment.fileName}
                                </button>
                                <button
                                  className="ghost-button"
                                  type="button"
                                  onClick={() => onDeleteExpenseAttachment(expense, attachment.id)}
                                  disabled={isGigLoading}
                                >
                                  Delete
                                </button>
                              </div>
                            ))}
                          </div>
                        ) : null
                      ) : (
                        <p className="attachment-helper">Save the gig before adding receipts.</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="empty-state">
                <strong>No expenses added yet.</strong>
                <p>Use the fields above to add chargeable costs to this gig.</p>
              </div>
            )}

            <div className="form-actions">
              <button
                className="primary-button"
                data-close-after-save="true"
                type="submit"
                disabled={isGigLoading || clients.length === 0}
              >
                Save and close
              </button>
              <button
                className="ghost-button"
                data-close-after-save="false"
                type="submit"
                disabled={isGigLoading || clients.length === 0}
              >
                Save
              </button>
              <button className="ghost-button" onClick={onCloseEditor} type="button">
                Discard changes
              </button>
            </div>

            {clients.length === 0 && (
              <p className="auth-note">
                Add a client first so this gig can be linked to the right account.
              </p>
            )}
          </form>
        </div>
      </div>
    </section>
  )
}
