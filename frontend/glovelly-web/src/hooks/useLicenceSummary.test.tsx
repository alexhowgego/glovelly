// @vitest-environment jsdom
import { act, cleanup, renderHook, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { useLicenceSummary } from './useLicenceSummary'

afterEach(() => { cleanup(); vi.unstubAllGlobals() })

const summary = { freeUsers: 12, paidUsers: 7, expiringGrants: 2 }
const response = () => new Response(JSON.stringify(summary), { headers: { 'content-type': 'application/json' } })

it('loads only for an enabled licence workspace and refreshes after changes without using the user-management directory', async () => {
  const fetch = vi.fn<(url: RequestInfo | URL) => Promise<Response>>(async () => response())
  const expired = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const { result, rerender } = renderHook(({ enabled }) => useLicenceSummary(enabled, expired),
    { initialProps: { enabled: false } })
  expect(fetch).not.toHaveBeenCalled()
  rerender({ enabled: true })
  await waitFor(() => expect(result.current).toEqual({ status: 'ready', summary }))
  expect(fetch.mock.calls[0][0]).toBe('/admin/licences/summary')
  act(() => { window.dispatchEvent(new Event('glovelly:entitlements-changed')) })
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2))
  expect(fetch.mock.calls.every(([url]) => url === '/admin/licences/summary')).toBe(true)
  rerender({ enabled: false })
  expect(result.current.status).toBe('loading')
})

it('reports a real loading/error state instead of inventing zero counts', async () => {
  const expired = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 503 })))
  const { result } = renderHook(() => useLicenceSummary(true, expired))
  expect(result.current.status).toBe('loading')
  await waitFor(() => expect(result.current.status).toBe('error'))
})

it('ignores late results after leaving the workspace', async () => {
  let resolve!: (value: Response) => void
  const expired = vi.fn()
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(finish => { resolve = finish })))
  const { result, rerender } = renderHook(({ enabled }) => useLicenceSummary(enabled, expired),
    { initialProps: { enabled: true } })
  rerender({ enabled: false })
  await act(async () => { resolve(response()) })
  expect(result.current.status).toBe('loading')
})
