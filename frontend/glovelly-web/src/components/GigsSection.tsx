import { useEffect, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import type { FormEvent } from 'react'
import { formatCurrency, formatDate, formatGigStatus } from '../formatters'
import { useMeasuredBlockSize } from '../hooks/useMeasuredBlockSize'
import type {
  Client,
  Gig,
  GigExternalResource,
  GigExternalResourceAttachment,
  GigExternalResourceForm,
  GigExternalResourcePurpose,
  GigExternalResourceType,
  GigExpenseForm,
  GigExpenseReimbursementStatus,
  GigForm,
  GigQuickFilter,
  GigSort,
  GigSortKey,
  GigStatus,
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
  const [expandedResourceId, setExpandedResourceId] = useState<string>('')
  const [expandedExpenseKey, setExpandedExpenseKey] = useState<string>('')
  const [isExpenseEditorOpen, setIsExpenseEditorOpen] = useState(false)
  const [editingExpenseIndex, setEditingExpenseIndex] = useState<number | null>(null)
  const [expenseDraft, setExpenseDraft] = useState({ description: '', amount: '' })
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
  const resourceTypeOptions: { value: GigExternalResourceType; label: string }[] = [
    { value: 'GoogleSheet', label: 'Google Sheet' },
    { value: 'GoogleDoc', label: 'Google Doc' },
    { value: 'Url', label: 'URL' },
    { value: 'Email', label: 'Email' },
    { value: 'File', label: 'File' },
    { value: 'Other', label: 'Other' },
  ]
  const resourcePurposeOptions: { value: GigExternalResourcePurpose; label: string }[] = [
    { value: 'SetList', label: 'Set list' },
    { value: 'GigPlan', label: 'Gig plan' },
    { value: 'Contract', label: 'Contract' },
    { value: 'Travel', label: 'Travel' },
    { value: 'Other', label: 'Other' },
  ]
  const formatResourceType = (value: GigExternalResourceType) =>
    resourceTypeOptions.find((option) => option.value === value)?.label ?? value
  const formatResourcePurpose = (value: GigExternalResourcePurpose) =>
    resourcePurposeOptions.find((option) => option.value === value)?.label ?? value
  const sortedExternalResources = (selectedGig?.externalResources ?? [])
    .slice()
    .sort((left, right) => {
      if (left.isPrimary !== right.isPrimary) {
        return left.isPrimary ? -1 : 1
      }

      return left.title.localeCompare(right.title)
    })

  useEffect(() => {
    if (!isEditorOpen || !window.matchMedia('(max-width: 1180px)').matches) {
      return
    }

    window.setTimeout(() => {
      editorSlotRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }, 80)
  }, [isEditorOpen])

  useEffect(() => {
    setExpandedResourceId('')
    setExpandedExpenseKey('')
  }, [selectedGig?.id])

  const externalResourceEditorTitle =
    externalResourceMode === 'edit' ? 'Edit attachment' : 'Add attachment'
  const expenseEditorTitle = editingExpenseIndex === null ? 'Add expense' : 'Edit expense'
  const openExpenseCreate = () => {
    setEditingExpenseIndex(null)
    setExpenseDraft({ description: '', amount: '' })
    setIsExpenseEditorOpen(true)
  }
  const openExpenseEdit = (index: number, expense: GigExpenseForm) => {
    setEditingExpenseIndex(index)
    setExpenseDraft({ description: expense.description, amount: expense.amount })
    setIsExpenseEditorOpen(true)
  }
  const closeExpenseEditor = () => {
    setIsExpenseEditorOpen(false)
    setEditingExpenseIndex(null)
    setExpenseDraft({ description: '', amount: '' })
  }
  const submitExpenseDraft = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const saved = await onSaveExpenseDraft(editingExpenseIndex, expenseDraft)
    if (saved) {
      closeExpenseEditor()
    }
  }

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
                  placeholder="Client, title, venue..."
                  value={gigSearchQuery}
                  onChange={(event) => onSearchQueryChange(event.target.value)}
                />
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
                <div className="associated-items-heading">
                  <div>
                    <p className="detail-label">Attachments</p>
                    <span>Links, documents, and files attached to this gig.</span>
                  </div>
                  <button
                    className="ghost-button"
                    onClick={onStartExternalResourceCreate}
                    type="button"
                    disabled={isGigLoading}
                  >
                    Add attachment
                  </button>
                </div>

                {sortedExternalResources.length > 0 ? (
                  <div className="associated-item-list external-resource-list">
                    {sortedExternalResources.map((resource) => {
                      const isExpanded = expandedResourceId === resource.id
                      const purposeLabel = formatResourcePurpose(resource.purpose)
                      const typeLabel = formatResourceType(resource.resourceType)
                      const fileCount = resource.attachments.length

                      return (
                        <article
                          key={resource.id}
                          className={`associated-item-row external-resource-item ${isExpanded ? 'expanded' : ''}`}
                        >
                          <button
                            className="associated-item-summary"
                            type="button"
                            aria-expanded={isExpanded}
                            onClick={() =>
                              setExpandedResourceId((current) =>
                                current === resource.id ? '' : resource.id
                              )
                            }
                          >
                            <div className="associated-item-main">
                              <strong>{resource.title}</strong>
                              <span>{purposeLabel} · {typeLabel}</span>
                            </div>
                            <div className="associated-item-chips">
                              {resource.isPrimary && (
                                <span className="resource-primary-badge">
                                  Primary {purposeLabel.toLowerCase()}
                                </span>
                              )}
                              {resource.url && <span className="resource-meta-chip">Link</span>}
                              <span className="resource-meta-chip">
                                {fileCount} file{fileCount === 1 ? '' : 's'}
                              </span>
                              <span className="associated-item-expand-indicator" aria-hidden="true">
                                {isExpanded ? '−' : '+'}
                              </span>
                            </div>
                          </button>

                          <div className="associated-item-expansion" inert={!isExpanded}>
                            <div className="associated-item-expansion-inner">
                              {resource.notes?.trim() && <p>{resource.notes}</p>}
                              <div className="associated-item-actions external-resource-actions">
                                {resource.url && (
                                  <a
                                    className="ghost-button"
                                    href={resource.url}
                                    target="_blank"
                                    rel="noreferrer"
                                  >
                                    Open
                                  </a>
                                )}
                                <button
                                  className="ghost-button"
                                  onClick={() => onStartExternalResourceEdit(resource)}
                                  type="button"
                                  disabled={isGigLoading}
                                >
                                  Edit
                                </button>
                                <button
                                  className="danger-button"
                                  onClick={() => onDeleteExternalResource(resource)}
                                  type="button"
                                  disabled={isGigLoading}
                                >
                                  Delete
                                </button>
                              </div>
                              <div className="resource-attachments">
                                <div className="expense-attachment-header">
                                  <span>Files</span>
                                  <label className="ghost-button file-upload-button">
                                    Upload
                                    <input
                                      type="file"
                                      onChange={(event) => {
                                        const file = event.target.files?.[0]
                                        if (file) {
                                          onUploadExternalResourceAttachment(resource, file)
                                        }
                                        event.currentTarget.value = ''
                                      }}
                                      disabled={isGigLoading}
                                    />
                                  </label>
                                </div>
                                {resource.attachments.length > 0 ? (
                                  <div className="expense-attachment-list">
                                    {resource.attachments.map((attachment) => (
                                      <div key={attachment.id} className="expense-attachment-item">
                                        <span>{attachment.fileName}</span>
                                        <div className="expense-attachment-actions">
                                          <button
                                            className="ghost-button"
                                            onClick={() =>
                                              onDownloadExternalResourceAttachment(resource, attachment)
                                            }
                                            type="button"
                                            disabled={isGigLoading}
                                          >
                                            Download
                                          </button>
                                          <button
                                            className="danger-button"
                                            onClick={() =>
                                              onDeleteExternalResourceAttachment(resource, attachment)
                                            }
                                            type="button"
                                            disabled={isGigLoading}
                                          >
                                            Delete
                                          </button>
                                        </div>
                                      </div>
                                    ))}
                                  </div>
                                ) : (
                                  <span>No files attached.</span>
                                )}
                              </div>
                            </div>
                          </div>
                        </article>
                      )
                    })}
                  </div>
                ) : (
                  <span>No attachments added yet.</span>
                )}

              </div>

              <div className="gig-timeline-note">
                <div className="associated-items-heading">
                  <div>
                    <p className="detail-label">Expenses</p>
                    <span>Chargeable costs associated with this gig.</span>
                  </div>
                  <button
                    className="ghost-button"
                    onClick={openExpenseCreate}
                    type="button"
                    disabled={isGigLoading || !selectedGig}
                  >
                    Add expense
                  </button>
                </div>

                {gigForm.expenses.length > 0 ? (
                  <div className="associated-item-list expense-associated-list">
                    {gigForm.expenses.map((expense, index) => {
                      const isReimbursed = expense.reimbursementStatus === 'Reimbursed'
                      const statusLabel = formatExpenseReimbursementStatus(expense.reimbursementStatus)
                      const expenseKey = `${expense.id || 'new'}-${index}`
                      const isExpanded = expandedExpenseKey === expenseKey
                      const amount = Number(expense.amount)
                      const amountLabel = Number.isFinite(amount) ? formatCurrency(amount) : expense.amount
                      const receiptCount = expense.attachments.length

                      return (
                        <article
                          className={`associated-item-row expense-associated-item ${isExpanded ? 'expanded' : ''}`}
                          data-testid="gig-expense-item"
                          key={expenseKey}
                        >
                          <button
                            className="associated-item-summary"
                            type="button"
                            aria-expanded={isExpanded}
                            onClick={() =>
                              setExpandedExpenseKey((current) =>
                                current === expenseKey ? '' : expenseKey
                              )
                            }
                          >
                            <div className="associated-item-main">
                              <strong>{expense.description || 'Untitled expense'}</strong>
                              <span>{amountLabel}</span>
                            </div>
                            <div className="associated-item-chips">
                              <span className={`expense-status-badge ${isReimbursed ? 'reimbursed' : ''}`}>
                                {statusLabel}
                              </span>
                              <span className="resource-meta-chip">
                                {receiptCount} receipt{receiptCount === 1 ? '' : 's'}
                              </span>
                              <span className="associated-item-expand-indicator" aria-hidden="true">
                                {isExpanded ? '−' : '+'}
                              </span>
                            </div>
                          </button>

                          <div className="associated-item-expansion" inert={!isExpanded}>
                            <div className="associated-item-expansion-inner">
                              {isReimbursed && (
                                <p>
                                  {expense.reimbursementMethod || expense.reimbursementNote || 'Reimbursement recorded.'}
                                </p>
                              )}
                              <div className="associated-item-actions expense-action-grid">
                                {expense.id && (
                                  <label>
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
                                <button
                                  className="ghost-button"
                                  onClick={() => openExpenseEdit(index, expense)}
                                  type="button"
                                  disabled={isGigLoading}
                                >
                                  Edit
                                </button>
                                <button
                                  className="danger-button"
                                  onClick={() => void onDeleteExpenseDraft(index)}
                                  type="button"
                                  disabled={isGigLoading}
                                >
                                  Remove
                                </button>
                              </div>

                              <div className="expense-attachments">
                                <div className="expense-attachment-header">
                                  <span>
                                    {receiptCount === 1 ? '1 receipt' : `${receiptCount} receipts`}
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
                                  <p className="attachment-helper">Save expense changes before adding receipts.</p>
                                )}
                              </div>
                            </div>
                          </div>
                        </article>
                      )
                    })}
                  </div>
                ) : (
                  <div className="empty-state compact-empty-state">
                    <strong>No expenses added yet.</strong>
                    <p>Add an expense to capture chargeable costs for this gig.</p>
                  </div>
                )}
              </div>
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a gig to review its details.</strong>
              <p>Key booking and billing details will appear here.</p>
            </div>
          )}
        </div>

        <div ref={editorSlotRef} className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
          <form
            aria-hidden={!isEditorOpen}
            className="editor-panel panel gig-editor-panel"
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

      {isExternalResourceEditorOpen && (
        <div className="settings-overlay" role="presentation">
          <section
            aria-labelledby="external-resource-editor-title"
            className="settings-modal external-resource-modal panel"
            role="dialog"
            aria-modal="true"
          >
            <div className="panel-heading">
              <div>
                <p className="section-label">Attachments</p>
                <h2 id="external-resource-editor-title">{externalResourceEditorTitle}</h2>
              </div>
              <button
                className="ghost-button"
                onClick={onCancelExternalResourceEdit}
                type="button"
                disabled={isGigLoading}
              >
                Close
              </button>
            </div>

            <form className="external-resource-form" onSubmit={onSubmitExternalResource}>
              <div className="compact-form-grid">
                <label>
                  <span>Type</span>
                  <select
                    value={externalResourceForm.resourceType}
                    onChange={(event) =>
                      onUpdateExternalResourceField(
                        'resourceType',
                        event.target.value as GigExternalResourceType
                      )
                    }
                  >
                    {resourceTypeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Purpose</span>
                  <select
                    value={externalResourceForm.purpose}
                    onChange={(event) =>
                      onUpdateExternalResourceField(
                        'purpose',
                        event.target.value as GigExternalResourcePurpose
                      )
                    }
                  >
                    {resourcePurposeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Title</span>
                  <input
                    required
                    value={externalResourceForm.title}
                    onChange={(event) =>
                      onUpdateExternalResourceField('title', event.target.value)
                    }
                  />
                </label>
                <label>
                  <span>URL</span>
                  <input
                    type="url"
                    placeholder="Optional link"
                    value={externalResourceForm.url}
                    onChange={(event) =>
                      onUpdateExternalResourceField('url', event.target.value)
                    }
                  />
                </label>
              </div>
              <label>
                <span>Notes</span>
                <textarea
                  rows={3}
                  value={externalResourceForm.notes}
                  onChange={(event) =>
                    onUpdateExternalResourceField('notes', event.target.value)
                  }
                />
              </label>
              <label className="checkbox-field resource-primary-toggle">
                <input
                  type="checkbox"
                  checked={externalResourceForm.isPrimary}
                  onChange={(event) =>
                    onUpdateExternalResourceField('isPrimary', event.target.checked)
                  }
                />
                <span>Make this the primary attachment for its purpose</span>
              </label>
              <div className="form-actions">
                <button className="primary-button" type="submit" disabled={isGigLoading}>
                  {externalResourceMode === 'edit' ? 'Update attachment' : 'Add attachment'}
                </button>
                <button
                  className="ghost-button"
                  onClick={onCancelExternalResourceEdit}
                  type="button"
                  disabled={isGigLoading}
                >
                  Cancel
                </button>
                <span className="status-pill">{gigStatus}</span>
              </div>
            </form>
          </section>
        </div>
      )}

      {isExpenseEditorOpen && (
        <div className="settings-overlay" role="presentation">
          <section
            aria-labelledby="expense-editor-title"
            className="settings-modal external-resource-modal panel"
            role="dialog"
            aria-modal="true"
          >
            <div className="panel-heading">
              <div>
                <p className="section-label">Expenses</p>
                <h2 id="expense-editor-title">{expenseEditorTitle}</h2>
              </div>
              <button
                className="ghost-button"
                onClick={closeExpenseEditor}
                type="button"
                disabled={isGigLoading}
              >
                Close
              </button>
            </div>

            <form className="external-resource-form" onSubmit={submitExpenseDraft}>
              <div className="compact-form-grid">
                <label>
                  <span>Amount</span>
                  <input
                    data-testid="gig-expense-amount-input"
                    inputMode="decimal"
                    value={expenseDraft.amount}
                    onChange={(event) =>
                      setExpenseDraft((current) => ({
                        ...current,
                        amount: event.target.value,
                      }))
                    }
                    placeholder="45.00"
                    disabled={isGigLoading}
                  />
                </label>
                <label>
                  <span>Description</span>
                  <input
                    data-testid="gig-expense-description-input"
                    value={expenseDraft.description}
                    onChange={(event) =>
                      setExpenseDraft((current) => ({
                        ...current,
                        description: event.target.value,
                      }))
                    }
                    placeholder="Parking, hotel, equipment hire..."
                    disabled={isGigLoading}
                  />
                </label>
              </div>
              <div className="form-actions">
                <button
                  className="primary-button"
                  data-testid="add-gig-expense-button"
                  type="submit"
                  disabled={isGigLoading}
                >
                  {editingExpenseIndex === null ? 'Add expense' : 'Update expense'}
                </button>
                <button
                  className="ghost-button"
                  onClick={closeExpenseEditor}
                  type="button"
                  disabled={isGigLoading}
                >
                  Cancel
                </button>
                <span className="status-pill">{gigStatus}</span>
              </div>
            </form>
          </section>
        </div>
      )}
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
