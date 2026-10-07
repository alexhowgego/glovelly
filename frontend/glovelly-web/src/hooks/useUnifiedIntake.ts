import { useCallback, useRef, useState } from 'react'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage, handleSessionExpired, jsonRequestInit } from '../api'
import type { GigExternalResourcePurpose, GigExternalResourceType, Intake, IntakeApplication, IntakeCandidatePage, IntakeEvidence } from '../types'

export type IntakeSourceMode = 'file' | 'url' | 'text'
export type IntakeFields = {
  url: string; text: string; selectedGigId: string; resourceType: GigExternalResourceType
  purpose: GigExternalResourcePurpose; title: string; notes: string; isPrimary: boolean
  intent: 'Receipt' | 'Resource'
}
export type IntakeUploadState = {
  phase: 'upload'; status: 'editing' | 'working' | 'ready' | 'error'
  mode: IntakeSourceMode; id: string; intake: Intake | null; fields: IntakeFields
  sourceName: string | null
  message: string; candidatesStatus: 'idle' | 'loading'; candidatesError: string
}
export type IntakeState =
  | { phase: 'closed' }
  | { phase: 'choosing'; loading: boolean; recovered: Intake | null; saved: IntakeApplication | null; error: string }
  | IntakeUploadState
  | { phase: 'attached' | 'review'; application: IntakeApplication; evidence: IntakeEvidence[] }

const recoveryKey = 'glovelly-current-intake-id'
const emptyFields = (): IntakeFields => ({ url: '', text: '', selectedGigId: '', resourceType: 'File', purpose: 'Other', title: '', notes: '', isPrimary: false, intent: 'Receipt' })
const modeFor = (intake: Intake): IntakeSourceMode => intake.sourceType === 'File' ? 'file' : intake.sourceType === 'Url' ? 'url' : 'text'

function upload(mode: IntakeSourceMode, intake: Intake | null = null): IntakeUploadState {
  return {
    phase: 'upload', status: intake ? 'ready' : 'editing', mode, id: intake?.id ?? crypto.randomUUID(), intake,
    sourceName: intake?.sourceName ?? null,
    fields: {
      ...emptyFields(), url: intake?.sourceUrl ?? '', text: intake?.sourceText ?? '',
      selectedGigId: intake?.candidates.find(candidate => candidate.isSelected)?.id ?? intake?.candidates[0]?.id ?? '',
      resourceType: intake?.suggestedResourceType ?? (mode === 'url' ? 'Url' : 'File'),
      title: intake?.sourceName ?? '', intent: intake?.proposedIntent === 'Resource' || mode === 'url' ? 'Resource' : 'Receipt',
    },
    message: intake?.applicationError ?? intake?.failureMessage ?? '', candidatesStatus: 'idle', candidatesError: '',
  }
}

type Options = { onApplied: (application: IntakeApplication) => void; onSessionExpired: (message: string) => void }

