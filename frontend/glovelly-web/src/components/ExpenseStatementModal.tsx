import { useEffect, useState } from 'react'
import { formatCurrency, formatDate } from '../formatters'
import type {
  Gig,
  GigExpense,
  GigExpenseReimbursementStatus,
} from '../types'

type ExpenseStatementModalProps = {
  clientName: string
  expenseIds: string[]
  gigs: Gig[]
  includeReceiptAppendix: boolean
  includeReceiptAttachments: boolean
  isOpen: boolean
  isSaving: boolean
  onClose: () => void
  onDownload: () => void
  onIncludeReceiptAppendixChange: (value: boolean) => void
  onIncludeReceiptAttachmentsChange: (value: boolean) => void
  onPreview: () => void
  onToggleExpense: (expenseId: string) => void
  previewPdfUrl: string | null
  receiptCount: number
  status: string
  total: number
}

export function ExpenseStatementModal({
  clientName,
  expenseIds,
  gigs,
  includeReceiptAppendix,
  includeReceiptAttachments,
  isOpen,
  isSaving,
  onClose,
  onDownload,
  onIncludeReceiptAppendixChange,
  onIncludeReceiptAttachmentsChange,
  onPreview,
  onToggleExpense,
  previewPdfUrl,
  receiptCount,
  status,
  total,
}: ExpenseStatementModalProps) {
  const [isPreviewVisible, setIsPreviewVisible] = useState(false)

  useEffect(() => {
    if (!isOpen) {
      setIsPreviewVisible(false)
    }
  }, [isOpen])

  if (!isOpen) {
    return null
  }

  const selectedExpenseIdSet = new Set(expenseIds)
  const selectedExpenseCount = expenseIds.length
  const shouldShowPreview = isPreviewVisible && Boolean(previewPdfUrl)
  const previewButtonLabel = shouldShowPreview
    ? 'Show expenses'
    : previewPdfUrl
      ? 'Show preview'
      : 'Preview PDF'

  const handlePreviewToggle = () => {
    if (shouldShowPreview) {
      setIsPreviewVisible(false)
      return
    }

    setIsPreviewVisible(true)
    if (!previewPdfUrl) {
      onPreview()
    }
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-modal="true"
        className="settings-modal expense-statement-modal panel"
        data-testid="expense-statement-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Expense Statement</p>
            <h2>{clientName}</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button">
            Close
          </button>
        </div>

        <div className="expense-statement-summary">
          <article>
            <span>{gigs.length}</span>
            <p>{gigs.length === 1 ? 'gig' : 'gigs'}</p>
          </article>
          <article>
            <span>{selectedExpenseCount}</span>
            <p>{selectedExpenseCount === 1 ? 'expense' : 'expenses'}</p>
          </article>
          <article>
            <span data-testid="expense-statement-total">{formatCurrency(total)}</span>
            <p>selected total</p>
          </article>
        </div>

        <div className="expense-statement-options">
          <label className="checkbox-field">
            <input
              type="checkbox"
              checked={includeReceiptAttachments}
              onChange={(event) => onIncludeReceiptAttachmentsChange(event.target.checked)}
            />
            <span>Include receipt attachments</span>
          </label>
          <label className="checkbox-field">
            <input
              type="checkbox"
              checked={includeReceiptAttachments && includeReceiptAppendix}
              disabled={!includeReceiptAttachments}
              onChange={(event) => onIncludeReceiptAppendixChange(event.target.checked)}
            />
            <span>Include receipt appendix in PDF</span>
          </label>
        </div>

        <div
          className={`expense-statement-body ${
            shouldShowPreview ? 'has-preview' : 'without-preview'
          }`}
        >
          {!shouldShowPreview && (
            <div className="expense-statement-gigs">
              {gigs.map((gig) => (
                <section className="expense-statement-gig" key={gig.id}>
                  <div className="expense-statement-gig-header">
                    <div>
                      <strong>{gig.title}</strong>
                      <span>
                        {formatDate(gig.date)} · {gig.venue}
                      </span>
                    </div>
                    {gig.isInvoiced && <span className="expense-status-badge">Invoiced</span>}
                  </div>

                  {gig.expenses.length > 0 ? (
                    <div className="expense-statement-expenses">
                      {gig.expenses
                        .slice()
                        .sort((left, right) => left.sortOrder - right.sortOrder)
                        .map((expense) => (
                          <ExpenseStatementExpenseRow
                            expense={expense}
                            isSelected={selectedExpenseIdSet.has(expense.id)}
                            key={expense.id}
                            onToggleExpense={onToggleExpense}
                          />
                        ))}
                    </div>
                  ) : (
                    <p className="attachment-helper">No expenses recorded for this gig.</p>
                  )}
                </section>
              ))}
            </div>
          )}

          {shouldShowPreview && previewPdfUrl && (
            <div className="expense-statement-preview">
              <iframe data-testid="expense-statement-preview-frame" src={previewPdfUrl} title="Expense statement PDF preview" />
            </div>
          )}
        </div>

        <div className="expense-statement-footer">
          <span className="status-pill" data-testid="expense-statement-status">
            {status ||
              `${receiptCount} receipt${receiptCount === 1 ? '' : 's'} available for selected expenses.`}
          </span>
          <div className="actions">
            <button
              className="ghost-button"
              data-testid="expense-statement-preview-button"
              disabled={isSaving || selectedExpenseCount === 0}
              aria-pressed={shouldShowPreview}
              onClick={handlePreviewToggle}
              type="button"
            >
              {previewButtonLabel}
            </button>
            <button
              className="primary-button"
              data-testid="expense-statement-download-button"
              disabled={isSaving || selectedExpenseCount === 0}
              onClick={onDownload}
              type="button"
            >
              Download PDF
            </button>
          </div>
        </div>
      </section>
    </div>
  )
}

function ExpenseStatementExpenseRow({
  expense,
  isSelected,
  onToggleExpense,
}: {
  expense: GigExpense
  isSelected: boolean
  onToggleExpense: (expenseId: string) => void
}) {
  const isReimbursed = expense.reimbursementStatus === 'Reimbursed'
  const isNotClaimable = expense.reimbursementStatus === 'NotClaimable'

  return (
    <label
      className={`expense-statement-expense ${
        isReimbursed || isNotClaimable ? 'is-muted' : ''
      }`}
      data-testid="expense-statement-expense-row"
    >
      <input
        checked={isSelected}
        onChange={() => onToggleExpense(expense.id)}
        type="checkbox"
      />
      <span>
        <strong>{expense.description}</strong>
        <small>
          {formatExpenseReimbursementStatus(expense.reimbursementStatus)}
          {expense.attachments.length > 0
            ? ` · ${expense.attachments.length} receipt${expense.attachments.length === 1 ? '' : 's'}`
            : ' · no receipts'}
        </small>
      </span>
      <b>{formatCurrency(expense.amount)}</b>
    </label>
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
