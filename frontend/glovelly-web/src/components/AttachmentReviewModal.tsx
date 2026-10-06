import { buildApiUrl } from '../api'
import { useEffect, useRef } from 'react'
import { useAttachmentReview } from '../hooks/useAttachmentReview'
import type { Gig, IntakeApplication, IntakeEvidence, Invoice } from '../types'
import { QuickCaptureGigSelect } from './QuickCaptureGigSelect'
import { IntakeResourceFields } from './IntakeResourceFields'

type Props = {
  application: IntakeApplication; evidence: IntakeEvidence[]; clientNamesById: ReadonlyMap<string, string>
  onClose: () => void; onMergeGig: (gig: Gig) => void; onMergeInvoices: (invoices: Invoice[]) => void
  onSessionExpired: (message: string) => void
}

export function AttachmentReviewModal({ application, evidence, clientNamesById, onClose, onMergeGig, onMergeInvoices, onSessionExpired }: Props) {
  const review = useAttachmentReview({ application, onMergeGig, onMergeInvoices, onDeleted: onClose, onSessionExpired })
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => { heading.current?.focus() }, [])
  const busy = review.operation !== 'idle'
  const expense = review.saved.gig.expenses.find(value => value.id === review.saved.expenseId)
  const attachments = expense?.attachments ?? review.saved.resource?.attachments ?? []
  const root = review.saved.kind === 'Receipt'
    ? `/gigs/${review.saved.gig.id}/expenses/${review.saved.expenseId}`
    : `/gigs/${review.saved.gig.id}/external-resources/${review.saved.resource!.id}`
  return <div className="settings-overlay" role="presentation">
    <section className="settings-modal unified-intake-modal panel" role="dialog" aria-modal="true" aria-labelledby="attachment-review-title" data-testid="attachment-review-modal">
      <div className="panel-heading"><div><p className="section-label">Saved {review.saved.kind.toLowerCase()}</p><h2 ref={heading} tabIndex={-1} id="attachment-review-title">Review attachment</h2></div><button type="button" className="ghost-button" onClick={onClose} disabled={busy}>Done</button></div>
      <div className="quick-receipt-summary"><strong>Attached to {review.saved.gig.title}</strong><span>This item is already saved. Changes here are optional.</span></div>
      <div className="form-actions">
        {attachments.map(attachment => <a key={attachment.id} className="ghost-button" href={buildApiUrl(`${root}/attachments/${attachment.id}`)} target="_blank" rel="noreferrer">Open {attachment.fileName}</a>)}
        {review.saved.resource?.url && <a className="ghost-button" href={review.saved.resource.url} target="_blank" rel="noreferrer">Open original link</a>}
      </div>
      {evidence.length > 0 && <div className="receipt-analysis-facts">{evidence.map(fact => <span key={`${fact.label}:${fact.value}`}><strong>{fact.label}</strong> {fact.value}</span>)}</div>}
      <QuickCaptureGigSelect candidates={review.candidates} clientNamesById={clientNamesById} emptyMessage="Load gigs to choose a destination." hasMoreCandidates={review.page.hasMore} isLoadingCandidates={review.candidateState === 'loading'} isSaving={busy} loadCandidatesError={review.candidateError} onLoadMoreCandidates={() => void review.loadMore()} onSelectedGigChange={id => review.updateField('selectedGigId', id)} selectedGigId={review.fields.selectedGigId} />
      {review.saved.kind === 'Receipt' ? <>
        <div className="form-grid">
          <label><span>Description</span><input value={review.description} onChange={event => review.setDescription(event.target.value)} disabled={busy} /></label>
          <label><span>Amount</span><input inputMode="decimal" value={review.amount} onChange={event => review.setAmount(event.target.value)} disabled={busy} /></label>
          <label><span>Category</span><select value={review.category} onChange={event => review.setCategory(event.target.value as typeof review.category)} disabled={busy}><option value="">No category</option>{['Travel', 'Meals', 'Accommodation', 'Equipment', 'Other'].map(category => <option key={category}>{category}</option>)}</select></label>
        </div>
        <p className="form-hint">Receipt changes refresh linked draft invoices only. Issued and other non-draft invoices remain unchanged.</p>
      </> : <>
        <IntakeResourceFields fields={review.fields} disabled={busy} onChange={review.updateField} />
        <label className="intake-input"><span>URL</span><input type="url" value={review.fields.url} onChange={event => review.updateField('url', event.target.value)} disabled={busy} /></label>
      </>}
      {review.status && <p role="status">{review.status}</p>}
      <div className="form-actions"><button type="button" className="primary-button" disabled={busy} onClick={() => void review.save()}>{review.operation === 'saving' ? 'Saving...' : 'Save changes'}</button><button type="button" className="danger-button" disabled={busy} onClick={() => void review.remove()}>{review.operation === 'deleting' ? 'Deleting...' : 'Delete attachment'}</button></div>
    </section>
  </div>
}
