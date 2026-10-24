import { GlobalWorkerOptions, getDocument } from 'pdfjs-dist'
import type { RenderTask } from 'pdfjs-dist'
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'

GlobalWorkerOptions.workerSrc = workerUrl
export const MAX_THUMBNAIL_PIXELS = 8_000_000

export function thumbnailDimensions(width: number, height: number, maxWidth: number, maxHeight: number, dpr: number) {
  if (![width, height, maxWidth, maxHeight].every(value => Number.isFinite(value) && value > 0)) throw new Error('Unsupported PDF page size.')
  const scale = Math.min(maxWidth / width, maxHeight / height)
  const cssWidth = width * scale
  const cssHeight = height * scale
  const density = Math.min(Math.max(Number.isFinite(dpr) ? dpr : 1, 1), 2,
    Math.sqrt(MAX_THUMBNAIL_PIXELS / (cssWidth * cssHeight)))
  return { width: cssWidth, height: cssHeight,
    pixelWidth: Math.max(1, Math.floor(cssWidth * density)),
    pixelHeight: Math.max(1, Math.floor(cssHeight * density)) }
}

export async function renderInvoiceThumbnail(bytes: Uint8Array<ArrayBuffer>, canvas: HTMLCanvasElement,
  maxWidth: number, maxHeight: number, signal: AbortSignal) {
  signal.throwIfAborted()
  const loading = getDocument({ data: bytes, maxImageSize: MAX_THUMBNAIL_PIXELS, enableXfa: false })
  let rendering: RenderTask | undefined
  let destroyed: Promise<void> | undefined
  const destroy = () => destroyed ??= loading.destroy()
  const abort = () => {
    rendering?.cancel()
    void destroy().catch(() => {})
  }
  signal.addEventListener('abort', abort, { once: true })
  try {
    const document = await loading.promise
    signal.throwIfAborted()
    const page = await document.getPage(1)
    signal.throwIfAborted()
    const viewport = page.getViewport({ scale: 1 })
    const dimensions = thumbnailDimensions(viewport.width, viewport.height, maxWidth, maxHeight, window.devicePixelRatio)
    canvas.width = dimensions.pixelWidth
    canvas.height = dimensions.pixelHeight
    rendering = page.render({ canvas, viewport: page.getViewport({ scale: dimensions.pixelWidth / viewport.width }), background: 'rgb(255, 255, 255)' })
    await rendering.promise
    signal.throwIfAborted()
    return { width: dimensions.width, height: dimensions.height }
  } finally {
    signal.removeEventListener('abort', abort)
    await destroy()
  }
}
