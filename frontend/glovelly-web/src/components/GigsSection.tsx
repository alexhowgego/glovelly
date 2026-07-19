import { useEffect, useRef } from 'react'
import type { CSSProperties } from 'react'
import type { FormEvent } from 'react'
import { GigAttachmentsPanel } from './GigAttachmentsPanel'
import { GigEditorPanel } from './GigEditorPanel'
import { GigExpensesPanel } from './GigExpensesPanel'
import { TrashIcon } from './TrashIcon'
import { formatCurrency, formatDate, formatGigStatus, formatGigType } from '../formatters'
import { useMeasuredBlockSize } from '../hooks/useMeasuredBlockSize'
import type {
  Client,
  Gig,
  GigExternalResource,
  GigExternalResourceAttachment,
  GigExternalResourceForm,
  GigExpenseForm,
  GigExpenseReimbursementStatus,
  GigForm,
  GigQuickFilter,
  GigSort,
  GigSortKey,
  GigType,
} from '../types'

type GigsSectionProps = {
  clientNamesById: ReadonlyMap<string, string>
  clients: Client[]
  completedGigCount: number
  filteredGigs: Gig[]
  externalResourceForm: GigExternalResourceForm
  externalResourceMode: 'create' | 'edit'
  gigForm: GigForm
  isEditorOpen: boolean
  gigMode: 'create' | 'edit'
  gigQuickFilter: GigQuickFilter
  gigTypeFilter: GigType | 'all'
  gigSearchQuery: string
  gigSort: GigSort
  gigStatus: string
  gigs: Gig[]
  isGigLoading: boolean
  isInvoiceLoading: boolean
  isMileageEstimating: boolean
  isExternalResourceEditorOpen: boolean
  onCancelExternalResourceEdit: () => void
  onCloseEditor: () => void
  onDeleteExpenseDraft: (index: number) => Promise<boolean>
  onDeleteExternalResource: (resource: GigExternalResource) => void
  onDeleteExternalResourceAttachment: (
    resource: GigExternalResource,
    attachment: GigExternalResourceAttachment
  ) => void
  onDownloadExternalResourceAttachment: (
    resource: GigExternalResource,
    attachment: GigExternalResourceAttachment
  ) => void
  onGenerateExpenseStatement: () => void
  onGenerateInvoice: () => void
  onEstimateMileage: () => void
  onDeleteGig: () => void
  onDownloadExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onCloneGig: () => void
  onOpenClient: (clientId: string) => void
  onOpenLinkedInvoice: () => void
  onUploadExpenseAttachment: (index: number, file: File) => void
  onUploadExternalResourceAttachment: (resource: GigExternalResource, file: File) => void
  onDeleteExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onResetForm: () => void
  onQuickFilterChange: (filter: GigQuickFilter) => void
  onGigTypeFilterChange: (filter: GigType | 'all') => void
  onSearchQueryChange: (value: string) => void
  onSelectGig: (gigId: string) => void
  onSortChange: (sort: GigSort) => void
  onToggleGigSelection: (gigId: string) => void
  onStartEditing: () => void
  onStartExternalResourceCreate: () => void
  onStartExternalResourceEdit: (resource: GigExternalResource) => void
  onSaveExpenseDraft: (
    index: number | null,
    draft: { description: string; amount: string }
  ) => Promise<boolean>
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onSubmitExternalResource: (event: FormEvent<HTMLFormElement>) => void
  onUpdateExpenseReimbursement: (
    expense: GigExpenseForm,
    status: GigExpenseReimbursementStatus
  ) => void
  onUpdateGigField: (
    field: keyof GigForm,
    value: string | boolean | GigExpenseForm[]
  ) => void
  onUpdateExternalResourceField: (
    field: keyof GigExternalResourceForm,
    value: string | boolean
  ) => void
  plannedGigCount: number
  selectedGig: Gig | null
  selectedGigIds: string[]
  selectedGigs: Gig[]
}

