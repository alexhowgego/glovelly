import { useEffect, useId, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { buildApiUrl, fetchWithSession, handleSessionExpired } from '../api'
import type { MapSizeBucket } from '../hooks/useVisualPaneEligibility'
import { googleMapsSearchUrl } from '../recordLocationSource'
import type { StaticMapDescriptor } from '../types'
import { mapFrameSize } from '../mapFrameSize'

export function GoogleMapsLocationLink({ query, children }: { query: string, children: ReactNode }) {
  const href = googleMapsSearchUrl(query)
  return href ? <a href={href} target="_blank" rel="noopener noreferrer" referrerPolicy="no-referrer"
    title="Open this location in Google Maps (opens a new tab)">{children}</a> : <>{children}</>
}

type RecordMapPreviewProps = {
  kind: 'clients' | 'gigs'
  id: string
  source: string
  query: string
  bucket: MapSizeBucket
  onUnavailable: () => void
  onSessionExpired?: (message: string) => void
}

export function RecordMapPreview(props: RecordMapPreviewProps) {
  return <MapFrame key={`${props.kind}:${props.id}:${props.source}:${props.bucket}`} {...props} />
}

function MapFrame(props: RecordMapPreviewProps) {
  const frame = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState<string | null>(null)
  const canMeasure = typeof ResizeObserver !== 'undefined'
  useEffect(() => {
    if (!frame.current || !canMeasure) return
    let current: string | null = null
    let timer: ReturnType<typeof setTimeout> | undefined
    const observer = new ResizeObserver(entries => {
      const rect = entries[0]?.contentRect
      const next = rect ? mapFrameSize(rect.width, rect.height) : null
      if (timer) clearTimeout(timer)
      if (!next || next === current) return
      if (current === null) {
        current = next
        setSize(next)
      } else {
        timer = setTimeout(() => { current = next; setSize(next) }, 300)
      }
    })
    observer.observe(frame.current)
    return () => { observer.disconnect(); if (timer) clearTimeout(timer) }
  }, [canMeasure])
  const requestedSize = size ?? (!canMeasure ? props.bucket : null)
  return <div className="map-preview-frame" ref={frame}>
    {requestedSize ? <MapImage {...props} size={requestedSize} /> : <p role="status">Loading map preview…</p>}
  </div>
}

function MapImage({ kind, id, source, query, size, onUnavailable, onSessionExpired }: RecordMapPreviewProps & { size: string }) {
  const tooltipId = useId()
  const mapsUrl = googleMapsSearchUrl(query)
  const [image, setImage] = useState<{ url: string, key: string } | null>(null)
  const url = image?.url
  const [loaded, setLoaded] = useState(false)
  const alive = useRef(false)
  const watchdog = useRef<ReturnType<typeof setTimeout> | null>(null)
  const key = `${kind}:${id}:${source}:${size}`
  useEffect(() => {
    const controller = new AbortController()
    alive.current = true
    watchdog.current = setTimeout(() => {
      if (!controller.signal.aborted) {
        controller.abort()
        alive.current = false
        onUnavailable()
      }
    }, 10000)
    void (async () => {
      try {
        const response = await fetchWithSession(buildApiUrl(`/${kind}/${id}/map-preview?source=${encodeURIComponent(source)}&size=${size}`), { signal: controller.signal })
        if (controller.signal.aborted) return
        if (handleSessionExpired(response, onSessionExpired ?? (() => {}), 'Your session expired. Sign in again.')) {
          controller.abort()
          alive.current = false
          if (watchdog.current) clearTimeout(watchdog.current)
          return
        }
        if (!response.ok || response.status === 204) throw new Error('unavailable')
        const descriptor = await response.json() as StaticMapDescriptor
        if (controller.signal.aborted) return
        const imageUrl = new URL(descriptor.url)
        if (descriptor.sourceFingerprint !== source || descriptor.size !== size || descriptor.url.length > 16384 ||
          imageUrl.origin !== 'https://maps.googleapis.com' || imageUrl.pathname !== '/maps/api/staticmap' ||
          imageUrl.username || imageUrl.password || imageUrl.hash || !imageUrl.searchParams.get('signature')) throw new Error('unavailable')
        setImage({ url: descriptor.url, key })
      } catch {
        if (!controller.signal.aborted) {
          controller.abort()
          alive.current = false
          if (watchdog.current) clearTimeout(watchdog.current)
          onUnavailable()
        }
      }
    })()
    return () => {
      controller.abort()
      alive.current = false
      if (watchdog.current) clearTimeout(watchdog.current)
    }
  }, [kind, id, source, size, key, onUnavailable, onSessionExpired])
  return <figure className="map-search-preview">
    {!loaded && <p role="status">Loading map preview…</p>}
    {url && mapsUrl && <a className="map-preview-link" href={mapsUrl} target="_blank" rel="noopener noreferrer"
      aria-label={`Open location in Google Maps: ${query}`} aria-describedby={tooltipId} hidden={!loaded}>
      <img src={url} alt="Unverified map search preview" referrerPolicy="origin" hidden={!loaded}
      onLoad={() => {
        if (!alive.current || image?.key !== key) return
        if (watchdog.current) clearTimeout(watchdog.current)
        setLoaded(true)
      }}
      onError={() => {
        if (!alive.current || image?.key !== key) return
        alive.current = false
        if (watchdog.current) clearTimeout(watchdog.current)
        onUnavailable()
      }} /></a>}
    {loaded && <span className="visual-preview-tooltip" id={tooltipId} role="tooltip">Unverified map search preview · Open in Google Maps (new tab)</span>}
  </figure>
}
