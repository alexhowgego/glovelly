import { useCallback, useEffect, useRef, useState } from 'react'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage, handleSessionExpired, jsonRequestInit } from '../api'
import type { GigExternalResourceForm, Intake, IntakeApplication, IntakeEvidence } from '../types'
import { getQuickCaptureCandidatePage } from '../quickCaptureCandidates'

export type IntakeSourceMode = 'file' | 'url' | 'text'
export type IntakeFields = GigExternalResourceForm & {
  text: string; selectedGigId: string
  intent: '' | 'Receipt' | 'Resource' | 'Gigs'
}
export type IntakeUploadState = {
  phase: 'upload'; status: 'editing' | 'working' | 'ready' | 'error'
  mode: IntakeSourceMode; id: string; intake: Intake | null; fields: IntakeFields
  sourceName: string | null
  submitted: { file?: File; mode: IntakeSourceMode; value: string } | null
  message: string; candidatesStatus: 'idle' | 'loading'; candidatesError: string
}
export type IntakeState =
  | { phase: 'closed' }
  | { phase: 'choosing'; loading: boolean; recovered: Intake | null; saved: IntakeApplication | null; preparedBatchId?: string | null; error: string }
  | IntakeUploadState
  | { phase: 'attached' | 'review'; application: IntakeApplication; evidence: IntakeEvidence[] }
  | { phase: 'prepared'; batchId: string }

const recoveryKey = 'glovelly-current-intake-id'
const emptyFields = (): IntakeFields => ({ url: '', text: '', selectedGigId: '', resourceType: 'File', purpose: 'Other', title: '', notes: '', isPrimary: false, intent: 'Receipt' })
const modeFor = (intake: Intake): IntakeSourceMode => intake.sourceType === 'File' ? 'file' : intake.sourceType === 'Url' ? 'url' : 'text'

function upload(mode: IntakeSourceMode, intake: Intake | null = null): IntakeUploadState {
  return {
    phase: 'upload', status: intake ? 'ready' : 'editing', mode, id: intake?.id ?? crypto.randomUUID(), intake,
    sourceName: intake?.sourceName ?? null,
    submitted: null,
    fields: {
      ...emptyFields(), url: intake?.sourceUrl ?? '', text: intake?.sourceText ?? '',
      selectedGigId: intake?.candidates.find(candidate => candidate.isSelected)?.id ?? intake?.candidates[0]?.id ?? '',
      resourceType: intake?.suggestedResourceType ?? (mode === 'url' ? 'Url' : 'File'),
      title: intake?.sourceName ?? '', intent: intake?.intentOverride && intake.intentOverride !== 'Unknown' ? intake.intentOverride : intake?.proposedIntent && intake.proposedIntent !== 'Unknown' ? intake.proposedIntent : '',
    },
    message: intake?.applicationError ?? intake?.preparationError ?? intake?.failureMessage ?? '', candidatesStatus: 'idle', candidatesError: '',
  }
}

type Options = { onApplied: (application: IntakeApplication) => void; onReviewImport: (batchId: string) => void; onSessionExpired: (message: string) => void }

