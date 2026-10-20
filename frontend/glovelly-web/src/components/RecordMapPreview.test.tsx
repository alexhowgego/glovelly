// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { GoogleMapsLocationLink, RecordMapPreview } from './RecordMapPreview'
import { fetchWithSession } from '../api'

vi.mock('../api', async importOriginal => ({
  ...await importOriginal<typeof import('../api')>(), fetchWithSession: vi.fn(),
}))

afterEach(() => { cleanup(); vi.useRealTimers(); vi.resetAllMocks(); vi.unstubAllGlobals() })

describe('location preview boundaries', () => {
  it('measures only its own frame immediately and coalesces subsequent resizes without changing its frame', async () => {
    let resize!: ResizeObserverCallback
    vi.stubGlobal('ResizeObserver', class {
      constructor(callback: ResizeObserverCallback) { resize = callback }
      observe() {}
      disconnect() {}
    })
    vi.mocked(fetchWithSession).mockImplementation(async input => {
      const size = new URL(String(input), 'https://example.test').searchParams.get('size')!
      return new Response(JSON.stringify(descriptor('v1:test', size)))
    })
    const unavailable = vi.fn()
    const view = render(<RecordMapPreview kind="clients" id="one" source="v1:test" query="Hall" bucket="small" onUnavailable={unavailable} />)
    const frame = view.container.querySelector('.map-preview-frame')
    expect(fetchWithSession).not.toHaveBeenCalled()
    act(() => resize([{ contentRect: { width: 580, height: 400 } } as ResizeObserverEntry], {} as ResizeObserver))
    fireEvent.load(await screen.findByAltText('Unverified map search preview'))
    const link = screen.getByRole('link', { name: 'Open location in Google Maps: Hall' })
    link.focus()
    expect(vi.mocked(fetchWithSession).mock.calls[0][0]).toContain('size=580x400')
    vi.useFakeTimers()
    act(() => resize([{ contentRect: { width: 600, height: 400 } } as ResizeObserverEntry], {} as ResizeObserver))
    act(() => resize([{ contentRect: { width: 620, height: 400 } } as ResizeObserverEntry], {} as ResizeObserver))
    expect(fetchWithSession).toHaveBeenCalledOnce()
    await act(async () => { vi.advanceTimersByTime(300) })
    expect(fetchWithSession).toHaveBeenCalledTimes(2)
    expect(vi.mocked(fetchWithSession).mock.calls[1][0]).toContain('size=620x400')
    expect(view.container.querySelector('.map-preview-frame')).toBe(frame)
    expect(screen.getByRole('link', { name: 'Open location in Google Maps: Hall' })).toBe(link)
    expect(document.activeElement).toBe(link)
    expect(unavailable).not.toHaveBeenCalled()
  })

  const descriptor = (source = 'v1:test', size = 'small') => ({
    sourceFingerprint: source, size,
    url: `https://maps.googleapis.com/maps/api/staticmap?center=Hall&key=test-only&signature=test-signature&size=${size}`,
  })

  it('uses a direct signed Google image with an origin referrer, not image-byte fetching', async () => {
    const image = descriptor()
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(JSON.stringify(image)))
    const unavailable = vi.fn()
    render(<RecordMapPreview kind="clients" id="one" source="v1:test" query="Hall & Café" bucket="small" onUnavailable={unavailable} />)
    const element = await screen.findByAltText('Unverified map search preview') as HTMLImageElement
    expect(element.getAttribute('src')).toBe(image.url)
    expect(element.getAttribute('referrerpolicy')).toBe('origin')
    fireEvent.load(element)
    expect(element.hidden).toBe(false)
    expect(screen.queryByRole('status')).toBeNull()
    expect(fetchWithSession).toHaveBeenCalledOnce()
    expect(unavailable).not.toHaveBeenCalled()
    const mapLink = screen.getByRole('link', { name: 'Open location in Google Maps: Hall & Café' })
    expect(mapLink.getAttribute('href')).toBe('https://www.google.com/maps/search/?api=1&query=Hall%20%26%20Caf%C3%A9')
    expect(mapLink.getAttribute('rel')).toBe('noopener noreferrer')
    const help = screen.getByRole('tooltip')
    expect(mapLink.getAttribute('aria-describedby')).toBe(help.id)
    expect(help.tagName).toBe('SPAN')
    expect(element.closest('figure')?.querySelector('figcaption')).toBeNull()
    fireEvent.click(mapLink)
    expect(fetchWithSession).toHaveBeenCalledOnce()
  })

  it.each([
    { name: 'unexpected provider origin', overrides: { url: 'https://example.com/map?signature=test' } },
    { name: 'obsolete source', overrides: { sourceFingerprint: 'v1:other' } },
    { name: 'wrong size bucket', overrides: { size: 'medium' } },
  ])('rejects $name', async ({ overrides }) => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(JSON.stringify({ ...descriptor(), ...overrides })))
    const unavailable = vi.fn()
    render(<RecordMapPreview kind="clients" id="one" source="v1:test" query="Hall" bucket="small" onUnavailable={unavailable} />)
    await waitFor(() => expect(unavailable).toHaveBeenCalledOnce())
    expect(screen.queryByAltText('Unverified map search preview')).toBeNull()
  })

  it('removes old imagery on source changes and ignores late descriptor completion', async () => {
    let finishOld!: (response: Response) => void
    vi.mocked(fetchWithSession).mockReturnValueOnce(new Promise(resolve => { finishOld = resolve }))
      .mockResolvedValueOnce(new Response(JSON.stringify(descriptor('v1:new'))))
    const unavailable = vi.fn()
    const { rerender } = render(<RecordMapPreview kind="gigs" id="one" source="v1:old" query="Old Hall" bucket="small" onUnavailable={unavailable} />)
    rerender(<RecordMapPreview kind="gigs" id="one" source="v1:new" query="New Hall" bucket="small" onUnavailable={unavailable} />)
    await screen.findByAltText('Unverified map search preview')
    await act(async () => finishOld(new Response(JSON.stringify(descriptor('v1:old')))))
    expect(screen.getAllByAltText('Unverified map search preview')).toHaveLength(1)
    expect(unavailable).not.toHaveBeenCalled()
  })

  it('bounds descriptor/image loading without retries', async () => {
    vi.useFakeTimers()
    vi.mocked(fetchWithSession).mockReturnValueOnce(new Promise(() => {}))
    const unavailable = vi.fn()
    render(<RecordMapPreview kind="clients" id="one" source="v1:test" query="Hall" bucket="small" onUnavailable={unavailable} />)
    await act(async () => { vi.advanceTimersByTime(10000) })
    expect(unavailable).toHaveBeenCalledOnce()
    await act(async () => { vi.advanceTimersByTime(30000) })
    expect(fetchWithSession).toHaveBeenCalledOnce()
  })
  it('renders and activates an external link without an API request or approval mutation', () => {
    render(<GoogleMapsLocationLink query="Public Hall & Café">Public Hall & Café</GoogleMapsLocationLink>)
    const link = screen.getByRole('link', { name: 'Public Hall & Café' })
    expect(link.getAttribute('href')).toBe('https://www.google.com/maps/search/?api=1&query=Public%20Hall%20%26%20Caf%C3%A9')
    expect(link.getAttribute('rel')).toBe('noopener noreferrer')
    expect(fetchWithSession).not.toHaveBeenCalled()
    fireEvent.click(link)
    expect(fetchWithSession).not.toHaveBeenCalled()
  })

  it('handles expiry through the existing callback without an error panel', async () => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(null, { status: 401 }))
    const expired = vi.fn()
    const unavailable = vi.fn()
    render(<RecordMapPreview kind="clients" id="one" source="v1:test" query="Hall" bucket="small" onSessionExpired={expired} onUnavailable={unavailable} />)
    await waitFor(() => expect(expired).toHaveBeenCalledOnce())
    expect(unavailable).not.toHaveBeenCalled()
  })

  it('fails once and aborts its request on unmount', async () => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(null, { status: 204 }))
    const unavailable = vi.fn()
    const { unmount } = render(<RecordMapPreview kind="gigs" id="one" source="v1:test" query="Hall" bucket="small" onUnavailable={unavailable} />)
    await waitFor(() => expect(unavailable).toHaveBeenCalledOnce())
    expect(fetchWithSession).toHaveBeenCalledOnce()
    const signal = vi.mocked(fetchWithSession).mock.calls[0][1]!.signal!
    unmount()
    expect(signal.aborted).toBe(true)
  })

  it('ignores an obsolete completion, including session-expiry callbacks', async () => {
    let complete!: (response: Response) => void
    vi.mocked(fetchWithSession).mockReturnValueOnce(new Promise(resolve => { complete = resolve }))
    const expired = vi.fn()
    const unavailable = vi.fn()
    const { unmount } = render(<RecordMapPreview kind="clients" id="old" source="v1:old" query="Old Hall" bucket="small" onSessionExpired={expired} onUnavailable={unavailable} />)
    unmount()
    complete(new Response(null, { status: 401 }))
    await Promise.resolve()
    expect(expired).not.toHaveBeenCalled()
    expect(unavailable).not.toHaveBeenCalled()
  })
})
