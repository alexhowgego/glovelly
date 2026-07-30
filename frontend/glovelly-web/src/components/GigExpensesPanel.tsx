import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { formatCurrency } from '../formatters'
import type { Gig, GigExpenseForm, GigExpenseReimbursementStatus, ReceiptAnalysisTarget } from '../types'
import { ReceiptAnalysisModal } from './ReceiptAnalysisModal'
import { AiSparkleIcon } from './AiSparkleIcon'
import { TrashIcon } from './TrashIcon'

type GigExpensesPanelProps = {
  expenses: GigExpenseForm[]
  gigStatus: string
  isGigLoading: boolean
  selectedGig: Gig | null
  onDeleteExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onDeleteExpenseDraft: (index: number) => Promise<boolean>
  onDownloadExpenseAttachment: (expense: GigExpenseForm, attachmentId: string) => void
  onSaveExpenseDraft: (
    index: number | null,
    draft: { description: string; amount: string }
  ) => Promise<boolean>
  onUpdateExpenseReimbursement: (
    expense: GigExpenseForm,
    status: GigExpenseReimbursementStatus
  ) => void
  onUploadExpenseAttachment: (index: number, file: File) => void
  onSessionExpired: (message: string) => void
}

export function GigExpensesPanel({
  expenses,
  gigStatus,
  isGigLoading,
  selectedGig,
  onDeleteExpenseAttachment,
  onDeleteExpenseDraft,
  onDownloadExpenseAttachment,
  onSaveExpenseDraft,
  onUpdateExpenseReimbursement,
  onUploadExpenseAttachment,
  onSessionExpired,
}: GigExpensesPanelProps) {
  const [expandedExpenseKey, setExpandedExpenseKey] = useState<string>('')
  const [isExpenseEditorOpen, setIsExpenseEditorOpen] = useState(false)
  const [editingExpenseIndex, setEditingExpenseIndex] = useState<number | null>(null)
  const [expenseDraft, setExpenseDraft] = useState({ description: '', amount: '' })
  const [analysisTarget, setAnalysisTarget] = useState<(ReceiptAnalysisTarget & { expenseIndex: number }) | null>(null)
  const expenseEditorTitle = editingExpenseIndex === null ? 'Add expense' : 'Edit expense'

  useEffect(() => {
    setExpandedExpenseKey('')
  }, [selectedGig?.id])

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

  const applyReceiptSuggestions = (suggestions: { merchant: string | null; totalAmount: number | null }) => {
    if (!analysisTarget) return
    const expense = expenses[analysisTarget.expenseIndex]
    if (!expense) return
    setEditingExpenseIndex(analysisTarget.expenseIndex)
    setExpenseDraft({
      description: suggestions.merchant || expense.description,
      amount: suggestions.totalAmount === null ? expense.amount : String(suggestions.totalAmount),
    })
    setIsExpenseEditorOpen(true)
    setAnalysisTarget(null)
  }

  const submitExpenseDraft = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const saved = await onSaveExpenseDraft(editingExpenseIndex, expenseDraft)
    if (saved) {
      closeExpenseEditor()
    }
  }

  return (
    <>
      <div className="gig-timeline-note">
        <div className="associated-items-heading">
          <div>
            <p className="detail-label">Expenses</p>
            <span>Chargeable costs associated with this gig.</span>
          </div>
          <button
            className="ghost-button"
            data-testid="open-gig-expense-dialog-button"
            onClick={openExpenseCreate}
            type="button"
            disabled={isGigLoading || !selectedGig}
          >
            Add expense
          </button>
        </div>

        {expenses.length > 0 ? (
          <div className="associated-item-list expense-associated-list">
            {expenses.map((expense, index) => {
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
                          aria-label={`Remove expense ${expense.description || 'Untitled expense'}`}
                          className="icon-delete-button"
                          onClick={() => void onDeleteExpenseDraft(index)}
                          type="button"
                          disabled={isGigLoading}
                          title="Remove expense"
                        >
                          <TrashIcon />
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
                              data-testid="gig-expense-receipt-file-input"
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
                                    data-testid="gig-expense-receipt-download-button"
                                    type="button"
                                    onClick={() => onDownloadExpenseAttachment(expense, attachment.id)}
                                    disabled={isGigLoading}
                                  >
                                   {attachment.fileName}
                                  </button>
                                  <button
                                    className="ghost-button ai-button"
                                    onClick={() => selectedGig && setAnalysisTarget({
                                      gigId: selectedGig.id,
                                      expenseId: expense.id,
                                      attachmentId: attachment.id,
                                      fileName: attachment.fileName,
                                      expenseIndex: index,
                                    })}
                                    type="button"
                                    disabled={isGigLoading}
                                  >
                                    <AiSparkleIcon />
                                    Analyse
                                  </button>
                                  <button
                                    aria-label={`Delete receipt ${attachment.fileName}`}
                                    className="icon-delete-button"
                                    data-testid="gig-expense-receipt-delete-button"
                                    type="button"
                                    onClick={() => onDeleteExpenseAttachment(expense, attachment.id)}
                                    disabled={isGigLoading}
                                    title="Delete receipt"
                                  >
                                    <TrashIcon />
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
      {analysisTarget ? (
        <ReceiptAnalysisModal
          target={analysisTarget}
          onClose={() => setAnalysisTarget(null)}
          onApply={applyReceiptSuggestions}
          onSessionExpired={onSessionExpired}
        />
      ) : null}
    </>
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
