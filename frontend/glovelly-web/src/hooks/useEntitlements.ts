import { useCallback, useEffect, useRef, useState } from 'react'
import { buildApiUrl, fetchWithSession, getResponseErrorMessage, handleSessionExpired } from '../api'
import type { SessionExpiredHandler } from '../api'
import type { EntitlementSnapshot } from '../types'

export function useEntitlements(enabled: boolean, workspace: string, onSessionExpired?: SessionExpiredHandler) {
  const [snapshot, setSnapshot] = useState<EntitlementSnapshot | null>(null)
  const [error, setError] = useState('')
  const generation = useRef(0)
  const clear = useCallback(() => { generation.current++; setSnapshot(null); setError('') }, [])
  const refresh = useCallback(async (signal?: AbortSignal) => {
    const current = ++generation.current
    try {
      const response = await fetchWithSession(buildApiUrl('/entitlements/me'), { signal })
      if (signal?.aborted || current !== generation.current) return
      if (onSessionExpired && handleSessionExpired(response, onSessionExpired, 'Sign in again to check your plan.')) return
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to load your plan.'))
      const next = await response.json() as EntitlementSnapshot
      if (!signal?.aborted && current === generation.current) { setSnapshot(next); setError('') }
    } catch (failure) {
      if (!signal?.aborted && current === generation.current) {
        setSnapshot(null)
        setError(failure instanceof Error ? failure.message : 'Unable to load your plan.')
      }
    }
  }, [onSessionExpired])
  useEffect(() => {
    if (!enabled) { clear(); return }
    const controller = new AbortController()
    const reload = () => { void refresh(controller.signal) }
    reload()
    window.addEventListener('glovelly:entitlements-changed', reload)
    window.addEventListener('focus', reload)
    return () => {
      controller.abort()
      window.removeEventListener('glovelly:entitlements-changed', reload)
      window.removeEventListener('focus', reload)
    }
  }, [enabled, workspace, refresh, clear])
  useEffect(() => {
    if (!enabled || !snapshot) return
    const boundaries = [new Date(snapshot.invoiceIssuance.resetsAtUtc).getTime()]
    if (snapshot.assignmentStatus === 'Active' && snapshot.endsAtUtc) boundaries.push(new Date(snapshot.endsAtUtc).getTime())
    const next = Math.min(...boundaries.filter(value => value > Date.now()))
    if (!Number.isFinite(next)) return
    const delay = next - Date.now()
    const timer = window.setTimeout(() => void refresh(), Math.min(delay, 2147483647))
    return () => window.clearTimeout(timer)
  }, [enabled, snapshot, refresh])
  return { snapshot: enabled ? snapshot : null, error: enabled ? error : '', refresh }
}
