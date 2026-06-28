import { useEffect } from 'react'
import { formatDate } from '../formatters'
import type { QuickGigCandidate } from '../types'

type QuickCaptureGigSelectProps = {
  candidates: QuickGigCandidate[]
  clientNamesById: ReadonlyMap<string, string>
  emptyMessage: string
  isSaving: boolean
  onSelectedGigChange: (gigId: string) => void
  selectedGigId: string
}

export function QuickCaptureGigSelect({
  candidates,
  clientNamesById,
  emptyMessage,
  isSaving,
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

  if (candidates.length === 0) {
    if (isSaving) {
      return null
    }

    return (
      <div className="empty-state">
        <strong>No candidate gigs are available.</strong>
        <p>{emptyMessage}</p>
      </div>
    )
  }

  return (
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
            {gig.title} · {formatDate(gig.date)} · {gig.venue} ·{' '}
            {clientNamesById.get(gig.clientId) ?? 'Unknown client'} ·{' '}
            {gig.daysFromToday === 0
              ? 'today'
              : `${gig.daysFromToday} day${gig.daysFromToday === 1 ? '' : 's'} away`}
          </option>
        ))}
      </select>
    </label>
  )
}