export function useUnifiedIntake({ onApplied, onSessionExpired }: Options) {
  const [state, setState] = useState<IntakeState>({ phase: 'closed' })
  const generation = useRef(0)
  const commandPending = useRef(false)
  const trigger = useRef<HTMLElement | null>(null)
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
      const id = sessionStorage.getItem(recoveryKey)
      if (id) {
        const response = await request(`/intake/applications/${id}`, undefined, version)
        if (response.ok) saved = await response.json() as IntakeApplication
        else if (response.status !== 404) throw new Error(await getResponseErrorMessage(response, 'Unable to recover your attachment.'))
      }
      const response = await request('/intake/current', undefined, version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to recover your upload.'))
      const recovered = response.status === 204 ? null : await response.json() as Intake
      if (version === generation.current) setState({ phase: 'choosing', loading: false, recovered, saved, error: '' })
    } catch (error) {
      if (version === generation.current) setState({ phase: 'choosing', loading: false, recovered: null, saved: null, error: error instanceof Error ? error.message : 'Unable to recover your upload.' })
    }
  }

  const choose = (mode: IntakeSourceMode) => setState(upload(mode))
  const resume = () => {
    if (state.phase !== 'choosing') return
    if (state.saved) applied(state.saved)
    else if (state.recovered) setState(upload(modeFor(state.recovered), state.recovered))
  }
  const close = () => {
    if (commandPending.current) return
    if (state.phase === 'attached' || state.phase === 'review') sessionStorage.removeItem(recoveryKey)
    ++generation.current
    setState({ phase: 'closed' })
    trigger.current?.focus()
  }
  const back = () => { if (!commandPending.current) void open() }
  const review = () => { if (state.phase === 'attached') setState({ ...state, phase: 'review' }) }
  const updateField = <K extends keyof IntakeFields>(key: K, value: IntakeFields[K]) => {
    setState(current => current.phase === 'upload' ? { ...current, fields: { ...current.fields, [key]: value } } : current)
  }

  const receive = (value: Intake, version: number) => {
    if (version !== generation.current) return
    if (value.application) applied(value.application, value.evidence ?? [], version)
    else setState(upload(modeFor(value), value))
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
    if (!response.ok) return false
    applied(await response.json() as IntakeApplication, [], version)
    return true
  }
  const submit = async (file?: File) => {
    if (state.phase !== 'upload') return
    const current = state
    await run(async version => {
      if (current.mode === 'file' && !file) throw new Error('Choose a photo or file first.')
      if (current.mode === 'url' && !/^https?:\/\//i.test(current.fields.url.trim())) throw new Error('Enter an absolute http or https URL.')
      if (current.mode === 'text' && !current.fields.text.trim()) throw new Error('Paste some text first.')
      sessionStorage.setItem(recoveryKey, current.id)
      let init: RequestInit
      if (file) {
        const data = new FormData()
        data.append('file', file)
        data.append('intakeId', current.id)
        init = { method: 'POST', body: data }
      } else init = jsonRequestInit('POST', { intakeId: current.id, sourceType: current.mode, value: current.mode === 'url' ? current.fields.url.trim() : current.fields.text.trim() })
      try {
        const response = await request(current.mode === 'file' ? '/intake/current/file' : '/intake/current', init, version)
        if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to submit this source.'))
        receive(await response.json() as Intake, version)
      } catch (error) {
        if (!await recoverSaved(current.id, version)) throw error
      }
    }, file?.name)
  }
  const retry = async () => {
    if (state.phase !== 'upload') return
    const id = state.id
    await run(async version => {
      if (await recoverSaved(id, version)) return
      const response = await request('/intake/current/retry', { method: 'POST' }, version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to retry analysis.'))
      receive(await response.json() as Intake, version)
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
  const discard = async () => {
    await run(async version => {
      const response = await request('/intake/current', { method: 'DELETE' }, version)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to discard this upload.'))
      sessionStorage.removeItem(recoveryKey)
      setState({ phase: 'choosing', loading: false, recovered: null, saved: null, error: '' })
    })
  }
  const loadMore = async () => {
    if (state.phase !== 'upload' || !state.intake?.candidateContinuation || state.candidatesStatus === 'loading') return
    const id = state.id
    setState({ ...state, candidatesStatus: 'loading', candidatesError: '' })
    try {
      const response = await request(`/intake/current/candidates?continuation=${encodeURIComponent(state.intake.candidateContinuation)}`)
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to load more gigs.'))
      const page = await response.json() as IntakeCandidatePage
      setState(current => current.phase === 'upload' && current.id === id && current.intake ? {
        ...current, candidatesStatus: 'idle', intake: { ...current.intake, candidates: [...current.intake.candidates, ...page.candidates.filter(candidate => !current.intake!.candidates.some(existing => existing.id === candidate.id))], hasMoreCandidates: page.hasMore, candidateContinuation: page.continuation },
      } : current)
    } catch (error) {
      setState(current => current.phase === 'upload' && current.id === id ? { ...current, candidatesStatus: 'idle', candidatesError: error instanceof Error ? error.message : 'Unable to load gigs.' } : current)
    }
  }
  return { state, open, choose, resume, close, back, review, updateField, submit, retry, apply, discard, loadMore, reset }
}
