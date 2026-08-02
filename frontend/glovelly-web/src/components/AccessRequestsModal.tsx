import { useEffect, useState } from 'react'
import { formatDate } from '../formatters'
import type { AccessRequest } from '../types'

type AccessRequestsModalProps = {
  accessRequestStatus: string
  accessRequests: AccessRequest[]
  isLoading: boolean
  isOpen: boolean
  onApprove: (approval: {
    role: 'Admin' | 'User'
    isActive: boolean
    sendInvitationEmail: boolean
    decisionNote?: string
  }) => void
  onClose: () => void
  onDecline: (decisionNote?: string) => void
  onRefresh: () => void
  onSelect: (request: AccessRequest) => void
  selectedAccessRequest: AccessRequest | null
}

export function AccessRequestsModal({
  accessRequestStatus,
  accessRequests,
  isLoading,
  isOpen,
  onApprove,
  onClose,
  onDecline,
  onRefresh,
  onSelect,
  selectedAccessRequest,
}: AccessRequestsModalProps) {
  const [role, setRole] = useState<'Admin' | 'User'>('User')
  const [isActive, setIsActive] = useState(true)
  const [sendInvitationEmail, setSendInvitationEmail] = useState(true)
  const [decisionNote, setDecisionNote] = useState('')
  const [isDeclineConfirmationOpen, setIsDeclineConfirmationOpen] = useState(false)
  const canDecide = selectedAccessRequest?.status === 'Pending'

  useEffect(() => {
    setRole('User')
    setIsActive(true)
    setSendInvitationEmail(true)
    setDecisionNote('')
    setIsDeclineConfirmationOpen(false)
  }, [selectedAccessRequest?.id])

  if (!isOpen) {
    return null
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
      <section
        aria-labelledby="access-requests-title"
        aria-modal="true"
        className="settings-modal access-requests-modal panel"
        data-testid="access-requests-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="panel-heading">
          <div>
            <p className="section-label">Administrator review</p>
            <h2 id="access-requests-title">Access requests</h2>
          </div>
          <div className="actions">
            <button className="ghost-button" disabled={isLoading} onClick={onRefresh} type="button">
              Refresh
            </button>
            <button className="ghost-button" disabled={isLoading} onClick={onClose} type="button">
              Close
            </button>
          </div>
        </div>

        <p aria-live="polite" className="access-request-status">
          {accessRequestStatus}
        </p>

        <div className="access-request-workspace">
          <div className="client-list access-request-list" aria-label="Pending access requests">
            {accessRequests.map((request) => (
              <button
                className={`client-card ${selectedAccessRequest?.id === request.id ? 'selected' : ''}`}
                key={request.id}
                onClick={() => onSelect(request)}
                type="button"
              >
                <div>
                  <strong>{request.displayName || request.email}</strong>
                  <span>{request.email}</span>
                </div>
                <small className="gig-card-meta">Requested {formatDate(request.requestedAtUtc.slice(0, 10))}</small>
              </button>
            ))}
            {accessRequests.length === 0 && (
              <div className="empty-state">
                <strong>No pending access requests.</strong>
                <p>New requests will appear here for administrator review.</p>
              </div>
            )}
          </div>

          <div className="access-request-detail">
            {selectedAccessRequest ? (
              <>
                <div className="access-request-identity">
                  <p className="section-label">Requester identity</p>
                  <strong>{selectedAccessRequest.displayName || 'No display name supplied'}</strong>
                  <span>{selectedAccessRequest.email}</span>
                  <span>Requested {formatDate(selectedAccessRequest.requestedAtUtc.slice(0, 10))}</span>
                  <span>Status: {selectedAccessRequest.status}</span>
                  {selectedAccessRequest.decisionAtUtc && (
                    <span>Decided {formatDate(selectedAccessRequest.decisionAtUtc.slice(0, 10))}</span>
                  )}
                  {selectedAccessRequest.decisionNote && (
                    <span>Decision note: {selectedAccessRequest.decisionNote}</span>
                  )}
                </div>

                {!canDecide && (
                  <div className="empty-state">
                    <strong>This request can no longer be decided.</strong>
                    <p>{selectedAccessRequest.status} requests cannot be changed.</p>
                  </div>
                )}

                {canDecide && (
                  <div className="access-request-form">
                    <label>
                      <span>Role</span>
                      <select value={role} onChange={(event) => setRole(event.target.value as 'Admin' | 'User')}>
                        <option value="User">User</option>
                        <option value="Admin">Administrator</option>
                      </select>
                    </label>
                    <label className="checkbox-field">
                      <input checked={isActive} onChange={(event) => setIsActive(event.target.checked)} type="checkbox" />
                      <span>Activate account immediately</span>
                    </label>
                    <label className="checkbox-field">
                      <input checked={sendInvitationEmail} onChange={(event) => setSendInvitationEmail(event.target.checked)} type="checkbox" />
                      <span>Send invitation email</span>
                    </label>
                    <label>
                      <span>Decision note (optional)</span>
                      <textarea rows={3} value={decisionNote} onChange={(event) => setDecisionNote(event.target.value)} />
                    </label>
                    {isDeclineConfirmationOpen ? (
                      <div className="access-request-decline-confirmation" role="group" aria-label="Confirm decline">
                        <strong>Decline this access request?</strong>
                        <span>This cannot be undone.</span>
                        <div className="actions">
                          <button className="ghost-button" disabled={isLoading} onClick={() => setIsDeclineConfirmationOpen(false)} type="button">
                            Cancel
                          </button>
                          <button className="danger-button" disabled={isLoading} onClick={() => onDecline(decisionNote.trim() || undefined)} type="button">
                            Confirm decline
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div className="actions">
                        <button className="primary-button" disabled={isLoading} onClick={() => onApprove({ role, isActive, sendInvitationEmail, decisionNote: decisionNote.trim() || undefined })} type="button">
                          Approve access
                        </button>
                        <button className="danger-button" disabled={isLoading} onClick={() => setIsDeclineConfirmationOpen(true)} type="button">
                          Decline
                        </button>
                      </div>
                    )}
                  </div>
                )}
              </>
            ) : (
              <div className="empty-state roomy">
                <strong>Select an access request to review it.</strong>
                <p>Only pending requests can be approved or declined.</p>
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  )
}
