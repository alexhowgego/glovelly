import type {
  GigExternalResourcePurpose,
  GigExternalResourceType,
  QuickExternalResourceDraftResponse,
  QuickGigCandidate,
} from '../types'
import type { QuickAttachmentMode } from '../hooks/useQuickAttachment'
import { QuickCaptureGigSelect } from './QuickCaptureGigSelect'

type QuickAttachmentModalProps = {
  candidates: QuickGigCandidate[]
  clientNamesById: ReadonlyMap<string, string>
  draft: QuickExternalResourceDraftResponse | null
  isPrimary: boolean
  isSaving: boolean
  mode: QuickAttachmentMode
  notes: string
  onClose: () => void
  onFileChange: (file: File) => void
  onGoToGig: () => void
  onIsPrimaryChange: (value: boolean) => void
  onModeLink: () => void
  onNotesChange: (value: string) => void
  onPurposeChange: (value: GigExternalResourcePurpose) => void
  onResourceTypeChange: (value: GigExternalResourceType) => void
  onSaveDetails: () => void
  onSaveDraft: () => void
  onSaveLink: () => void
  onSelectedGigChange: (gigId: string) => void
  onTitleChange: (value: string) => void
  onUrlChange: (value: string) => void
  pendingFile: File | null
  purpose: GigExternalResourcePurpose
  resourceType: GigExternalResourceType
  selectedGigId: string
  status: string
  title: string
  url: string
}

const resourceTypeOptions: { value: GigExternalResourceType; label: string }[] = [
  { value: 'GoogleSheet', label: 'Google Sheet' },
  { value: 'GoogleDoc', label: 'Google Doc' },
  { value: 'Url', label: 'URL' },
  { value: 'Email', label: 'Email' },
  { value: 'File', label: 'File' },
  { value: 'Other', label: 'Other' },
]

const purposeOptions: { value: GigExternalResourcePurpose; label: string }[] = [
  { value: 'SetList', label: 'Set list' },
  { value: 'GigPlan', label: 'Gig plan' },
  { value: 'Contract', label: 'Contract' },
  { value: 'Travel', label: 'Travel' },
  { value: 'Other', label: 'Other' },
]

