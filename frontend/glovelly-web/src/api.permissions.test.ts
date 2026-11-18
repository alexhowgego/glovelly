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
