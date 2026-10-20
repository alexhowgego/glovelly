// @vitest-environment jsdom
import { act, cleanup, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { RecordVisualOverview } from './RecordVisualOverview'
import { RecordMapPreview } from './RecordMapPreview'
import { fetchWithSession } from '../api'

vi.mock('../api', async importOriginal => ({ ...await importOriginal<typeof import('../api')>(), fetchWithSession: vi.fn() }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.resetAllMocks(); vi.unstubAllGlobals() })

describe('automatic preview eligibility', () => {
  function setup() {
    let resized!: ResizeObserverCallback
    let intersected!: IntersectionObserverCallback
    vi.spyOn(document, 'hidden', 'get').mockReturnValue(false)
    vi.stubGlobal('ResizeObserver', class {
      callback: ResizeObserverCallback
      constructor(callback: ResizeObserverCallback) { this.callback = callback }
      observe(node: Element) {
        if (node.classList.contains('map-preview-frame')) {
          this.callback([{ contentRect: { width: 400, height: 492 } } as ResizeObserverEntry], {} as ResizeObserver)
        } else resized = this.callback
      }
      disconnect() {}
    })
    vi.stubGlobal('IntersectionObserver', class {
      constructor(callback: IntersectionObserverCallback) { intersected = callback }
      observe() {}
      disconnect() {}
    })
    const unavailable = vi.fn()
    let reportFailure: (() => void) | undefined
    const view = render(<RecordVisualOverview visual={(bucket, failed) => {
      reportFailure ??= () => { unavailable(); failed() }
      return <RecordMapPreview kind="clients" id="one" source="v1:test" query="Hall" bucket={bucket} onUnavailable={reportFailure} />
    }}>
      <p>Ordinary facts</p>
    </RecordVisualOverview>)
    return { view, unavailable,
      resize: (width: number, height: number) => act(() => resized([{ contentRect: { width, height } } as ResizeObserverEntry], {} as ResizeObserver)),
      visible: (isIntersecting: boolean) => act(() => intersected([{ isIntersecting } as IntersectionObserverEntry], {} as IntersectionObserver)),
    }
  }

  it('does not fetch while hidden, narrow or short, and automatically loads once eligible', async () => {
    vi.mocked(fetchWithSession).mockReturnValue(new Promise(() => {}))
    const pane = setup()
    pane.resize(900, 500)
    expect(fetchWithSession).not.toHaveBeenCalled()
    pane.resize(500, 500)
    pane.visible(true)
    expect(fetchWithSession).not.toHaveBeenCalled()
    pane.resize(900, 200)
    expect(fetchWithSession).not.toHaveBeenCalled()
    pane.resize(900, 500)
    await waitFor(() => expect(fetchWithSession).toHaveBeenCalledOnce())
    const signal = vi.mocked(fetchWithSession).mock.calls[0][1]!.signal!
    pane.resize(500, 500)
    expect(signal.aborted).toBe(true)
    expect(screen.getByText('Ordinary facts')).toBeDefined()
  })

  it('removes a failed rail and does not retry because of resizing', async () => {
    vi.mocked(fetchWithSession).mockResolvedValue(new Response(null, { status: 204 }))
    const pane = setup()
    pane.resize(900, 500)
    pane.visible(true)
    await waitFor(() => expect(pane.unavailable).toHaveBeenCalledOnce())
    pane.resize(950, 550)
    expect(fetchWithSession).toHaveBeenCalledOnce()
    expect(screen.queryByLabelText('Supplementary preview')).toBeNull()
  })

  it('reserves the rail and mounts its loading content immediately when eligible', () => {
    vi.mocked(fetchWithSession).mockReturnValue(new Promise(() => {}))
    const pane = setup()
    pane.resize(900, 500)
    pane.visible(true)
    expect(screen.getByLabelText('Supplementary preview')).toBeDefined()
    expect(screen.getByRole('status').textContent).toBe('Loading map preview…')
    expect(fetchWithSession).toHaveBeenCalledOnce()
  })

  it('updates the height budget within a bucket without issuing another descriptor', async () => {
    vi.mocked(fetchWithSession).mockReturnValue(new Promise(() => {}))
    const pane = setup()
    pane.resize(900, 500)
    pane.visible(true)
    await waitFor(() => expect(fetchWithSession).toHaveBeenCalledOnce())
    expect(vi.mocked(fetchWithSession).mock.calls[0][0]).toContain('size=400x492')
    const layout = pane.view.container.querySelector('.record-visual-layout') as HTMLElement
    expect(layout.style.getPropertyValue('--record-visual-max-height')).toBe('492px')
    pane.resize(900, 540)
    expect(layout.style.getPropertyValue('--record-visual-max-height')).toBe('532px')
    expect(fetchWithSession).toHaveBeenCalledOnce()
  })
})
