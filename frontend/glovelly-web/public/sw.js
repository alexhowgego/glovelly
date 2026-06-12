const CACHE_NAME = 'glovelly-shell-v1'
const APP_SHELL = ['/', '/manifest.webmanifest', '/gordon-192.png', '/gordon-512.png']
const API_PREFIXES = [
  '/.well-known',
  '/access',
  '/admin',
  '/app',
  '/auth',
  '/clients',
  '/expense-statements',
  '/gigs',
  '/gig-imports',
  '/health',
  '/integrations',
  '/invoices',
  '/invoice-lines',
  '/invoice-email-template',
  '/mcp',
  '/oauth',
  '/seller-profile',
  '/test-auth',
  '/workspace-events',
]

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(APP_SHELL))
  )
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys
          .filter((key) => key !== CACHE_NAME)
          .map((key) => caches.delete(key))
      )
    )
  )
  self.clients.claim()
})

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') {
    return
  }

  const requestUrl = new URL(event.request.url)
  if (API_PREFIXES.some((prefix) => requestUrl.pathname.startsWith(prefix))) {
    return
  }

  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request).catch(() => caches.match('/'))
    )
    return
  }

  event.respondWith(
    caches.match(event.request).then((cachedResponse) => {
      if (cachedResponse) {
        return cachedResponse
      }

      return fetch(event.request).then((networkResponse) => {
        const responseClone = networkResponse.clone()

        caches.open(CACHE_NAME).then((cache) => {
          cache.put(event.request, responseClone)
        })

        return networkResponse
      })
    })
  )
})
