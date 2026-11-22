import { useCallback, useEffect, useRef, useState } from 'react'
import type { Invoice } from '../types'

export type InvoiceActionDecision = { completeLinkedGigs: boolean }
export type InvoiceActionRequest = { invoice: Invoice; linkedGigCount: number; action: 'issue' | 'publish' }

export function useInvoiceActionConfirmation(enabled = true) {
  const [request, setRequest] = useState<InvoiceActionRequest | null>(null)
  const pending = useRef<((decision: InvoiceActionDecision | null) => void) | null>(null)

  const confirm = useCallback((next: InvoiceActionRequest) => {
    if (pending.current) return Promise.resolve(null)
    return new Promise<InvoiceActionDecision | null>(resolve => {
      pending.current = resolve
      setRequest(next)
    })
  }, [])

  const finish = useCallback((decision: InvoiceActionDecision | null) => {
    const resolve = pending.current
    pending.current = null
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

  return { request, confirm, finish }
}
