import { useEffect, useRef, useState } from 'react'
import type { PDFDocumentProxy } from 'pdfjs-dist'

export function InvoicePdfPreview({ url, invoiceNumber }: { url: string; invoiceNumber: string }) {
  const [document, setDocument] = useState<PDFDocumentProxy | null>(null)
  const [pageNumber, setPageNumber] = useState(1)
  const [message, setMessage] = useState('Preparing invoice preview…')
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    let cancelled = false
    let destroy: (() => Promise<void>) | undefined
    void (async () => {
      try {
        const pdfjs = await import('pdfjs-dist')
        const worker = await import('pdfjs-dist/build/pdf.worker.min.mjs?url')
        if (cancelled) return
        pdfjs.GlobalWorkerOptions.workerSrc = worker.default
        const loading = pdfjs.getDocument({ url, enableXfa: false, maxImageSize: 8_000_000 })
        destroy = () => loading.destroy()
        const pdf = await loading.promise
        if (!cancelled) setDocument(pdf)
      } catch {
        if (!cancelled) setMessage('Unable to display the PDF here. Download the PDF to review it.')
      }
    })()
    return () => { cancelled = true; void destroy?.().catch(() => {}) }
  }, [url])

  useEffect(() => {
    const canvas = canvasRef.current
    if (!document || !canvas) return
    let cancelled = false
    let renderTask: ReturnType<Awaited<ReturnType<PDFDocumentProxy['getPage']>>['render']> | undefined
    setMessage('Rendering page…')
    void (async () => {
      try {
        const page = await document.getPage(pageNumber)
        if (cancelled) return
        const viewport = page.getViewport({ scale: 1 })
        if (!Number.isFinite(viewport.width) || !Number.isFinite(viewport.height) || viewport.width <= 0 || viewport.height <= 0) throw new Error('Invalid page size')
        const scale = Math.min(2, 1400 / viewport.width, Math.sqrt(8_000_000 / (viewport.width * viewport.height)))
        const scaled = page.getViewport({ scale })
        canvas.width = Math.max(1, Math.floor(scaled.width))
        canvas.height = Math.max(1, Math.floor(scaled.height))
        renderTask = page.render({ canvas, viewport: scaled, background: 'rgb(255, 255, 255)' })
        await renderTask.promise
        if (!cancelled) setMessage('')
      } catch {
        if (!cancelled) setMessage('Unable to display this page. Download the PDF to review it.')
      }
    })()
    return () => { cancelled = true; renderTask?.cancel(); canvas.width = 0; canvas.height = 0 }
  }, [document, pageNumber])

  return <div className="invoice-pdf-preview" data-testid="invoice-preview-frame">
    {message && <p role="status">{message}</p>}
    <canvas key={pageNumber} ref={canvasRef} hidden={!!message} role="img" aria-label={`Invoice ${invoiceNumber}, page ${pageNumber}`} />
    {document && document.numPages > 1 && <div className="invoice-pdf-page-controls">
      <button type="button" className="ghost-button" disabled={pageNumber === 1} onClick={() => setPageNumber(page => page - 1)}>Previous page</button>
      <span>Page {pageNumber} of {document.numPages}</span>
      <button type="button" className="ghost-button" disabled={pageNumber === document.numPages} onClick={() => setPageNumber(page => page + 1)}>Next page</button>
    </div>}
  </div>
}
