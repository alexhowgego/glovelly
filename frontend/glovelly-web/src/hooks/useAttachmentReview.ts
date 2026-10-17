import { useEffect, useRef, useState } from 'react'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage, handleSessionExpired, jsonRequestInit } from '../api'
import type { Gig, GigExternalResourceForm, IntakeApplication, Invoice, QuickGigCandidate, ReceiptAnalysisResult } from '../types'
import { notifications } from '../notifications'
import { getQuickCaptureCandidatePage } from '../quickCaptureCandidates'
import { invoiceSaveMessage } from '../receiptSaveFeedback'
import type { ReceiptSuggestions } from '../gigEditing'
import { normalizeExpenseCategory } from '../gigEditing'

type ReviewFields = GigExternalResourceForm & { selectedGigId: string }

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
  const [fields, setFields] = useState<ReviewFields>({
    selectedGigId: application.gig.id, title: application.resource?.title ?? '', url: application.resource?.url ?? '',
    notes: application.resource?.notes ?? '', resourceType: application.resource?.resourceType ?? 'File',
    purpose: application.resource?.purpose ?? 'Other', isPrimary: application.resource?.isPrimary ?? false,
  })
  const gig = application.gig
  const today = new Date(); today.setHours(0, 0, 0, 0)
  const distance = Math.round(Math.abs(new Date(`${gig.date}T00:00:00`).getTime() - today.getTime()) / 86400000)
  const [candidates, setCandidates] = useState<QuickGigCandidate[]>(application.candidates ?? [{ id: gig.id, clientId: gig.clientId, title: gig.title, date: gig.date, venue: gig.venue, type: gig.type, status: gig.status, daysFromToday: distance, isSelected: true }])
  const [page, setPage] = useState<{ hasMore: boolean; continuation: string | null }>({ hasMore: application.hasMoreCandidates ?? true, continuation: application.candidateContinuation ?? null })
  const [operation, setOperation] = useState<'idle' | 'saving' | 'deleting' | 'analysing'>('idle')
  const [analysis, setAnalysis] = useState<ReceiptAnalysisResult | null>(application.analysis ?? null)
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
  const updateField = <K extends keyof ReviewFields>(key: K, value: ReviewFields[K]) => setFields(current => ({ ...current, [key]: value }))
  const updateResourceField = <K extends keyof GigExternalResourceForm>(key: K, value: GigExternalResourceForm[K]) => setFields(current => ({ ...current, [key]: value }))
  const loadMore = async () => {
    if (!page.hasMore || candidateState === 'loading') return
    setCandidateState('loading')
    setCandidateError('')
    try {
      const { response, page: next } = await getQuickCaptureCandidatePage(page.continuation ?? undefined)
      if (!mounted.current) return
      if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again to load gigs.')) return
      if (!response.ok || !next) throw new Error(await getResponseErrorMessage(response, 'Unable to load gigs.'))
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
      if (saved.kind === 'Resource' && !fields.title.trim()) throw new Error('Add a title before saving attachment details.')
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
      setStatus(saved.kind === 'Receipt' ? invoiceSaveMessage(result.invoices ?? []) : 'Attachment updated.')
    } catch (error) { if (mounted.current) setStatus(error instanceof Error ? error.message : 'Unable to save changes.') }
    finally { pending.current = false; if (mounted.current) setOperation('idle') }
  }
  const remove = async () => {
    if (pending.current) return
    if (!window.confirm(saved.kind === 'Receipt' ? 'Delete this receipt expense and all its attached files? This cannot be undone.' : `Delete ${saved.resource?.title || 'this resource'} and all its attached files? This cannot be undone.`)) return
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
  const analyse = async () => {
    if (pending.current || saved.kind !== 'Receipt') return
    pending.current = true; setOperation('analysing'); setStatus('Analysing receipt...')
    try {
      const response = await fetchWithSession(buildApiUrl(`/gigs/${saved.gig.id}/expenses/${saved.expenseId}/attachments/${saved.attachmentId}/analysis`), { method: 'POST' })
      if (!mounted.current) return
      if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again to analyse receipts.')) return
      if (!response.ok && response.status !== 429) throw new Error(await getResponseErrorMessage(response, 'Unable to analyse receipt.'))
      const value = await response.json() as ReceiptAnalysisResult
      setAnalysis(value); setStatus(value.status === 'Succeeded' ? 'Suggestions are ready to review.' : value.failureMessage ?? 'Unable to analyse receipt.')
    } catch (error) { if (mounted.current) setStatus(error instanceof Error ? error.message : 'Unable to analyse receipt.') }
    finally { pending.current = false; if (mounted.current) setOperation('idle') }
  }
  const useSuggestions = (values: ReceiptSuggestions) => {
    if (values.merchant) setDescription(values.merchant)
    if (values.totalAmount !== null) setAmount(String(values.totalAmount))
    if (values.suggestedCategory) setCategory(normalizeExpenseCategory(values.suggestedCategory))
    setStatus('Suggestions copied into the editable fields. Save changes to apply them.')
  }
  return { saved, fields, description, amount, category, candidates, page, operation, status, candidateState, candidateError, analysis, updateField, updateResourceField, setDescription, setAmount, setCategory, loadMore, save, remove, analyse, useSuggestions }
}
