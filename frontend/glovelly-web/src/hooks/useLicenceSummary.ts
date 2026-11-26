import { useEffect, useState } from 'react'
import { buildApiUrl, fetchWithSession, handleSessionExpired } from '../api'
import type { SessionExpiredHandler } from '../api'
import type { LicenceSummaryState } from '../dashboardCards'
import type { LicenceSummary } from '../types'

export function useLicenceSummary(enabled: boolean, onSessionExpired: SessionExpiredHandler): LicenceSummaryState {
  const [state, setState] = useState<LicenceSummaryState>({ status: 'loading' })

  useEffect(() => {
    if (!enabled) return
    const controller = new AbortController()
    let generation = 0
    const load = async () => {
      const current = ++generation
      setState({ status: 'loading' })
      try {
        const response = await fetchWithSession(buildApiUrl('/admin/licences/summary'), { signal: controller.signal })
        if (controller.signal.aborted || generation !== current) return
        if (handleSessionExpired(response, onSessionExpired, 'Sign in again to view licences.')) return
        if (!response.ok) throw new Error('Unable to load the licence summary.')
        const summary = await response.json() as LicenceSummary
        if (!controller.signal.aborted && generation === current) setState({ status: 'ready', summary })
      } catch {
        if (!controller.signal.aborted && generation === current) setState({ status: 'error' })
      }
    }
    const refresh = () => { void load() }
    refresh()
    window.addEventListener('glovelly:entitlements-changed', refresh)
    window.addEventListener('focus', refresh)
    return () => {
      controller.abort()
      window.removeEventListener('glovelly:entitlements-changed', refresh)
      window.removeEventListener('focus', refresh)
    }
  }, [enabled, onSessionExpired])

  return enabled ? state : { status: 'loading' }
}
