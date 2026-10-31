import type { useUnifiedIntake } from '../hooks/useUnifiedIntake'
import { useEffect, useRef } from 'react'
import { QuickCaptureGigSelect } from './QuickCaptureGigSelect'
import { GigResourceFields } from './GigResourceFields'
import { ReceiptAnalysisResults } from './ReceiptAnalysisResults'
import { invoiceSaveMessage } from '../receiptSaveFeedback'
import { buildApiUrl } from '../api'

type Props = {
  workflow: ReturnType<typeof useUnifiedIntake>
  clientNamesById: ReadonlyMap<string, string>
  onGoToGig: (gigId: string) => void
}

export function UnifiedIntakeModal({ workflow, clientNamesById, onGoToGig }: Props) {
  const { state } = workflow
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => { heading.current?.focus() }, [state.phase])
  if (state.phase === 'closed' || state.phase === 'review') return null
  const busy = state.phase === 'upload' && state.status === 'working'
  return <div className="settings-overlay" role="presentation">
    <section aria-labelledby="unified-intake-title" className="settings-modal unified-intake-modal panel" role="dialog" aria-modal="true" data-testid="unified-intake-modal">
      <div className="panel-heading">
        <div><p className="section-label">{state.phase === 'choosing' ? 'Choose a source' : 'Unified upload'}</p><h2 ref={heading} tabIndex={-1} id="unified-intake-title">Add to Glovelly</h2></div>
        <button className="ghost-button" type="button" onClick={workflow.close} disabled={busy}>Close</button>
      </div>

      {state.phase === 'choosing' && <>
        <p className="intake-guidance">What would you like to add?</p>
        <div className="intake-source-options">
          <button className="intake-source-option" type="button" onClick={() => workflow.choose('file')} disabled={state.loading}><strong>Photo or file</strong><span>Receipts, PDFs and gig documents</span></button>
          <button className="intake-source-option" type="button" onClick={() => workflow.choose('url')} disabled={state.loading}><strong>URL</strong><span>A link to a document or resource</span></button>
          <button className="intake-source-option" type="button" onClick={() => workflow.choose('text')} disabled={state.loading}><strong>Paste text</strong><span>Receipt emails or booking notes</span></button>
        </div>
        {state.loading && <p role="status">Checking for an unfinished upload...</p>}
        {(state.recovered || state.saved || state.preparedBatchId) && <button className="ghost-button" type="button" onClick={workflow.resume}>{state.saved ? 'Show saved attachment' : state.preparedBatchId ? 'Show prepared import' : 'Resume unfinished upload'}</button>}
        {state.error && <p className="form-error" role="alert">{state.error}</p>}
      </>}

      {state.phase === 'upload' && <>
        <div className="quick-receipt-summary" role="status">
          <strong>{state.sourceName ?? (state.mode === 'file' ? 'Photo or file' : state.mode === 'url' ? 'URL' : 'Pasted text')}</strong>
          <span>{busy ? state.fields.intent === 'Gigs' ? 'Preparing reviewable gig drafts. No gigs will be created yet.' : 'Saving and analysing your source. Eligible receipts may be attached automatically.' : state.intake ? 'Check the suggestion and choose how to treat this source.' : 'Submit your source to begin. Eligible receipts are saved to the nearest gig automatically.'}</span>
          {busy && <div className="quick-receipt-progress" aria-label="Upload in progress"><span /></div>}
        </div>
        {state.message && <p className="form-error" role="alert">{state.message}</p>}
        {(state.intake?.failureCode === 'document_connection_required' || state.intake?.failureCode === 'document_unavailable') && <p>To analyse this document, <a href={buildApiUrl('/integrations/google-docs/connect')}>connect or reconnect Google Docs</a>. This grants read-only access to your Google Docs. You can also attach the URL as a resource without connecting.</p>}
        {!state.intake && <>
          {state.mode === 'file' && <label className="primary-button file-button">Choose photo or file<input type="file" accept="application/pdf,image/jpeg,image/png,image/webp,image/heic,image/heif,text/plain" onChange={event => { const file = event.target.files?.[0]; event.target.value = ''; if (file) void workflow.submit(file) }} disabled={busy} /></label>}
          {state.mode === 'url' && <label className="intake-input"><span>URL</span><input type="url" value={state.fields.url} onChange={event => workflow.updateField('url', event.target.value)} placeholder="https://..." disabled={busy} /></label>}
          {state.mode === 'text' && <label className="intake-input"><span>Text</span><textarea rows={7} value={state.fields.text} onChange={event => workflow.updateField('text', event.target.value)} maxLength={16000} placeholder="Paste receipt emails or gig notes..." disabled={busy} /></label>}
          {state.mode !== 'file' && <button className="primary-button" type="button" onClick={() => void workflow.submit()} disabled={busy}>Upload and analyse</button>}
          {state.status === 'error' && <button className="ghost-button" type="button" onClick={() => void workflow.retry()}>Recover submitted upload</button>}
          <button className="ghost-button" type="button" onClick={workflow.back} disabled={busy}>Back to source selection</button>
        </>}
        {state.intake && <>
          {state.intake.sourceText && <details><summary>Inspect source text</summary><pre className="intake-source-text">{state.intake.sourceText}</pre></details>}
          {state.intake.sourceType === 'File' && <p><a href={buildApiUrl(`/intake/current/source?intakeId=${encodeURIComponent(state.intake.id)}`)}>Download original source</a></p>}
          {state.intake.sourceUrl && <p><a href={state.intake.sourceUrl} target="_blank" rel="noreferrer">Open original link</a></p>}
          <div className="intake-result">
            <div><strong>{state.intake.proposedIntent === 'Unknown' ? 'Choose a treatment' : `Suggested ${state.intake.proposedIntent === 'Gigs' ? 'gig import' : state.intake.proposedIntent.toLowerCase()}`}</strong><span>{state.intake.confidence} confidence</span></div>
            <div className="receipt-analysis-facts">{state.intake.evidence.map(fact => <span key={`${fact.label}:${fact.value}`}><strong>{fact.label}</strong> {fact.value}</span>)}</div>
            {state.intake.analysis && <ReceiptAnalysisResults analysis={state.intake.analysis} />}
          </div>
          <label className="intake-input"><span>Treat as</span><select value={state.fields.intent} onChange={event => void workflow.setIntent(event.target.value as 'Receipt' | 'Resource' | 'Gigs')} disabled={busy}>
            <option value="" disabled>Choose a treatment</option>{state.mode !== 'url' && <option value="Receipt">Receipt</option>}<option value="Resource">Resource</option><option value="Gigs">Gig import</option>
          </select></label>
          {(state.fields.intent === 'Receipt' || state.fields.intent === 'Resource') && <QuickCaptureGigSelect candidates={state.intake.candidates} clientNamesById={clientNamesById} emptyMessage="Load more gigs to choose a destination, or create a gig first." hasMoreCandidates={state.intake.hasMoreCandidates} isLoadingCandidates={state.candidatesStatus === 'loading'} isSaving={busy} loadCandidatesError={state.candidatesError} onLoadMoreCandidates={() => void workflow.loadMore()} onSelectedGigChange={id => workflow.updateField('selectedGigId', id)} selectedGigId={state.fields.selectedGigId} />}
          {state.fields.intent === 'Gigs' && state.mode === 'url' && !state.intake.sourceText && <p>Read the Google Doc first, or paste the text or upload a PDF to prepare an import.</p>}
          {state.fields.intent === 'Resource' && <GigResourceFields form={state.fields} disabled={busy} onChange={workflow.updateResourceField} showUrl={false} />}
          <div className="form-actions">
            {state.fields.intent === 'Gigs' ? <><button className="primary-button" type="button" onClick={() => void workflow.prepareGigDrafts(false)} disabled={busy || (state.mode === 'url' && !state.intake.sourceText)}>Prepare gig drafts</button><button className="ghost-button" type="button" onClick={() => void workflow.prepareGigDrafts(true)} disabled={busy || (state.mode === 'url' && !state.intake.sourceText)}>Prepare manual gig draft</button></> : state.fields.intent && <button className="primary-button" type="button" onClick={() => void workflow.apply()} disabled={busy || !state.fields.selectedGigId}>Attach {state.fields.intent.toLowerCase()}</button>}
            <button className="ghost-button" type="button" onClick={() => void workflow.retry()} disabled={busy}>Retry analysis</button>
            <button className="danger-button" type="button" onClick={() => void workflow.discard()} disabled={busy}>Discard upload</button>
          </div>
        </>}
      </>}

      {state.phase === 'attached' && <div className="intake-attached" role="status">
        <div><strong>Attached to {state.application.gig.title}</strong><span>Your {state.application.kind.toLowerCase()} is saved. Review it now or come back later.</span></div>
        {state.application.hasNearbyCandidates && <div className="quick-receipt-warning"><strong>Check the gig before moving on.</strong><span>There are other gigs close to this date. The nearest one has been selected, but this receipt may belong somewhere else.</span></div>}
        {state.application.kind === 'Receipt' && invoiceSaveMessage(state.application.invoices ?? [], true) && <p className="form-error">{invoiceSaveMessage(state.application.invoices ?? [], true)}</p>}
        <div className="form-actions"><button className="ghost-button" type="button" onClick={workflow.review}>Review attachment</button>{state.application.kind === 'Resource' && <button className="ghost-button" type="button" onClick={() => onGoToGig(state.application.gig.id)}>Go to gig</button>}<button className="primary-button" type="button" onClick={workflow.close}>Done</button></div>
      </div>}
      {state.phase === 'prepared' && <div className="intake-attached" role="status">
        <div><strong>Draft batch prepared</strong><span>Complete and review the draft before creating a gig.</span></div>
        <div className="form-actions"><button className="ghost-button" type="button" onClick={workflow.reviewImport}>Review import</button><button className="primary-button" type="button" onClick={workflow.close}>Done</button></div>
      </div>}
    </section>
  </div>
}
