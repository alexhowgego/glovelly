// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getDocument } from 'pdfjs-dist'
import { MAX_THUMBNAIL_PIXELS, renderInvoiceThumbnail, thumbnailDimensions } from './renderInvoiceThumbnail'

vi.mock('pdfjs-dist', () => ({ GlobalWorkerOptions: {}, getDocument: vi.fn() }))
afterEach(() => vi.resetAllMocks())

describe('local first-page rendering', () => {
  it('preserves proportions, caps DPR/pixels and rejects malformed page sizes', () => {
    const dimensions = thumbnailDimensions(600, 800, 300, 400, 4)
    expect(dimensions).toEqual({ width: 300, height: 400, pixelWidth: 600, pixelHeight: 800 })
    const large = thumbnailDimensions(600, 800, 10000, 10000, 4)
    expect(large.pixelWidth * large.pixelHeight).toBeLessThanOrEqual(MAX_THUMBNAIL_PIXELS)
    expect(() => thumbnailDimensions(0, 800, 300, 400, 1)).toThrow()
  })

  function documentWith(renderTask: { promise: Promise<void>, cancel: () => void }) {
    const render = vi.fn().mockReturnValue(renderTask)
    const getPage = vi.fn().mockResolvedValue({ getViewport: ({ scale }: { scale: number }) => ({ width: 600 * scale, height: 800 * scale }), render })
    const destroy = vi.fn().mockResolvedValue(undefined)
    vi.mocked(getDocument).mockReturnValue({ promise: Promise.resolve({ getPage }), destroy } as unknown as ReturnType<typeof getDocument>)
    return { getPage, render, destroy }
  }

  it('renders only page one from owned bytes and destroys its document after rendering', async () => {
    const mocks = documentWith({ promise: Promise.resolve(), cancel: vi.fn() })
    const bytes = new Uint8Array([1, 2, 3])
    const canvas = document.createElement('canvas')
    await renderInvoiceThumbnail(bytes, canvas, 280, 380, new AbortController().signal)
    expect(mocks.getPage).toHaveBeenCalledWith(1)
    expect(mocks.render).toHaveBeenCalledWith(expect.objectContaining({ canvas, background: 'rgb(255, 255, 255)' }))
    expect(getDocument).toHaveBeenCalledWith(expect.objectContaining({ data: bytes }))
    expect(mocks.destroy).toHaveBeenCalledOnce()
    expect(canvas.width * canvas.height).toBeLessThanOrEqual(MAX_THUMBNAIL_PIXELS)
  })

  it('cancels obsolete rendering and destroys the loading/document task exactly once', async () => {
    let reject!: (error: Error) => void
    const promise = new Promise<void>((_, fail) => { reject = fail })
    const cancel = vi.fn(() => reject(new Error('cancelled')))
    const mocks = documentWith({ promise, cancel })
    const controller = new AbortController()
    const pending = renderInvoiceThumbnail(new Uint8Array([1]), document.createElement('canvas'), 280, 380, controller.signal)
    await vi.waitFor(() => expect(mocks.render).toHaveBeenCalledOnce())
    controller.abort()
    await expect(pending).rejects.toThrow('cancelled')
    expect(cancel).toHaveBeenCalledOnce()
    expect(mocks.destroy).toHaveBeenCalledOnce()
  })
})
