const CACHE_NAME = 'glovelly-shell-v1'
const APP_SHELL = ['/', '/manifest.webmanifest', '/gordon-192.png', '/gordon-512.png']

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
  if (
    requestUrl.pathname.startsWith('/auth') ||
    requestUrl.pathname.startsWith('/admin') ||
    requestUrl.pathname.startsWith('/clients') ||
    requestUrl.pathname.startsWith('/gigs') ||
    requestUrl.pathname.startsWith('/invoices') ||
    requestUrl.pathname.startsWith('/invoice-lines') ||
    requestUrl.pathname.startsWith('/seller-profile')
  ) {
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
