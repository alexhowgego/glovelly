import { buildApiUrl, fetchWithSession } from './api'
import type { QuickCaptureCandidatePage } from './types'

export async function getQuickCaptureCandidatePage(continuation?: string) {
  const query = continuation ? `?continuation=${encodeURIComponent(continuation)}` : ''
  const response = await fetchWithSession(buildApiUrl(`/gigs/quick-capture-candidates${query}`))
  return { response, page: response.ok ? ((await response.json()) as QuickCaptureCandidatePage) : null }
}
