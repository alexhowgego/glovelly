// @vitest-environment jsdom
import { act, cleanup, renderHook, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { useEntitlements } from './useEntitlements'

afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.useRealTimers() })

const free = {
  effectivePlan: { code: 'free', displayName: 'Free' }, assignmentStatus: 'Default', endsAtUtc: null,
  invoiceIssuance: { resetsAtUtc: '2100-01-01T00:00:00Z' },
}

it('loads only while authenticated and refreshes on plan changes and workspace entry', async () => {
  const fetch = vi.fn(async () => new Response(JSON.stringify(free)))
  vi.stubGlobal('fetch', fetch)
  const { result, rerender } = renderHook(({ enabled, workspace }) => useEntitlements(enabled, workspace),
    { initialProps: { enabled: false, workspace: 'gigs' } })
  expect(fetch).not.toHaveBeenCalled()
  rerender({ enabled: true, workspace: 'gigs' })
  await waitFor(() => expect(result.current.snapshot?.effectivePlan.code).toBe('free'))
  act(() => { window.dispatchEvent(new Event('glovelly:entitlements-changed')) })
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2))
  rerender({ enabled: true, workspace: 'invoices' })
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(3))
  rerender({ enabled: false, workspace: 'invoices' })
  expect(result.current.snapshot).toBeNull()
})

it('does not replace the latest snapshot with an older in-flight response', async () => {
  let completeFirst!: (value: Response) => void
  vi.stubGlobal('fetch', vi.fn()
    .mockImplementationOnce(() => new Promise<Response>(resolve => { completeFirst = resolve }))
    .mockResolvedValueOnce(new Response(JSON.stringify(free))))
  const { result } = renderHook(() => useEntitlements(true, 'invoices'))
  act(() => { window.dispatchEvent(new Event('glovelly:entitlements-changed')) })
  await waitFor(() => expect(result.current.snapshot?.effectivePlan.code).toBe('free'))
  await act(async () => {
    completeFirst(new Response(JSON.stringify({ ...free, effectivePlan: { code: 'pro', displayName: 'Pro' } })))
  })
  expect(result.current.snapshot?.effectivePlan.code).toBe('free')
})

it('refreshes an open session at grant expiry and at the next allowance reset', async () => {
  vi.useFakeTimers()
  vi.setSystemTime(new Date('2030-01-31T23:59:58Z'))
  const paid = {
    ...free, effectivePlan: { code: 'pro', displayName: 'Pro' }, assignmentStatus: 'Active',
    endsAtUtc: '2030-01-31T23:59:59Z', invoiceIssuance: { resetsAtUtc: '2030-02-01T00:00:00Z' },
  }
  const expired = { ...free, assignmentStatus: 'Expired', invoiceIssuance: { resetsAtUtc: '2030-02-01T00:00:00Z' } }
  const fetch = vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify(paid)))
    .mockResolvedValueOnce(new Response(JSON.stringify(expired)))
    .mockResolvedValueOnce(new Response(JSON.stringify(free)))
  vi.stubGlobal('fetch', fetch)
  const { result } = renderHook(() => useEntitlements(true, 'invoices'))
  await act(async () => { await Promise.resolve() })
  expect(result.current.snapshot?.effectivePlan.code).toBe('pro')
  await act(async () => { await vi.advanceTimersByTimeAsync(1000) })
  expect(result.current.snapshot?.assignmentStatus).toBe('Expired')
  await act(async () => { await vi.advanceTimersByTimeAsync(1000) })
  expect(fetch).toHaveBeenCalledTimes(3)
})
