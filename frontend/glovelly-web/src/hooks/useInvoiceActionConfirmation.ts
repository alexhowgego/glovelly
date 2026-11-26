import { useCallback, useEffect, useRef, useState } from 'react'
import type { Invoice } from '../types'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage } from '../api'

export type InvoiceActionDecision = { completeLinkedGigs: boolean }
export type InvoiceActionRequest = { invoice: Invoice; linkedGigCount: number; action: 'issue' | 'publish' }

export function useInvoiceActionConfirmation(enabled = true) {
  const [request, setRequest] = useState<InvoiceActionRequest | null>(null)
  const pending = useRef<((decision: InvoiceActionDecision | null) => void) | null>(null)
  const operation = useRef<((decision: InvoiceActionDecision, invoice: Invoice) => Promise<void>) | null>(null)
  const onRefresh = useRef<((invoice: Invoice) => void) | null>(null)
  const currentRequest = useRef<InvoiceActionRequest | null>(null)
  const inFlight = useRef(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const confirm = useCallback((next: InvoiceActionRequest,
    execute?: (decision: InvoiceActionDecision, invoice: Invoice) => Promise<void>,
    onInvoiceRefreshed?: (invoice: Invoice) => void) => {
    if (pending.current) return Promise.resolve(null)
    return new Promise<InvoiceActionDecision | null>(resolve => {
      pending.current = resolve
      operation.current = execute ?? null
      onRefresh.current = onInvoiceRefreshed ?? null
      currentRequest.current = next
      setError('')
      setRequest(next)
    })
  }, [])

  const finish = useCallback(async (decision: InvoiceActionDecision | null) => {
    if (decision && inFlight.current) return
    const resolve = pending.current
    const next = currentRequest.current
    if (decision && operation.current && next) {
      inFlight.current = true
      setBusy(true)
      setError('')
      const refreshInvoice = async () => {
        const response = await fetchWithSession(buildApiUrl(`/invoices/${next.invoice.id}`))
        if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to refresh the invoice.'))
        const invoice = await response.json() as Invoice
        if (pending.current === resolve) {
          currentRequest.current = { ...next, invoice }
          setRequest({ ...next, invoice })
          onRefresh.current?.(invoice)
        }
        return invoice
      }
      try {
        const invoice = await refreshInvoice()
        if (pending.current !== resolve) return
        await operation.current(decision, invoice)
      } catch (failure) {
        if (pending.current === resolve) {
          setError(failure instanceof Error ? failure.message : 'Unable to complete the invoice action.')
          // A delivery failure may follow successful issuance. Retain that
          // authoritative status and ensure retry does not issue again.
          try { await refreshInvoice() } catch { /* Retain the actionable original failure. */ }
        }
        return
      } finally {
        inFlight.current = false
        setBusy(false)
      }
      if (pending.current !== resolve) return
    }
    pending.current = null
    operation.current = null
    onRefresh.current = null
    currentRequest.current = null
    setRequest(null)
    resolve?.(decision)
  }, [])

  useEffect(() => {
    if (!enabled) finish(null)
  }, [enabled, finish])

  useEffect(() => () => {
    pending.current?.(null)
    pending.current = null
  }, [])

  return { request, confirm, finish, busy, error }
}
