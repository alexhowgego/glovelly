// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest'
import { fetchWithSession } from './api'

afterEach(() => { vi.unstubAllGlobals() })

it('requests a permission refresh after a forbidden response without recursing on session refresh', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 403 })))
  const listener = vi.fn()
  window.addEventListener('glovelly:permissions-changed', listener)
  try {
    await fetchWithSession('/admin/users')
    expect(listener).toHaveBeenCalledOnce()
    await fetchWithSession('/auth/me')
    expect(listener).toHaveBeenCalledOnce()
  } finally {
    window.removeEventListener('glovelly:permissions-changed', listener)
  }
})

it('refreshes commercial access without treating a plan restriction as lost administrative permissions', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ code: 'invoice_allowance_exhausted' }), {
    status: 403, headers: { 'content-type': 'application/problem+json' },
  })))
  const permissions = vi.fn()
  const entitlements = vi.fn()
  window.addEventListener('glovelly:permissions-changed', permissions)
  window.addEventListener('glovelly:entitlements-changed', entitlements)
  try {
    const response = await fetchWithSession('/invoices/id/status')
    expect(entitlements).toHaveBeenCalledOnce()
    expect(permissions).not.toHaveBeenCalled()
    expect((await response.json()).code).toBe('invoice_allowance_exhausted')
  } finally {
    window.removeEventListener('glovelly:permissions-changed', permissions)
    window.removeEventListener('glovelly:entitlements-changed', entitlements)
  }
})