export function useUnifiedIntake({ onApplied, onReviewImport, onSessionExpired }: Options) {
  const [state, setState] = useState<IntakeState>({ phase: 'closed' })
  const generation = useRef(0)
  const commandPending = useRef(false)
  const trigger = useRef<HTMLElement | null>(null)
  useEffect(() => () => { ++generation.current; commandPending.current = false }, [])
  const reset = useCallback(() => {
    ++generation.current
    commandPending.current = false
    sessionStorage.removeItem(recoveryKey)
    setState({ phase: 'closed' })
  }, [])

  const request = async (path: string, init?: RequestInit, version = generation.current) => {
    if (version !== generation.current) throw new Error('The upload session changed.')
    const response = await fetchWithSession(buildApiUrl(path), init)
    if (version !== generation.current) throw new Error('The upload session changed.')
    if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again to add attachments.')) throw new Error('Sign in again to continue.')
    return response
  }
  const applied = (application: IntakeApplication, evidence: IntakeEvidence[] = [], version = generation.current) => {
    if (version !== generation.current) return
    setState({ phase: 'attached', application, evidence })
    onApplied(application)
  }

  const open = async () => {
    if (state.phase === 'closed') trigger.current = document.activeElement as HTMLElement | null
    const version = ++generation.current
    setState({ phase: 'choosing', loading: true, recovered: null, saved: null, error: '' })
    try {
      let saved: IntakeApplication | null = null
      let preparedBatchId: string | null = null
      const id = sessionStorage.getItem(recoveryKey)
      if (id) {
        const response = await request(`/intake/applications/${id}`, undefined, version)
        if (response.ok) saved = await response.json() as IntakeApplication
        else if (response.status !== 404) throw new Error(await getResponseErrorMessage(response, 'Unable to recover your attachment.'))
        if (!saved) {
          const prepared = await request(`/intake/gig-drafts/${id}`, undefined, version)
          if (prepared.ok) preparedBatchId = (await prepared.json() as { batchId: string }).batchId
          else if (prepared.status !== 404) throw new Error(await getResponseErrorMessage(prepared, 'Unable to recover your import.'))
        }
      }
      const response = await request('/intake/current', undefined, version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to recover your upload.'))
      const recovered = response.status === 204 ? null : await response.json() as Intake
      if (version === generation.current) setState({ phase: 'choosing', loading: false, recovered, saved, preparedBatchId, error: '' })
    } catch (error) {
      if (version === generation.current) setState({ phase: 'choosing', loading: false, recovered: null, saved: null, error: error instanceof Error ? error.message : 'Unable to recover your upload.' })
    }
  }

  const choose = (mode: IntakeSourceMode) => setState(upload(mode))
  const resume = () => {
    if (state.phase !== 'choosing') return
    if (state.saved) applied(state.saved)
    else if (state.preparedBatchId) setState({ phase: 'prepared', batchId: state.preparedBatchId })
    else if (state.recovered) {
      const recovered = state.recovered
      setState(upload(modeFor(recovered), recovered))
      if (recovered.preparationState === 'Pending' || recovered.preparationState === 'Running') {
        setState(current => current.phase === 'upload' ? { ...current, status: 'working' } : current)
        void waitForPreparation(recovered.id, generation.current).catch(error => setState(current => current.phase === 'upload' && current.id === recovered.id ? { ...current, status: 'error', message: error instanceof Error ? error.message : 'Unable to recover your import.' } : current))
      }
      else if (recovered.analysisInBackground) {
        setState(current => current.phase === 'upload' ? { ...current, status: 'working' } : current)
        void waitForAnalysis(recovered.id, generation.current).catch(error => setState(current => current.phase === 'upload' && current.id === recovered.id ? { ...current, status: 'error', message: error instanceof Error ? error.message : 'Unable to recover analysis.' } : current))
      }
    }
  }
  const close = () => {
    if (commandPending.current) return
    if (state.phase === 'attached' || state.phase === 'review' || state.phase === 'prepared') sessionStorage.removeItem(recoveryKey)
    ++generation.current
    setState({ phase: 'closed' })
    trigger.current?.focus()
  }
  const back = () => { if (!commandPending.current) void open() }
  const review = () => { if (state.phase === 'attached') setState({ ...state, phase: 'review' }) }
  const updateField = <K extends keyof IntakeFields>(key: K, value: IntakeFields[K]) => {
    setState(current => current.phase === 'upload' ? { ...current, fields: { ...current.fields, [key]: value } } : current)
  }
  const updateResourceField = <K extends keyof GigExternalResourceForm>(key: K, value: GigExternalResourceForm[K]) => {
    setState(current => current.phase === 'upload' ? { ...current, fields: { ...current.fields, [key]: value } } : current)
  }

  const receive = async (value: Intake, version: number): Promise<void> => {
    if (version !== generation.current) return
    if (value.application) applied(value.application, value.evidence ?? [], version)
    else {
      setState({ ...upload(modeFor(value), value), status: value.analysisInBackground ? 'working' : 'ready' })
      if (value.analysisInBackground) await waitForAnalysis(value.id, version)
    }
  }
  const run = async (work: (version: number) => Promise<void>, sourceName?: string) => {
    if (commandPending.current || state.phase !== 'upload') return
    commandPending.current = true
    const version = generation.current
    setState({ ...state, status: 'working', message: '', sourceName: sourceName ?? state.sourceName })
    try { await work(version) }
    catch (error) {
      if (version === generation.current) setState(current => current.phase === 'upload' ? { ...current, status: 'error', message: error instanceof Error ? error.message : 'Unable to complete this upload.' } : current)
    }
    finally { if (version === generation.current) commandPending.current = false }
  }

  const recoverSaved = async (id: string, version: number): Promise<boolean> => {
    const response = await request(`/intake/applications/${id}`, undefined, version)
    if (response.ok) {
      applied(await response.json() as IntakeApplication, [], version)
      return true
    }
    if (response.status !== 404) return false
    const prepared = await request(`/intake/gig-drafts/${id}`, undefined, version)
    if (!prepared.ok) return false
    const result = await prepared.json() as { batchId: string }
    if (version === generation.current) setState({ phase: 'prepared', batchId: result.batchId })
    return true
  }
  const waitForAnalysis = async (id: string, version: number) => {
    for (let attempt = 0; attempt < 180; attempt++) {
      await new Promise(resolve => window.setTimeout(resolve, 1000))
      const response = await request('/intake/current', undefined, version)
      if (response.status === 204) {
        if (await recoverSaved(id, version)) return
        throw new Error('This source is no longer available. Reopen Add to Glovelly to recover it.')
      }
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to recover analysis.'))
      const value = await response.json() as Intake
      if (value.id !== id) throw new Error('This source was replaced. Reopen Add to Glovelly.')
      if (!value.analysisInBackground) { await receive(value, version); return }
    }
    throw new Error('Analysis is still running. Close and reopen Add to Glovelly to recover it.')
  }
  const sourceRequest = (id: string, source: NonNullable<IntakeUploadState['submitted']>): RequestInit => {
    if (source.file) {
      const data = new FormData(); data.append('file', source.file); data.append('intakeId', id)
      return { method: 'POST', body: data }
    }
    return jsonRequestInit('POST', { intakeId: id, sourceType: source.mode, value: source.value })
  }
  const submit = async (file?: File) => {
    if (state.phase !== 'upload') return
    const current = state
    await run(async version => {
      if (current.mode === 'file' && !file) throw new Error('Choose a photo or file first.')
      if (current.mode === 'url' && !/^https?:\/\//i.test(current.fields.url.trim())) throw new Error('Enter an absolute http or https URL.')
      if (current.mode === 'text' && !current.fields.text.trim()) throw new Error('Paste some text first.')
      // Submit always means a newly selected source. Retry below preserves the
      // frozen payload/identity instead of silently reusing an ID for new bytes.
      const id = crypto.randomUUID()
      const source = { file, mode: current.mode, value: current.mode === 'url' ? current.fields.url.trim() : current.fields.text }
      sessionStorage.setItem(recoveryKey, id)
      setState(value => value.phase === 'upload' ? { ...value, id, submitted: source } : value)
      try {
        const response = await request(current.mode === 'file' ? '/intake/current/file' : '/intake/current', sourceRequest(id, source), version)
        if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to submit this source.'))
        await receive(await response.json() as Intake, version)
      } catch (error) {
        if (!await recoverSaved(id, version)) throw error
      }
    }, file?.name)
  }
  const retry = async () => {
    if (state.phase !== 'upload') return
    const id = state.id
    const submitted = state.submitted
    await run(async version => {
      if (await recoverSaved(id, version)) return
      let response = await request('/intake/current/retry', jsonRequestInit('POST', { intakeId: id }), version)
      if (response.status === 404 && submitted) response = await request(submitted.mode === 'file' ? '/intake/current/file' : '/intake/current', sourceRequest(id, submitted), version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to retry analysis.'))
      await receive(await response.json() as Intake, version)
    })
  }
  const apply = async () => {
    if (state.phase !== 'upload' || !state.intake) return
    const { fields, intake } = state
    await run(async version => {
      if (!fields.selectedGigId) throw new Error('Choose a gig before attaching this item.')
      if (fields.intent === 'Resource' && !fields.title.trim()) throw new Error('Add a title for this resource.')
      const response = await request('/intake/current/apply', jsonRequestInit('POST', {
        intakeId: intake.id, intent: fields.intent, gigId: fields.selectedGigId,
        resource: fields.intent === 'Resource' ? { resourceType: fields.resourceType, purpose: fields.purpose, title: fields.title.trim(), notes: fields.notes.trim() || null, isPrimary: fields.isPrimary } : null,
      }), version)
      if (!response.ok) {
        if (await recoverSaved(intake.id, version)) return
        throw new Error(await getResponseErrorMessage(response, 'Unable to attach this item.'))
      }
      applied(await response.json() as IntakeApplication, intake.evidence, version)
    })
  }
  const setIntent = async (intent: IntakeFields['intent']) => {
    if (state.phase !== 'upload' || !state.intake || !intent) return
    const id = state.id
    await run(async version => {
      const response = await request('/intake/current/intent', jsonRequestInit('PUT', { intakeId: id, intent }), version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to change the treatment.'))
      const intake = await response.json() as Intake
      setState(current => current.phase === 'upload' && current.id === id ? { ...current, status: 'ready', intake, fields: { ...current.fields, intent } } : current)
    })
  }
  const waitForPreparation = async (id: string, version: number) => {
    for (let attempt = 0; attempt < 180; attempt++) {
      await new Promise(resolve => window.setTimeout(resolve, 1000))
      const response = await request(`/intake/current/gig-drafts/status?intakeId=${encodeURIComponent(id)}`, undefined, version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to recover your import.'))
      const result = await response.json() as { state: string; batchId?: string; error?: string }
      if (result.state === 'Completed' && result.batchId) { setState({ phase: 'prepared', batchId: result.batchId }); return }
      if (result.state === 'Failed' || result.state === 'None') throw new Error(result.error || 'Gig preparation stopped. Retry or prepare a manual draft.')
    }
    throw new Error('Gig preparation is still running. Close and reopen Add to Glovelly to recover it.')
  }
  const prepareGigDrafts = async (manual: boolean) => {
    if (state.phase !== 'upload' || !state.intake) return
    const id = state.id
    await run(async version => {
      try {
        const response = await request(`/intake/current/gig-drafts${manual ? '/manual' : ''}`, jsonRequestInit('POST', { intakeId: id }), version)
        if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to prepare a draft.'))
        const result = await response.json() as { batchId?: string }
        if (result.batchId && version === generation.current) setState({ phase: 'prepared', batchId: result.batchId })
        else if (!manual) await waitForPreparation(id, version)
        else throw new Error('Unable to recover your prepared import.')
      } catch (error) {
        const recovered = await request(`/intake/gig-drafts/${id}`, undefined, version)
        if (!recovered.ok) throw error
        const result = await recovered.json() as { batchId: string }
        if (version === generation.current) setState({ phase: 'prepared', batchId: result.batchId })
      }
    })
  }
  const reviewImport = () => {
    if (state.phase !== 'prepared') return
    const batchId = state.batchId
    close()
    onReviewImport(batchId)
  }
  const discard = async () => {
    await run(async version => {
      const response = await request(`/intake/current?intakeId=${encodeURIComponent(state.phase === 'upload' ? state.id : '')}`, { method: 'DELETE' }, version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to discard this upload.'))
      sessionStorage.removeItem(recoveryKey)
      setState({ phase: 'choosing', loading: false, recovered: null, saved: null, error: '' })
    })
  }
  const loadMore = async () => {
    if (state.phase !== 'upload' || !state.intake?.candidateContinuation || state.candidatesStatus === 'loading') return
    const id = state.id
    const version = generation.current
    setState({ ...state, candidatesStatus: 'loading', candidatesError: '' })
    try {
      const { response, page } = await getQuickCaptureCandidatePage(state.intake.candidateContinuation)
      if (version !== generation.current) return
      if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again to load gigs.')) return
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to load more gigs.'))
      if (!page) throw new Error('Unable to load more gigs.')
      setState(current => current.phase === 'upload' && current.id === id && current.intake ? {
        ...current, candidatesStatus: 'idle', intake: { ...current.intake, candidates: [...current.intake.candidates, ...page.candidates.filter(candidate => !current.intake!.candidates.some(existing => existing.id === candidate.id))], hasMoreCandidates: page.hasMore, candidateContinuation: page.continuation },
      } : current)
    } catch (error) {
      setState(current => current.phase === 'upload' && current.id === id ? { ...current, candidatesStatus: 'idle', candidatesError: error instanceof Error ? error.message : 'Unable to load gigs.' } : current)
    }
  }
  return { state, open, choose, resume, close, back, review, reviewImport, setIntent, prepareGigDrafts, updateField, updateResourceField, submit, retry, apply, discard, loadMore, reset }
}
