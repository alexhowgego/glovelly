import workerSource from '../public/sw.js?raw'
import { describe, expect, it, vi } from 'vitest'

describe('app-shell service worker privacy boundaries', () => {
  function worker(requests: { url: string }[] = []) {
    const listeners = new Map<string, (event: unknown) => void>()
    const remove = vi.fn().mockResolvedValue(true)
    const cache = { keys: vi.fn().mockResolvedValue(requests), delete: remove }
    // Evaluate only the checked-in worker source against controlled browser doubles.
    const installWorker = new Function('self', 'caches', 'URL', workerSource)
    installWorker(
      { location: { origin: 'https://menu.glovelly.net' }, addEventListener: (name: string, callback: (event: unknown) => void) => listeners.set(name, callback), clients: { claim: vi.fn() } },
      { open: vi.fn().mockResolvedValue(cache), keys: vi.fn().mockResolvedValue(['glovelly-shell-v1']) },
      URL,
    )
    return { listeners, remove }
  }

  it('never handles external Google images or authenticated PDF/metadata reads', () => {
    const { listeners } = worker()
    for (const url of ['https://maps.googleapis.com/maps/api/staticmap?signature=fixture',
      'https://menu.glovelly.net/invoices/one/pdf', 'https://menu.glovelly.net/clients/one/map-preview']) {
      const respondWith = vi.fn()
      listeners.get('fetch')!({ request: { url, method: 'GET' }, respondWith })
      expect(respondWith).not.toHaveBeenCalled()
    }
  })

  it('cleans only old Static Maps cache entries, preserving the app shell', async () => {
    const map = { url: 'https://maps.googleapis.com/maps/api/staticmap?signature=fixture' }
    const { listeners, remove } = worker([map, { url: 'https://menu.glovelly.net/' }, { url: 'https://menu.glovelly.net/assets/app.js' }])
    const pending: Promise<unknown>[] = []
    listeners.get('activate')!({ waitUntil: (promise: Promise<unknown>) => pending.push(promise) })
    await Promise.all(pending)
    expect(remove).toHaveBeenCalledExactlyOnceWith(map)
  })
})
