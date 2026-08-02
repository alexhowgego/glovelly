import type {
  QuickReceiptCandidate,
  QuickReceiptDraftResponse,
} from '../types'
import { QuickCaptureGigSelect } from './QuickCaptureGigSelect'
import { ReceiptAnalysisModal } from './ReceiptAnalysisModal'
import { AiSparkleIcon } from './AiSparkleIcon'
import { useState } from 'react'

type QuickReceiptModalProps = {
  candidates: QuickReceiptCandidate[]
  clientNamesById: ReadonlyMap<string, string>
  draft: QuickReceiptDraftResponse | null
  isSaving: boolean
  onAmountChange: (value: string) => void
  onClose: () => void
  onDescriptionChange: (value: string) => void
  onGoToGig: () => void
  onSaveDetails: (details?: { description: string; amount: string }) => Promise<void>
  onSaveDraft: () => void
  onSelectedGigChange: (gigId: string) => void
  pendingFile: File | null
  selectedGigId: string
  status: string
  amount: string
  description: string
  onSessionExpired: (message: string) => void
}

export function QuickReceiptModal({
  amount,
  candidates,
  clientNamesById,
  description,
  draft,
  isSaving,
  onAmountChange,
  onClose,
  onDescriptionChange,
  onGoToGig,
  onSaveDetails,
  onSaveDraft,
  onSelectedGigChange,
  pendingFile,
  selectedGigId,
  status,
  onSessionExpired,
}: QuickReceiptModalProps) {
  const [isAnalysisOpen, setIsAnalysisOpen] = useState(false)
  if (!pendingFile && !draft) {
    return null
  }

  const uploadedFileName =
    pendingFile?.name ||
    draft?.gig.expenses
      .find((expense) => expense.id === draft.expenseId)
      ?.attachments.find((attachment) => attachment.id === draft.attachmentId)
      ?.fileName ||
    'Receipt upload'

  return (
    <div className="settings-overlay" role="presentation">
      <section
        aria-labelledby="quick-receipt-title"
        className="settings-modal quick-receipt-modal panel"
        data-testid="quick-receipt-modal"
        role="dialog"
        aria-modal="true"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Receipt capture</p>
            <h2 id="quick-receipt-title">{draft ? 'Receipt saved' : 'Choose a gig'}</h2>
          </div>
          <button
            className="ghost-button"
            onClick={onClose}
            type="button"
            disabled={isSaving}
          >
            Close
          </button>
        </div>

        <div className="quick-receipt-summary">
          <strong>{uploadedFileName}</strong>
          <span>{status}</span>
          {isSaving && !draft ? (
            <div className="quick-receipt-progress" aria-label="Receipt upload in progress">
              <span />
            </div>
          ) : null}
        </div>

        {draft?.hasNearbyCandidates ? (
          <div className="quick-receipt-warning">
            <strong>Check the gig before moving on.</strong>
            <span>
              There are other gigs close to this date. The nearest one has been
              selected, but this receipt may belong somewhere else.
            </span>
          </div>
        ) : null}

        <QuickCaptureGigSelect
          candidates={candidates}
          clientNamesById={clientNamesById}
          emptyMessage="Create or update a gig near this receipt date, then try again."
          isSaving={isSaving && !draft}
          onSelectedGigChange={onSelectedGigChange}
          selectedGigId={selectedGigId}
        />

        {draft ? (
          <div className="form-grid quick-receipt-details">
            <label>
              <span>Amount</span>
              <input
                inputMode="decimal"
                value={amount}
                onChange={(event) => onAmountChange(event.target.value)}
                placeholder="0.00"
                disabled={isSaving}
              />
            </label>
            <label>
              <span>Description</span>
              <input
                value={description}
                onChange={(event) => onDescriptionChange(event.target.value)}
                placeholder="Taxi, parking, hotel..."
                disabled={isSaving}
              />
            </label>
          </div>
        ) : null}

        <div className="form-actions">
          {draft ? (
            <>
              <button
                className="primary-button"
                onClick={() => void onSaveDetails()}
                type="button"
                disabled={isSaving || !selectedGigId}
              >
                {isSaving ? 'Saving...' : 'Save details'}
              </button>
              <button
                className="ghost-button ai-button"
                onClick={() => setIsAnalysisOpen(true)}
                type="button"
                disabled={isSaving}
              >
                <AiSparkleIcon />
                Analyse receipt
              </button>
              <button
                className="ghost-button"
                onClick={onGoToGig}
                type="button"
                disabled={isSaving || !selectedGigId}
              >
                Go to gig
              </button>
            </>
          ) : (
            <button
              className="primary-button"
              onClick={onSaveDraft}
              type="button"
              disabled={isSaving || !selectedGigId}
            >
              {isSaving ? 'Saving...' : 'Save receipt draft'}
            </button>
          )}
          <span className="status-pill">{status}</span>
        </div>
      </section>
      {draft && isAnalysisOpen ? (
        <ReceiptAnalysisModal
          target={{
            gigId: draft.gig.id,
            expenseId: draft.expenseId,
            attachmentId: draft.attachmentId,
            fileName: uploadedFileName,
          }}
          onClose={() => setIsAnalysisOpen(false)}
          onApply={({ merchant, totalAmount }) => {
            const nextDescription = merchant || description
            const nextAmount = totalAmount === null ? amount : String(totalAmount)
            onDescriptionChange(nextDescription)
            onAmountChange(nextAmount)
            void onSaveDetails({ description: nextDescription, amount: nextAmount })
            setIsAnalysisOpen(false)
          }}
          onSessionExpired={onSessionExpired}
        />
      ) : null}
    </div>
  )
}
