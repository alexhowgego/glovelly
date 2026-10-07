import { useEffect, useRef, useState } from 'react'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage, handleSessionExpired, jsonRequestInit } from '../api'
import type { Gig, IntakeApplication, Invoice, QuickCaptureCandidatePage, QuickGigCandidate } from '../types'
import type { IntakeFields } from './useUnifiedIntake'
import { notifications } from '../notifications'

type Options = {
  application: IntakeApplication; onMergeGig: (gig: Gig) => void; onMergeInvoices: (invoices: Invoice[]) => void
  onDeleted: () => void; onSessionExpired: (message: string) => void
}

export function useAttachmentReview({ application, onMergeGig, onMergeInvoices, onDeleted, onSessionExpired }: Options) {
  const mounted = useRef(true)
  const pending = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const [saved, setSaved] = useState(application)
  const expense = application.gig.expenses.find(value => value.id === application.expenseId)
  const [description, setDescription] = useState(expense?.description ?? '')
  const [amount, setAmount] = useState(String(expense?.amount ?? 0))
  const [category, setCategory] = useState(expense?.category ?? '')
  const [fields, setFields] = useState<IntakeFields>({
    selectedGigId: application.gig.id, title: application.resource?.title ?? '', url: application.resource?.url ?? '', text: '',
    notes: application.resource?.notes ?? '', resourceType: application.resource?.resourceType ?? 'File',
    purpose: application.resource?.purpose ?? 'Other', isPrimary: application.resource?.isPrimary ?? false, intent: application.kind,
  })
  const gig = application.gig
  const [candidates, setCandidates] = useState<QuickGigCandidate[]>([{ id: gig.id, clientId: gig.clientId, title: gig.title, date: gig.date, venue: gig.venue, type: gig.type, status: gig.status, daysFromToday: 0, isSelected: true }])
  const [page, setPage] = useState<{ hasMore: boolean; continuation: string | null }>({ hasMore: true, continuation: null })
  const [operation, setOperation] = useState<'idle' | 'saving' | 'deleting'>('idle')
  const [status, setStatus] = useState('')
  const [candidateState, setCandidateState] = useState<'idle' | 'loading'>('idle')
  const [candidateError, setCandidateError] = useState('')

  const request = async (path: string, init?: RequestInit) => {
    const response = await fetchWithSession(buildApiUrl(path), init)
    if (!mounted.current) throw new Error('Attachment review has closed.')
    if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again to review attachments.')) throw new Error('Sign in again to continue.')
    if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to update this attachment.'))
    return response
  }
  const updateField = <K extends keyof IntakeFields>(key: K, value: IntakeFields[K]) => setFields(current => ({ ...current, [key]: value }))
  const loadMore = async () => {
    if (!page.hasMore || candidateState === 'loading') return
    setCandidateState('loading')
    setCandidateError('')
    try {
      const query = page.continuation ? `?continuation=${encodeURIComponent(page.continuation)}` : ''
      const response = await request(`/gigs/quick-capture-candidates${query}`)
      const next = await response.json() as QuickCaptureCandidatePage
      if (!mounted.current) return
      setCandidates(current => [...current, ...next.candidates.filter(candidate => !current.some(existing => existing.id === candidate.id))])
      setPage(next)
    } catch (error) { if (mounted.current) setCandidateError(error instanceof Error ? error.message : 'Unable to load gigs.') }
    finally { if (mounted.current) setCandidateState('idle') }
  }
  const save = async () => {
    if (pending.current) return
    pending.current = true
    setOperation('saving')
    setStatus('')
    try {
      const value = Number(amount)
      if (saved.kind === 'Receipt' && (!description.trim() || !amount.trim() || !Number.isFinite(value) || value < 0)) throw new Error('Enter a description and a valid non-negative amount.')
      const endpoint = saved.kind === 'Receipt' ? `/gigs/receipt-drafts/${saved.expenseId}` : `/gigs/external-resource-drafts/${saved.resource!.id}`
      const body = saved.kind === 'Receipt'
        ? { gigId: fields.selectedGigId, description: description.trim(), amount: value, category: category || null }
        : { gigId: fields.selectedGigId, title: fields.title.trim(), url: fields.url.trim() || null, notes: fields.notes.trim() || null, resourceType: fields.resourceType, purpose: fields.purpose, isPrimary: fields.isPrimary }
      const response = await request(endpoint, jsonRequestInit('PATCH', body))
      const result = await response.json() as { gig: Gig; previousGig: Gig | null; invoices?: Invoice[] }
      if (!mounted.current) return
      onMergeGig(result.gig)
      if (result.previousGig) onMergeGig(result.previousGig)
      if (result.invoices) onMergeInvoices(result.invoices)
      setSaved({ ...saved, gig: result.gig, resource: result.gig.externalResources.find(resource => resource.id === saved.resource?.id) ?? null })
      setStatus('Attachment updated.')
    } catch (error) { if (mounted.current) setStatus(error instanceof Error ? error.message : 'Unable to save changes.') }
    finally { pending.current = false; if (mounted.current) setOperation('idle') }
  }
  const remove = async () => {
    if (pending.current) return
    pending.current = true
    setOperation('deleting')
    setStatus('')
    try {
      const endpoint = saved.kind === 'Receipt'
        ? `/gigs/receipt-drafts/${saved.expenseId}`
        : `/gigs/${saved.gig.id}/external-resources/${saved.resource!.id}`
      const response = await request(endpoint, { method: 'DELETE' })
      const result = await response.json() as Gig | { gig: Gig; invoices: Invoice[] }
      if (!mounted.current) return
      if ('gig' in result) { onMergeGig(result.gig); onMergeInvoices(result.invoices) }
      else onMergeGig(result)
      notifications.success('Attachment deleted.')
      onDeleted()
    } catch (error) { if (mounted.current) setStatus(error instanceof Error ? error.message : 'Unable to delete attachment.') }
    finally { pending.current = false; if (mounted.current) setOperation('idle') }
  }
  return { saved, fields, description, amount, category, candidates, page, operation, status, candidateState, candidateError, updateField, setDescription, setAmount, setCategory, loadMore, save, remove }
}
