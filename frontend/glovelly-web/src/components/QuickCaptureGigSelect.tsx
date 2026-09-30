import { useEffect } from 'react'
import { formatDate, formatGigType } from '../formatters'
import type { QuickGigCandidate } from '../types'

type QuickCaptureGigSelectProps = {
  candidates: QuickGigCandidate[]
  clientNamesById: ReadonlyMap<string, string>
  emptyMessage: string
  hasMoreCandidates: boolean
  isLoadingCandidates: boolean
  isSaving: boolean
  loadCandidatesError: string
  onLoadMoreCandidates: () => void
  onSelectedGigChange: (gigId: string) => void
  selectedGigId: string
}

export function QuickCaptureGigSelect({
  candidates,
  clientNamesById,
  emptyMessage,
  hasMoreCandidates,
  isLoadingCandidates,
  isSaving,
  loadCandidatesError,
  onLoadMoreCandidates,
  onSelectedGigChange,
  selectedGigId,
}: QuickCaptureGigSelectProps) {
  const selectedValue = candidates.some((candidate) => candidate.id === selectedGigId)
    ? selectedGigId
    : candidates.find((candidate) => candidate.isSelected)?.id ?? candidates[0]?.id ?? ''

  useEffect(() => {
    if (selectedValue && selectedValue !== selectedGigId) {
      onSelectedGigChange(selectedValue)
    }
  }, [onSelectedGigChange, selectedGigId, selectedValue])

  return (
    <div className="quick-capture-gig-picker">
      {candidates.length > 0 ? (
        <label className="quick-receipt-select">
          <span>Gig</span>
          <select
            data-testid="quick-capture-gig-select"
            value={selectedValue}
            onChange={(event) => onSelectedGigChange(event.target.value)}
            disabled={isSaving}
          >
            {candidates.map((gig) => (
              <option key={gig.id} value={gig.id}>
                {gig.title} · {formatGigType(gig.type)} · {formatDate(gig.date)} · {gig.venue} ·{' '}
                {clientNamesById.get(gig.clientId) ?? 'Unknown client'} ·{' '}
                {gig.daysFromToday === 0
                  ? 'today'
                  : `${gig.daysFromToday} day${gig.daysFromToday === 1 ? '' : 's'} away`}
              </option>
            ))}
          </select>
        </label>
      ) : !isSaving ? (
        <div className="empty-state">
          <strong>No nearby candidate gigs are available.</strong>
          <p>{emptyMessage}</p>
        </div>
      ) : null}
      {hasMoreCandidates ? (
        <button
          className="ghost-button quick-capture-load-more"
          disabled={isSaving || isLoadingCandidates}
          onClick={onLoadMoreCandidates}
          type="button"
        >
          {isLoadingCandidates ? 'Loading gigs...' : 'Load more gigs'}
        </button>
      ) : candidates.length > 0 ? (
        <p className="form-hint">No more gigs are available.</p>
      ) : null}
      {loadCandidatesError ? <p className="form-error">{loadCandidatesError}</p> : null}
    </div>
  )
}