export function GigsSection({
  clientNamesById,
  clients,
  completedGigCount,
  filteredGigs,
  externalResourceForm,
  externalResourceMode,
  gigForm,
  isEditorOpen,
  gigMode,
  gigQuickFilter,
  gigTypeFilter,
  gigSearchQuery,
  gigSort,
  gigStatus,
  gigs,
  isGigLoading,
  isInvoiceLoading,
  isMileageEstimating,
  isExternalResourceEditorOpen,
  onCancelExternalResourceEdit,
  onCloseEditor,
  onDeleteExpenseDraft,
  onDeleteExternalResource,
  onDeleteExternalResourceAttachment,
  onDownloadExternalResourceAttachment,
  onGenerateExpenseStatement,
  onGenerateInvoice,
  onEstimateMileage,
  onDeleteGig,
  onDownloadExpenseAttachment,
  onCloneGig,
  onOpenClient,
  onOpenLinkedInvoice,
  onUploadExpenseAttachment,
  onUploadExternalResourceAttachment,
  onDeleteExpenseAttachment,
  onResetForm,
  onQuickFilterChange,
  onGigTypeFilterChange,
  onSearchQueryChange,
  onSelectGig,
  onSortChange,
  onToggleGigSelection,
  onStartEditing,
  onStartExternalResourceCreate,
  onStartExternalResourceEdit,
  onSaveExpenseDraft,
  onSubmit,
  onSubmitExternalResource,
  onUpdateExpenseReimbursement,
  onUpdateExternalResourceField,
  onUpdateGigField,
  plannedGigCount,
  selectedGig,
  selectedGigIds,
  selectedGigs,
}: GigsSectionProps) {
  const editorSlotRef = useRef<HTMLDivElement | null>(null)
  const { ref: detailPanelRef, blockSize: detailPanelBlockSize } = useMeasuredBlockSize<HTMLDivElement>()
  const workspaceStyle = detailPanelBlockSize > 0
    ? ({ '--workspace-detail-height': `${detailPanelBlockSize}px` } as CSSProperties)
    : undefined
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
  const gigFilterOptions: { value: GigQuickFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'upcoming', label: 'Upcoming' },
    { value: 'uninvoiced', label: 'Uninvoiced' },
    { value: 'drafts', label: 'Drafts' },
    { value: 'completed', label: 'Completed' },
  ]
  useEffect(() => {
    if (!isEditorOpen || !window.matchMedia('(max-width: 1180px)').matches) {
      return
    }

    window.setTimeout(() => {
      editorSlotRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }, 80)
  }, [isEditorOpen])

  return (
    <section className="section-layout">
      <div className="gig-workspace" style={workspaceStyle}>
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

          <div className="compact-list-controls" aria-label="Gig list controls">
            <div className="compact-list-main-controls">
              <label className="search-field compact-search-field">
                <span>Search</span>
                <input
                  data-testid="gig-search-input"
                  type="search"
                  placeholder="Client, title, location, type..."
                  value={gigSearchQuery}
                  onChange={(event) => onSearchQueryChange(event.target.value)}
                />
              </label>
              <label>
                <span>Type</span>
                <select value={gigTypeFilter} onChange={(event) => onGigTypeFilterChange(event.target.value as GigType | 'all')}>
                  <option value="all">All types</option>
                  <option value="Performance">Performance</option>
                  <option value="Teaching">Teaching</option>
                  <option value="Rehearsal">Rehearsal</option>
                  <option value="Recording">Recording</option>
                  <option value="Admin">Admin</option>
                  <option value="Other">Other work</option>
                </select>
              </label>
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
            <div className="compact-filter-chips" aria-label="Gig filters">
              {gigFilterOptions.map((option) => (
                <button
                  key={option.value}
                  className={`compact-filter-chip ${gigQuickFilter === option.value ? 'selected' : ''}`}
                  type="button"
                  onClick={() => onQuickFilterChange(option.value)}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>

          <div className="compact-record-list gig-record-list" aria-label="Gigs">
            <div className="compact-record-header gig-record-row">
              <span title="Select gigs" aria-label="Select gigs" />
              <span>Gig</span>
              <span>Client</span>
              <span>Date</span>
              <span>Location</span>
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
                    <span>{formatGigType(gig.type)} · {clientName}</span>
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

        <div ref={detailPanelRef} className="panel">
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
                className={`ghost-button editor-toggle ${isEditorOpen ? 'active' : ''}`}
                data-testid="gig-edit-button"
                onClick={isEditorOpen ? onCloseEditor : onStartEditing}
                type="button"
                disabled={!selectedGig}
                aria-expanded={isEditorOpen}
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
                <TrashIcon />
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
                    data-testid="gig-client-link"
                    onClick={() => onOpenClient(selectedGig.clientId)}
                    type="button"
                  >
                    {selectedGigClientName}
                  </button>
                </article>
                <article>
                  <p className="detail-label">Type</p>
                  <strong>{formatGigType(selectedGig.type)}</strong>
                </article>
                <div className="gig-detail-metrics full-width">
                  <article>
                    <p className="detail-label">Date</p>
                    <strong>{formatDate(selectedGig.date)}</strong>
                  </article>
                  <article>
                    <p className="detail-label">Fee</p>
                    <strong>{formatCurrency(selectedGig.fee)}</strong>
                  </article>
                  <article>
                    <p className="detail-label">Status</p>
                    <strong data-testid="selected-gig-status">{formatGigStatus(selectedGig.status)}</strong>
                  </article>
                </div>
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

              <GigAttachmentsPanel
                selectedGig={selectedGig}
                externalResourceForm={externalResourceForm}
                externalResourceMode={externalResourceMode}
                gigStatus={gigStatus}
                isGigLoading={isGigLoading}
                isExternalResourceEditorOpen={isExternalResourceEditorOpen}
                onCancelExternalResourceEdit={onCancelExternalResourceEdit}
                onDeleteExternalResource={onDeleteExternalResource}
                onDeleteExternalResourceAttachment={onDeleteExternalResourceAttachment}
                onDownloadExternalResourceAttachment={onDownloadExternalResourceAttachment}
                onStartExternalResourceCreate={onStartExternalResourceCreate}
                onStartExternalResourceEdit={onStartExternalResourceEdit}
                onSubmitExternalResource={onSubmitExternalResource}
                onUpdateExternalResourceField={onUpdateExternalResourceField}
                onUploadExternalResourceAttachment={onUploadExternalResourceAttachment}
              />

              <GigExpensesPanel
                expenses={gigForm.expenses}
                gigStatus={gigStatus}
                isGigLoading={isGigLoading}
                selectedGig={selectedGig}
                onDeleteExpenseAttachment={onDeleteExpenseAttachment}
                onDeleteExpenseDraft={onDeleteExpenseDraft}
                onDownloadExpenseAttachment={onDownloadExpenseAttachment}
                onSaveExpenseDraft={onSaveExpenseDraft}
                onUpdateExpenseReimbursement={onUpdateExpenseReimbursement}
                onUploadExpenseAttachment={onUploadExpenseAttachment}
              />
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a gig to review its details.</strong>
              <p>Key booking and billing details will appear here.</p>
            </div>
          )}
        </div>

        <div ref={editorSlotRef} className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
          <GigEditorPanel
            clients={clients}
            gigForm={gigForm}
            gigMode={gigMode}
            gigStatus={gigStatus}
            isEditorOpen={isEditorOpen}
            isGigLoading={isGigLoading}
            isMileageEstimating={isMileageEstimating}
            onCloseEditor={onCloseEditor}
            onEstimateMileage={onEstimateMileage}
            onSubmit={onSubmit}
            onUpdateGigField={onUpdateGigField}
          />
        </div>
      </div>
    </section>
  )
}