export function QuickAttachmentModal({
  candidates,
  clientNamesById,
  draft,
  isPrimary,
  isSaving,
  mode,
  notes,
  onClose,
  onFileChange,
  onGoToGig,
  onIsPrimaryChange,
  onModeLink,
  onNotesChange,
  onPurposeChange,
  onResourceTypeChange,
  onSaveDetails,
  onSaveDraft,
  onSaveLink,
  onSelectedGigChange,
  onTitleChange,
  onUrlChange,
  pendingFile,
  purpose,
  resourceType,
  selectedGigId,
  status,
  title,
  url,
}: QuickAttachmentModalProps) {
  if (mode === 'choose' && !status) {
    return null
  }

  const uploadedFileName =
    pendingFile?.name ||
    draft?.gig.externalResources
      .find((resource) => resource.id === draft.resourceId)
      ?.attachments.find((attachment) => attachment.id === draft.attachmentId)
      ?.fileName ||
    (mode === 'link' ? url || 'Link attachment' : 'Attachment upload')
  const draftGigCandidate = draft
    ? {
        id: draft.gig.id,
        clientId: draft.gig.clientId,
        title: draft.gig.title,
        date: draft.gig.date,
        venue: draft.gig.venue,
        status: draft.gig.status,
        daysFromToday: getDaysFromToday(draft.gig.date),
        isSelected: true,
      }
    : null
  const displayedCandidates = candidates.length > 0
    ? candidates
    : draftGigCandidate
      ? [draftGigCandidate]
      : []
  const hasCandidatePrompt =
    displayedCandidates.length > 0 || status.startsWith('No gig was within')
  const selectedCandidateId = selectedGigId ||
    displayedCandidates.find((candidate) => candidate.isSelected)?.id ||
    draft?.gig.id ||
    displayedCandidates[0]?.id ||
    ''
  const hasNearbyCandidates = draft?.hasNearbyCandidates || displayedCandidates.some(
    (candidate) => candidate.id !== selectedCandidateId && candidate.daysFromToday <= 2
  )
  const showDetails = mode === 'link' || Boolean(draft)
  const showCandidatePicker = mode !== 'choose' && hasCandidatePrompt && !isSaving

  return (
    <div className="settings-overlay" role="presentation">
      <section
        aria-labelledby="quick-attachment-title"
        className="settings-modal quick-receipt-modal panel"
        role="dialog"
        aria-modal="true"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Attachment capture</p>
            <h2 id="quick-attachment-title">
              {draft ? 'Attachment saved' : mode === 'choose' ? 'Add attachment' : 'Choose a gig'}
            </h2>
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
            <div className="quick-receipt-progress" aria-label="Attachment upload in progress">
              <span />
            </div>
          ) : null}
        </div>

        {mode === 'choose' ? (
          <div className="quick-attachment-choice-grid">
            <label className="primary-button quick-attachment-choice">
              Upload file
              <input
                type="file"
                disabled={isSaving}
                onChange={(event) => {
                  const file = event.target.files?.[0]
                  event.target.value = ''
                  if (file) {
                    onFileChange(file)
                  }
                }}
              />
            </label>
            <button
              className="ghost-button quick-attachment-choice"
              onClick={onModeLink}
              type="button"
              disabled={isSaving}
            >
              Add link
            </button>
          </div>
        ) : null}

        {mode === 'file' && !draft ? (
          <label className="ghost-button quick-attachment-file-pick">
            Choose file
            <input
              type="file"
              disabled={isSaving}
              onChange={(event) => {
                const file = event.target.files?.[0]
                event.target.value = ''
                if (file) {
                  onFileChange(file)
                }
              }}
            />
          </label>
        ) : null}

        {hasNearbyCandidates ? (
          <div className="quick-receipt-warning">
            <strong>Check the gig before moving on.</strong>
            <span>
              There are other gigs close to this date. The nearest one has been
              selected, but this attachment may belong somewhere else.
            </span>
          </div>
        ) : null}

        {showCandidatePicker ? (
          <QuickCaptureGigSelect
            candidates={displayedCandidates}
            clientNamesById={clientNamesById}
            emptyMessage="Create or update a gig near this attachment date, then try again."
            isSaving={isSaving && !draft}
            onSelectedGigChange={onSelectedGigChange}
            selectedGigId={selectedGigId || draft?.gig.id || ''}
          />
        ) : null}

        {mode === 'link' && !draft ? (
          <label className="quick-receipt-select">
            <span>Link</span>
            <input
              type="url"
              value={url}
              onChange={(event) => onUrlChange(event.target.value)}
              placeholder="https://docs.google.com/..."
              disabled={isSaving}
            />
          </label>
        ) : null}

        {showDetails ? (
          <div className="form-grid quick-receipt-details">
            <label>
              <span>Title</span>
              <input
                value={title}
                onChange={(event) => onTitleChange(event.target.value)}
                placeholder="Set list, gig plan, contract..."
                disabled={isSaving}
              />
            </label>
            <label>
              <span>Type</span>
              <select
                value={resourceType}
                onChange={(event) =>
                  onResourceTypeChange(event.target.value as GigExternalResourceType)
                }
                disabled={isSaving}
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
                value={purpose}
                onChange={(event) =>
                  onPurposeChange(event.target.value as GigExternalResourcePurpose)
                }
                disabled={isSaving}
              >
                {purposeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>URL</span>
              <input
                type="url"
                value={url}
                onChange={(event) => onUrlChange(event.target.value)}
                placeholder="Optional link"
                disabled={isSaving}
              />
            </label>
            <label className="full-width">
              <span>Notes</span>
              <textarea
                rows={3}
                value={notes}
                onChange={(event) => onNotesChange(event.target.value)}
                disabled={isSaving}
              />
            </label>
            <label className="checkbox-field full-width">
              <input
                type="checkbox"
                checked={isPrimary}
                onChange={(event) => onIsPrimaryChange(event.target.checked)}
                disabled={isSaving}
              />
              <span>Make this the primary attachment for its purpose</span>
            </label>
          </div>
        ) : null}

        {mode !== 'choose' ? (
          <div className="form-actions">
            {draft ? (
            <>
              <button
                className="primary-button"
                onClick={onSaveDetails}
                type="button"
                disabled={isSaving || !selectedGigId}
              >
                {isSaving ? 'Saving...' : 'Save details'}
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
            ) : mode === 'link' ? (
            <button
              className="primary-button"
              onClick={candidates.length > 0 ? onSaveDraft : onSaveLink}
              type="button"
              disabled={isSaving || (candidates.length > 0 && !selectedGigId)}
            >
              {isSaving ? 'Saving...' : 'Save attachment draft'}
            </button>
            ) : mode === 'file' ? (
            <button
              className="primary-button"
              onClick={onSaveDraft}
              type="button"
              disabled={isSaving || !selectedGigId || !pendingFile}
            >
              {isSaving ? 'Saving...' : 'Save attachment draft'}
            </button>
            ) : null}
            <span className="status-pill">{status}</span>
          </div>
        ) : null}
      </section>
    </div>
  )
}

function getDaysFromToday(date: string) {
  const target = new Date(`${date}T00:00:00`)
  if (Number.isNaN(target.getTime())) {
    return 0
  }

  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return Math.abs(Math.round((target.getTime() - today.getTime()) / 86_400_000))
}
