import type { useUnifiedIntake } from '../hooks/useUnifiedIntake'
import { useEffect, useRef } from 'react'
import { QuickCaptureGigSelect } from './QuickCaptureGigSelect'
import { IntakeResourceFields } from './IntakeResourceFields'

type Props = {
  workflow: ReturnType<typeof useUnifiedIntake>
  clientNamesById: ReadonlyMap<string, string>
}

export function UnifiedIntakeModal({ workflow, clientNamesById }: Props) {
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
        {(state.recovered || state.saved) && <button className="ghost-button" type="button" onClick={workflow.resume}>{state.saved ? 'Show saved attachment' : 'Resume unfinished upload'}</button>}
        {state.error && <p className="form-error" role="alert">{state.error}</p>}
      </>}

      {state.phase === 'upload' && <>
        <div className="quick-receipt-summary" role="status">
          <strong>{state.sourceName ?? (state.mode === 'file' ? 'Photo or file' : state.mode === 'url' ? 'URL' : 'Pasted text')}</strong>
          <span>{busy ? 'Saving your source, analysing it and attaching eligible receipts...' : state.intake ? 'Choose how to attach this item. You can review its details after saving.' : 'Submit your source to begin. Eligible receipts are saved to the nearest gig automatically.'}</span>
          {busy && <div className="quick-receipt-progress" aria-label="Upload in progress"><span /></div>}
        </div>
        {state.message && <p className="form-error" role="alert">{state.message}</p>}
        {!state.intake && <>
          {state.mode === 'file' && <label className="primary-button file-button">Choose photo or file<input type="file" accept="application/pdf,image/jpeg,image/png,image/webp,image/heic,image/heif,text/plain" onChange={event => { const file = event.target.files?.[0]; event.target.value = ''; if (file) void workflow.submit(file) }} disabled={busy} /></label>}
          {state.mode === 'url' && <label className="intake-input"><span>URL</span><input type="url" value={state.fields.url} onChange={event => workflow.updateField('url', event.target.value)} placeholder="https://..." disabled={busy} /></label>}
          {state.mode === 'text' && <label className="intake-input"><span>Text</span><textarea rows={7} value={state.fields.text} onChange={event => workflow.updateField('text', event.target.value)} maxLength={16000} placeholder="Paste receipt emails or gig notes..." disabled={busy} /></label>}
          {state.mode !== 'file' && <button className="primary-button" type="button" onClick={() => void workflow.submit()} disabled={busy}>Upload and analyse</button>}
          {state.status === 'error' && <button className="ghost-button" type="button" onClick={() => void workflow.retry()}>Recover submitted upload</button>}
          <button className="ghost-button" type="button" onClick={workflow.back} disabled={busy}>Back to source selection</button>
        </>}
        {state.intake && <>
          <div className="intake-result">
            <div><strong>{state.intake.proposedIntent === 'Unknown' ? 'Choose an attachment type' : `Suggested ${state.intake.proposedIntent.toLowerCase()}`}</strong><span>{state.intake.confidence} confidence</span></div>
            <div className="receipt-analysis-facts">{state.intake.evidence.map(fact => <span key={`${fact.label}:${fact.value}`}><strong>{fact.label}</strong> {fact.value}</span>)}</div>
          </div>
          <label className="intake-input"><span>Attach as</span><select value={state.fields.intent} onChange={event => workflow.updateField('intent', event.target.value as 'Receipt' | 'Resource')} disabled={busy}>
            {state.mode !== 'url' && <option value="Receipt">Receipt</option>}<option value="Resource">Resource</option>
          </select></label>
          <QuickCaptureGigSelect candidates={state.intake.candidates} clientNamesById={clientNamesById} emptyMessage="Load more gigs to choose a destination, or create a gig first." hasMoreCandidates={state.intake.hasMoreCandidates} isLoadingCandidates={state.candidatesStatus === 'loading'} isSaving={busy} loadCandidatesError={state.candidatesError} onLoadMoreCandidates={() => void workflow.loadMore()} onSelectedGigChange={id => workflow.updateField('selectedGigId', id)} selectedGigId={state.fields.selectedGigId} />
          {state.fields.intent === 'Resource' && <IntakeResourceFields fields={state.fields} disabled={busy} onChange={workflow.updateField} />}
          <div className="form-actions">
            <button className="primary-button" type="button" onClick={() => void workflow.apply()} disabled={busy || !state.fields.selectedGigId}>Attach {state.fields.intent.toLowerCase()}</button>
            <button className="ghost-button" type="button" onClick={() => void workflow.retry()} disabled={busy}>Retry analysis</button>
            <button className="danger-button" type="button" onClick={() => void workflow.discard()} disabled={busy}>Discard upload</button>
          </div>
        </>}
      </>}

      {state.phase === 'attached' && <div className="intake-attached" role="status">
        <div><strong>Attached to {state.application.gig.title}</strong><span>Your {state.application.kind.toLowerCase()} is saved. Review it now or come back later.</span></div>
        <div className="form-actions"><button className="ghost-button" type="button" onClick={workflow.review}>Review attachment</button><button className="primary-button" type="button" onClick={workflow.close}>Done</button></div>
      </div>}
    </section>
  </div>
}
