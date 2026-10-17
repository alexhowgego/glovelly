import { buildApiUrl } from '../api'
import { useEffect, useRef } from 'react'
import { useAttachmentReview } from '../hooks/useAttachmentReview'
import type { Gig, IntakeApplication, IntakeEvidence, Invoice } from '../types'
import { QuickCaptureGigSelect } from './QuickCaptureGigSelect'
import { GigResourceFields } from './GigResourceFields'
import { ExpenseFields } from './ExpenseFields'
import { ReceiptAnalysisResults } from './ReceiptAnalysisResults'

type Props = {
  application: IntakeApplication; evidence: IntakeEvidence[]; clientNamesById: ReadonlyMap<string, string>
  onClose: () => void; onMergeGig: (gig: Gig) => void; onMergeInvoices: (invoices: Invoice[]) => void
  onSessionExpired: (message: string) => void
  onGoToGig?: (gig: Gig) => void
}

export function AttachmentReviewModal({ application, evidence, clientNamesById, onClose, onMergeGig, onMergeInvoices, onSessionExpired, onGoToGig }: Props) {
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
      {review.saved.hasNearbyCandidates && <div className="quick-receipt-warning"><strong>Check the gig before moving on.</strong><span>There are other gigs close to this date. You can change the destination below.</span></div>}
      <div className="form-actions">
        {attachments.map(attachment => <a key={attachment.id} className="ghost-button" href={buildApiUrl(`${root}/attachments/${attachment.id}`)} target="_blank" rel="noreferrer">Open {attachment.fileName}</a>)}
        {review.saved.resource?.url && <a className="ghost-button" href={review.saved.resource.url} target="_blank" rel="noreferrer">Open original link</a>}
      </div>
      {evidence.length > 0 && <div className="receipt-analysis-facts">{evidence.map(fact => <span key={`${fact.label}:${fact.value}`}><strong>{fact.label}</strong> {fact.value}</span>)}</div>}
      <QuickCaptureGigSelect candidates={review.candidates} clientNamesById={clientNamesById} emptyMessage="Load gigs to choose a destination." hasMoreCandidates={review.page.hasMore} isLoadingCandidates={review.candidateState === 'loading'} isSaving={busy} loadCandidatesError={review.candidateError} onLoadMoreCandidates={() => void review.loadMore()} onSelectedGigChange={id => review.updateField('selectedGigId', id)} selectedGigId={review.fields.selectedGigId} />
      {review.saved.kind === 'Receipt' ? <>
        <ExpenseFields amount={review.amount} description={review.description} category={review.category} disabled={busy} onAmount={review.setAmount} onDescription={review.setDescription} onCategory={review.setCategory} />
        {review.analysis && <ReceiptAnalysisResults analysis={review.analysis} onApply={review.useSuggestions} disabled={busy} />}
        <button type="button" className="ghost-button ai-button" disabled={busy} onClick={() => void review.analyse()}>{review.operation === 'analysing' ? 'Analysing...' : review.analysis ? 'Analyse again' : 'Analyse receipt'}</button>
        <p className="form-hint">Receipt changes refresh linked draft invoices only. Issued and other non-draft invoices remain unchanged.</p>
      </> : <>
        <GigResourceFields form={review.fields} disabled={busy} onChange={review.updateResourceField} />
      </>}
      {review.status && <p role="status">{review.status}</p>}
      <div className="form-actions"><button type="button" className="primary-button" disabled={busy} onClick={() => void review.save()}>{review.operation === 'saving' ? 'Saving...' : 'Save changes'}</button>{onGoToGig && <button type="button" className="ghost-button" disabled={busy} onClick={() => onGoToGig(review.saved.gig)}>Go to gig</button>}<button type="button" className="danger-button" disabled={busy} onClick={() => void review.remove()}>{review.operation === 'deleting' ? 'Deleting...' : review.saved.kind === 'Receipt' ? 'Delete receipt expense' : 'Delete resource'}</button></div>
    </section>
  </div>
}
