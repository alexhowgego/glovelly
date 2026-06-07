import type { FormEvent } from 'react'
import { formatCurrency, formatDate, formatGigStatus } from '../formatters'
import type {
  Client,
  Gig,
  GigExpenseForm,
  GigExpenseReimbursementStatus,
  GigForm,
  GigSort,
  GigSortKey,
  GigStatus,
  SellerProfile,
} from '../types'

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
  gigSort: GigSort
  gigStatus: string
  gigs: Gig[]
  isGigLoading: boolean
  isInvoiceLoading: boolean
  isMileageEstimating: boolean
  onAddGigExpense: () => void
  onCloseEditor: () => void
  onExpenseAmountChange: (value: string) => void
  onExpenseDescriptionChange: (value: string) => void
  onGenerateExpenseStatement: () => void
  onGenerateInvoice: () => void
  onEstimateMileage: () => void
  onDeleteGig: () => void
  onDownloadExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onCloneGig: () => void
  onOpenClient: (clientId: string) => void
  onOpenLinkedInvoice: () => void
  onOpenSellerProfile: () => void
  onUploadExpenseAttachment: (index: number, file: File) => void
  onDeleteExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onRemoveGigExpense: (index: number) => void
  onResetForm: () => void
  onSearchQueryChange: (value: string) => void
  onSelectGig: (gigId: string) => void
  onSortChange: (sort: GigSort) => void
  onToggleGigSelection: (gigId: string) => void
  onStartEditing: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateExpenseReimbursement: (
    expense: GigExpenseForm,
    status: GigExpenseReimbursementStatus
  ) => void
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
  gigSort,
  gigStatus,
  gigs,
  isGigLoading,
  isInvoiceLoading,
  isMileageEstimating,
  onAddGigExpense,
  onCloseEditor,
  onExpenseAmountChange,
  onExpenseDescriptionChange,
  onGenerateExpenseStatement,
  onGenerateInvoice,
  onEstimateMileage,
  onDeleteGig,
  onDownloadExpenseAttachment,
  onCloneGig,
  onOpenClient,
  onOpenLinkedInvoice,
  onOpenSellerProfile,
  onUploadExpenseAttachment,
  onDeleteExpenseAttachment,
  onRemoveGigExpense,
  onResetForm,
  onSearchQueryChange,
  onSelectGig,
  onSortChange,
  onToggleGigSelection,
  onStartEditing,
  onSubmit,
  onUpdateExpenseReimbursement,
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
  const selectedClientId = selectedGigs[0]?.clientId ?? null
  const hasCrossClientSelection = new Set(selectedGigs.map((gig) => gig.clientId)).size > 1
  const hasInvoicedSelection = selectedGigs.some((gig) => gig.isInvoiced)
  const gigSortOptions: { value: GigSortKey; label: string }[] = [
    { value: 'priority', label: 'Priority' },
    { value: 'date', label: 'Date' },
    { value: 'title', label: 'Gig' },
    { value: 'client', label: 'Client' },
    { value: 'venue', label: 'Venue' },
    { value: 'fee', label: 'Fee' },
    { value: 'status', label: 'Status' },
  ]

  return (
    <section className="section-layout">
      <div className="gig-workspace">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Bookings</p>
              <h2>Gigs</h2>
            </div>
            <button className="ghost-button" data-testid="new-gig-button" onClick={onResetForm} type="button">
              New gig
            </button>
          </div>

          <label className="search-field">
            <span>Search</span>
              <input
                data-testid="gig-search-input"
                type="search"
              placeholder="Client, title, venue..."
              value={gigSearchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
            />
          </label>

          <div className="compact-list-toolbar" aria-label="Gig list controls">
            <label>
              <span>Sort by</span>
              <select
                value={gigSort.key}
                onChange={(event) =>
                  onSortChange({ ...gigSort, key: event.target.value as GigSortKey })
                }
              >
                {gigSortOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <button
              className="compact-sort-direction"
              type="button"
              aria-label={
                gigSort.direction === 'asc'
                  ? 'Sort ascending. Click to sort descending.'
                  : 'Sort descending. Click to sort ascending.'
              }
              title={gigSort.direction === 'asc' ? 'Ascending' : 'Descending'}
              onClick={() =>
                onSortChange({
                  ...gigSort,
                  direction: gigSort.direction === 'asc' ? 'desc' : 'asc',
                })
              }
            >
              {gigSort.direction === 'asc' ? '↑' : '↓'}
            </button>
          </div>

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

          <div className="compact-record-list gig-record-list" aria-label="Gigs">
            <div className="compact-record-header gig-record-row">
              <span title="Select gigs" aria-label="Select gigs" />
              <span>Gig</span>
              <span>Client</span>
              <span>Date</span>
              <span>Venue</span>
              <span>Fee</span>
              <span>Status</span>
            </div>
            {filteredGigs.map((gig) => {
              const clientName = clientNamesById.get(gig.clientId) ?? 'Unknown client'
              const isDifferentSelectedClient =
                Boolean(selectedClientId) &&
                selectedClientId !== gig.clientId &&
                !selectedGigIds.includes(gig.id)
              const isSelectionDisabled = isDifferentSelectedClient
              const selectionLabel = gig.isInvoiced
                ? 'Invoiced gig'
                : isDifferentSelectedClient
                  ? 'Different client'
                  : 'Select gig'

              return (
                <button
                  key={gig.id}
                  className={`compact-record-row gig-record-row ${selectedGig?.id === gig.id ? 'selected' : ''}`}
                  data-testid="gig-card"
                  onClick={() => onSelectGig(gig.id)}
                  type="button"
                >
                  <label
                    className="compact-select-toggle gig-select-toggle"
                    onClick={(event) => event.stopPropagation()}
                    title={selectionLabel}
                  >
                    <input
                      type="checkbox"
                      aria-label={selectionLabel}
                      checked={selectedGigIds.includes(gig.id)}
                      disabled={isSelectionDisabled}
                      onChange={() => onToggleGigSelection(gig.id)}
                    />
                  </label>
                  <div className="compact-primary-cell">
                    <strong>{gig.title}</strong>
                    <span>{clientName}</span>
                  </div>
                  <span>{clientName}</span>
                  <span>{formatDate(gig.date)}</span>
                  <span>{gig.venue || 'No venue set'}</span>
                  <span>{formatCurrency(gig.fee)}</span>
                  <span className="compact-status-cell">
                    {formatGigStatus(gig.status)}
                  </span>
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
                data-testid="generate-invoice-button"
                onClick={onGenerateInvoice}
                type="button"
                disabled={
                  isInvoiceLoading ||
                  (selectedGigIds.length === 0 &&
                    (!selectedGig || selectedGig.isInvoiced)) ||
                  hasCrossClientSelection
                  || hasInvoicedSelection
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
                data-testid="expense-statement-button"
                onClick={onGenerateExpenseStatement}
                type="button"
                disabled={
                  isGigLoading ||
                  !selectedGig ||
                  hasCrossClientSelection ||
                  (selectedGigIds.length > 0
                    ? selectedGigs.every((gig) => gig.expenses.length === 0)
                    : selectedGig.expenses.length === 0)
                }
              >
                {selectedGigIds.length > 0
                  ? `Expense statement (${selectedGigIds.length})`
                  : 'Expense statement'}
              </button>
              <button
                className="ghost-button"
                data-testid="gig-edit-button"
                onClick={onStartEditing}
                type="button"
                disabled={!selectedGig}
              >
                Edit gig
              </button>
              <button
                className="ghost-button"
                onClick={onCloneGig}
                type="button"
                disabled={isGigLoading || !selectedGig}
              >
                Clone gig
              </button>
              <button
                className="danger-button"
                onClick={onDeleteGig}
                type="button"
                disabled={
                  isGigLoading ||
                  !selectedGig ||
                  selectedGig.status !== 'Confirmed' ||
                  selectedGig.isInvoiced
                }
                title={
                  selectedGig && selectedGig.status !== 'Confirmed'
                    ? 'Only planned gigs can be deleted.'
                    : selectedGig?.isInvoiced
                      ? 'Gigs with linked invoices cannot be deleted.'
                    : 'Delete planned gig'
                }
              >
                Delete gig
              </button>
            </div>
          </div>

          {selectedGig ? (
            <>
              <div className="detail-grid">
                <article>
                  <p className="detail-label">Client</p>
                  <button
                    className="link-button detail-link"
                    onClick={() => onOpenClient(selectedGig.clientId)}
                    type="button"
                  >
                    {selectedGigClientName}
                  </button>
                </article>
                <article>
                  <p className="detail-label">Status</p>
                  <strong data-testid="selected-gig-status">{formatGigStatus(selectedGig.status)}</strong>
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
                  <strong>
                    {selectedGig.wasDriving
                      ? `${selectedGig.travelMiles || 0} miles`
                      : 'No'}
                  </strong>
                </article>
                <article>
                  <p className="detail-label">Invoice link</p>
                  {selectedGig.isInvoiced ? (
                    <button className="ghost-button" data-testid="gig-open-linked-invoice-button" onClick={onOpenLinkedInvoice} type="button">
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
                      : hasInvoicedSelection
                        ? ` ${selectedGigIds.length} gig(s) selected. Invoiced gigs can be used for expense statements, but not new invoices.`
                        : ` ${selectedGigIds.length} gig(s) selected for a combined invoice or expense statement.`}
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
            data-testid="gig-form"
            onSubmit={onSubmit}
          >
            <div className="panel-heading">
              <div>
                <p className="section-label">Management Pane</p>
                <h2>{gigMode === 'create' ? 'Create gig' : 'Update gig'}</h2>
              </div>
              <span className="status-pill" data-testid="gig-status">{gigStatus}</span>
            </div>

            <div className="form-grid">
              <label>
                <span>Client</span>
                <select
                  data-testid="gig-client-select"
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
                  data-testid="gig-date-input"
                  required
                  type="date"
                  value={gigForm.date}
                  onChange={(event) => onUpdateGigField('date', event.target.value)}
                />
              </label>

              <label className="full-width">
                <span>Title / description</span>
                <input
                  data-testid="gig-title-input"
                  required
                  value={gigForm.title}
                  onChange={(event) => onUpdateGigField('title', event.target.value)}
                  placeholder="Spring product launch"
                />
              </label>

              <label className="full-width">
                <span>Location / venue</span>
                <input
                  data-testid="gig-venue-input"
                  required
                  value={gigForm.venue}
                  onChange={(event) => onUpdateGigField('venue', event.target.value)}
                  placeholder="Albert Hall, Manchester"
                />
              </label>

              <label>
                <span>Fee</span>
                <input
                  data-testid="gig-fee-input"
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
                  data-testid="gig-status-select"
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
                  data-testid="gig-driving-checkbox"
                  type="checkbox"
                  checked={gigForm.wasDriving}
                  onChange={(event) => onUpdateGigField('wasDriving', event.target.checked)}
                />
                <span>I was driving for this gig</span>
              </label>

              {gigForm.wasDriving && (
                <>
                  <label>
                    <span>Travel miles</span>
                    <input
                      data-testid="gig-travel-miles-input"
                      inputMode="decimal"
                      value={gigForm.travelMiles}
                      onChange={(event) => onUpdateGigField('travelMiles', event.target.value)}
                      placeholder="24"
                    />
                  </label>

                  <div className="mileage-estimate-action">
                    <button
                      className="ghost-button"
                      data-testid="gig-estimate-mileage-button"
                      disabled={gigMode !== 'edit' || isMileageEstimating}
                      onClick={onEstimateMileage}
                      type="button"
                    >
                      {isMileageEstimating ? 'Estimating...' : 'Estimate mileage'}
                    </button>
                  </div>

                  <label>
                    <span>Passengers</span>
                    <input
                      data-testid="gig-passenger-count-input"
                      inputMode="numeric"
                      value={gigForm.passengerCount}
                      onChange={(event) => onUpdateGigField('passengerCount', event.target.value)}
                      placeholder="0"
                    />
                  </label>
                </>
              )}

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
                  data-testid="gig-expense-amount-input"
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
                  data-testid="gig-expense-description-input"
                  value={gigExpenseDescription}
                  onChange={(event) => onExpenseDescriptionChange(event.target.value)}
                  placeholder="Parking, hotel, equipment hire..."
                  disabled={isGigLoading}
                />
              </label>
              <button
                className="ghost-button"
                data-testid="add-gig-expense-button"
                onClick={onAddGigExpense}
                type="button"
                disabled={isGigLoading}
              >
                Add expense
              </button>
            </div>

            {gigForm.expenses.length > 0 ? (
              <div className="gig-expense-list">
                {gigForm.expenses.map((expense, index) => {
                  const isReimbursed = expense.reimbursementStatus === 'Reimbursed'
                  const statusLabel = formatExpenseReimbursementStatus(expense.reimbursementStatus)

                  return (
                  <div
                    className={`gig-expense-item ${isReimbursed ? 'is-reimbursed' : ''}`}
                    data-testid="gig-expense-item"
                    key={`${expense.id || 'new'}-${index}`}
                  >
                    <div className="expense-reimbursement-state">
                      <span className={`expense-status-badge ${isReimbursed ? 'reimbursed' : ''}`}>
                        {statusLabel}
                      </span>
                      {isReimbursed && (
                        <span>
                          {expense.reimbursementMethod || expense.reimbursementNote || 'Recorded'}
                        </span>
                      )}
                    </div>
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
                    {expense.id && (
                      <label className="expense-status-select">
                        <span>Reimbursement</span>
                        <select
                          data-testid="gig-expense-reimbursement-select"
                          value={expense.reimbursementStatus}
                          onChange={(event) =>
                            onUpdateExpenseReimbursement(
                              expense,
                              event.target.value as GigExpenseReimbursementStatus
                            )
                          }
                          disabled={isGigLoading}
                        >
                          <option value="Unreimbursed">Claimable</option>
                          <option value="Reimbursed">Reimbursed</option>
                          <option value="NotClaimable">Not claimable</option>
                        </select>
                      </label>
                    )}
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
                  )
                })}
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
                data-testid="gig-save-close-button"
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

function formatExpenseReimbursementStatus(status: GigExpenseReimbursementStatus) {
  switch (status) {
    case 'Unreimbursed':
      return 'Claimable'
    case 'NotClaimable':
      return 'Not claimable'
    default:
      return status
  }
}
