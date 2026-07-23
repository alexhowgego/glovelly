import { formatCurrency, formatDate, formatGigType } from '../formatters'
import type { Client, GigImportBatchDetail, GigImportBatchSummary, GigImportDraft } from '../types'
import type { GigImportDraftField } from '../hooks/useGigImportsWorkspace'

type GigImportsModalProps = {
  batchDetail: GigImportBatchDetail | null
  batches: GigImportBatchSummary[]
  clients: Client[]
  gigImportStatus: string
  isOpen: boolean
  isGigImportLoading: boolean
  onClose: () => void
  onCommitDecisions: () => void
  onSelectBatch: (batchId: string) => void
  onSetDraftStatus: (draft: GigImportDraft, status: 'Accepted' | 'Rejected' | 'Pending') => void
  onUpdateDraftField: (
    draftId: string,
    field: GigImportDraftField,
    value: string
  ) => void
  selectedBatchId: string
}

export function GigImportsModal({
  batchDetail,
  batches,
  clients,
  gigImportStatus,
  isOpen,
  isGigImportLoading,
  onClose,
  onCommitDecisions,
  onSelectBatch,
  onSetDraftStatus,
  onUpdateDraftField,
  selectedBatchId,
}: GigImportsModalProps) {
  const acceptedRows = batchDetail?.drafts.filter((draft) => draft.status === 'Accepted').length ?? 0
  const rejectedRows = batchDetail?.drafts.filter((draft) => draft.status === 'Rejected').length ?? 0
  const pendingRows = batchDetail?.drafts.filter((draft) => draft.status === 'Pending').length ?? 0
  const canCommitDecisions = acceptedRows > 0 || (rejectedRows > 0 && pendingRows === 0)

  if (!isOpen) {
    return null
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-labelledby="gig-imports-title"
        aria-modal="true"
        className="settings-modal gig-imports-modal panel"
        data-testid="gig-imports-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Imported gigs</p>
            <h2 id="gig-imports-title">Review imports</h2>
          </div>
          <button
            className="ghost-button"
            disabled={isGigImportLoading}
            onClick={onClose}
            type="button"
          >
            Close
          </button>
        </div>

        <div className="gig-import-workspace">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Staged</p>
              <h3>Gig imports</h3>
            </div>
            <span className="status-pill">{batches.length} batches</span>
          </div>

          <div className="client-list">
            {batches.map((batch) => (
              <button
                key={batch.batchId}
                className={`client-card ${selectedBatchId === batch.batchId ? 'selected' : ''}`}
                data-testid="gig-import-batch-card"
                onClick={() => onSelectBatch(batch.batchId)}
                type="button"
              >
                <div>
                  <strong>{batch.sourceName}</strong>
                  <span>{formatDate(batch.createdAtUtc.slice(0, 10))}</span>
                </div>
                <small className="gig-card-meta">
                  {batch.status} · {batch.draftCount} rows
                </small>
                <small className="gig-card-meta">
                  {batch.acceptedCount} accepted · {batch.pendingCount} pending · {batch.rejectedCount} rejected
                </small>
                <small className="gig-card-meta">
                  Confidence: {batch.highConfidenceCount} high · {batch.lowConfidenceCount} low
                </small>
              </button>
            ))}

            {batches.length === 0 && (
              <div className="empty-state">
                <strong>No gig imports yet.</strong>
                <p>Staged rows created by the assistant will appear here for review.</p>
              </div>
            )}
          </div>
        </div>

        <div className="panel gig-import-detail-panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Review</p>
              <h2>{batchDetail?.batch.sourceName ?? 'Select an import batch'}</h2>
            </div>
            <div className="actions">
              <button
                className="primary-button"
                disabled={isGigImportLoading || !canCommitDecisions}
                onClick={onCommitDecisions}
                type="button"
              >
                Commit decisions ({acceptedRows} accepted, {rejectedRows} rejected)
              </button>
            </div>
          </div>

          <div className="gig-timeline-note">
            <p className="detail-label">Import status</p>
            <span>{gigImportStatus || 'Review rows, accept the ones you trust, then commit them into real gigs.'}</span>
          </div>

          {batchDetail ? (
            <div className="gig-import-draft-list">
              {batchDetail.drafts.map((draft) => {
                const isCommitted = draft.status === 'Committed'
                const isRejected = draft.status === 'Rejected'

                return (
                  <article className={`gig-import-draft ${draft.status.toLowerCase()}`} key={draft.draftId}>
                    <div data-draft-id={draft.draftId} data-testid="gig-import-draft-row">
                    <div className="gig-import-draft-header">
                      <div>
                        <strong>{draft.title || draft.projectName || 'Untitled row'}</strong>
                        <span>
                          {formatGigType(draft.gigType)} · {draft.date ? formatDate(draft.date) : 'Missing date'} · {draft.venueName || draft.venueAddress || 'Missing location'}
                        </span>
                      </div>
                      <span className={`status-pill confidence-${draft.confidence.toLowerCase()}`}>
                        {draft.confidence}
                      </span>
                    </div>

                    {(draft.warnings.length > 0 || draft.missingFields.length > 0) && (
                      <div className="import-warning-list">
                        {draft.missingFields.map((field) => (
                          <span key={field}>Missing {field}</span>
                        ))}
                        {draft.warnings.map((warning) => (
                          <span key={warning}>{warning}</span>
                        ))}
                      </div>
                    )}

                    <div className="form-grid compact-form-grid">
                      <label>
                        <span>Client</span>
                        <select
                          data-testid="gig-import-draft-client-select"
                          value={draft.proposedClientId ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'proposedClientId', event.target.value)
                          }
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
                        <span>Source client</span>
                        <input
                          data-testid="gig-import-draft-source-client-input"
                          value={draft.clientName ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'clientName', event.target.value)
                          }
                        />
                      </label>
                      <label className="full-width">
                        <span>Title</span>
                        <input
                          data-testid="gig-import-draft-title-input"
                          value={draft.title ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'title', event.target.value)
                          }
                        />
                      </label>
                      <label>
                        <span>Date</span>
                        <input
                          data-testid="gig-import-draft-date-input"
                          type="date"
                          value={draft.date ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'date', event.target.value)
                          }
                        />
                      </label>
                      <label>
                        <span>Type</span>
                        <select
                          data-testid="gig-import-draft-type-select"
                          value={draft.gigType}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'gigType', event.target.value)
                          }
                        >
                          <option value="Performance">Performance</option>
                          <option value="Teaching">Teaching</option>
                          <option value="Rehearsal">Rehearsal</option>
                          <option value="Recording">Recording</option>
                          <option value="Admin">Admin</option>
                          <option value="Other">Other work</option>
                        </select>
                      </label>
                      <label>
                        <span>Fee</span>
                        <input
                          data-testid="gig-import-draft-fee-input"
                          inputMode="decimal"
                          value={draft.fee ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'fee', event.target.value)
                          }
                        />
                      </label>
                      <label>
                        <span>Per diem</span>
                        <input
                          data-testid="gig-import-draft-per-diem-input"
                          inputMode="decimal"
                          value={draft.perDiem ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'perDiem', event.target.value)
                          }
                        />
                      </label>
                      <label>
                        <span>Confidence</span>
                        <select
                          value={draft.confidence}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'confidence', event.target.value)
                          }
                        >
                          <option value="High">High</option>
                          <option value="Medium">Medium</option>
                          <option value="Low">Low</option>
                        </select>
                      </label>
                      <label className="full-width">
                        <span>Location</span>
                        <input
                          data-testid="gig-import-draft-venue-input"
                          value={draft.venueName ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'venueName', event.target.value)
                          }
                        />
                      </label>
                      <label className="full-width">
                        <span>Address</span>
                        <input
                          value={draft.venueAddress ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'venueAddress', event.target.value)
                          }
                        />
                      </label>
                      <label>
                        <span>Postcode</span>
                        <input
                          value={draft.postcode ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'postcode', event.target.value)
                          }
                        />
                      </label>
                      <label className="full-width">
                        <span>Notes</span>
                        <textarea
                          rows={3}
                          value={draft.notes ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'notes', event.target.value)
                          }
                        />
                      </label>
                      <label className="full-width">
                        <span>Source reference</span>
                        <input
                          data-testid="gig-import-draft-source-reference-input"
                          value={draft.sourceReference ?? ''}
                          disabled={isCommitted}
                          onChange={(event) =>
                            onUpdateDraftField(draft.draftId, 'sourceReference', event.target.value)
                          }
                        />
                      </label>
                    </div>

                    <div className="gig-import-row-actions">
                      <span>
                        {draft.fee !== null ? formatCurrency(draft.fee) : 'No fee'}{draft.perDiem ? ` + ${formatCurrency(draft.perDiem)}` : ''}
                      </span>
                      <button
                        className="ghost-button"
                        data-testid="gig-import-draft-accept-button"
                        disabled={isGigImportLoading || isCommitted}
                        onClick={() => onSetDraftStatus(draft, 'Accepted')}
                        type="button"
                      >
                        Accept
                      </button>
                      <button
                        className="danger-button"
                        data-testid="gig-import-draft-reject-button"
                        disabled={isGigImportLoading || isCommitted}
                        onClick={() => onSetDraftStatus(draft, 'Rejected')}
                        type="button"
                      >
                        Reject
                      </button>
                      {isRejected && (
                        <button
                          className="ghost-button"
                          disabled={isGigImportLoading}
                          onClick={() => onSetDraftStatus(draft, 'Pending')}
                          type="button"
                        >
                          Reopen
                        </button>
                      )}
                    </div>
                    </div>
                  </article>
                )
              })}
            </div>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a batch to review staged rows.</strong>
              <p>Accepted rows can be committed into live gig records.</p>
            </div>
          )}
        </div>
      </div>
      </section>
    </div>
  )
}
