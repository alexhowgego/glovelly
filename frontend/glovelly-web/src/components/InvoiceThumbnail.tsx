import { useEffect, useId, useRef, useState } from 'react'
import { getInvoiceDocumentAvailability } from '../invoiceDocumentState'
import { invoiceThumbnailIdentity, loadInvoiceThumbnailBytes } from '../invoiceThumbnail'
import type { Invoice } from '../types'

type InvoiceThumbnailProps = {
  invoice: Invoice
  maxHeight: number
  onPreview: (invoice: Invoice) => Promise<void>
  onSessionExpired?: (message: string) => void
}

export function InvoiceThumbnail(props: InvoiceThumbnailProps) {
  return <Thumbnail key={`${invoiceThumbnailIdentity(props.invoice)}:${props.maxHeight}`} {...props} />
}

function Thumbnail({ invoice, maxHeight, onPreview, onSessionExpired }: InvoiceThumbnailProps) {
  const tooltipId = useId()
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [state, setState] = useState<{ width?: number, height?: number, message: string, ready: boolean }>({ message: 'Loading invoice thumbnail…', ready: false })
  const identity = invoiceThumbnailIdentity(invoice)
  // Metadata identity, not object reference, controls work. New list objects must not refetch.
  const invoiceRef = useRef(invoice)
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const selected = invoiceRef.current
    if (!getInvoiceDocumentAvailability(selected).isCurrent || selected.gigLinesNeedRefresh) return
    const controller = new AbortController()
    const timer = setTimeout(() => {
      controller.abort()
      canvas.width = 0
      canvas.height = 0
      setState({ message: 'Thumbnail loading timed out. Use Preview instead.', ready: false })
    }, 20000)
    void (async () => {
      try {
        const bytes = await loadInvoiceThumbnailBytes(selected, controller.signal, onSessionExpired ?? (() => {}))
        if (controller.signal.aborted) return
        const renderer = await import('../renderInvoiceThumbnail')
        if (controller.signal.aborted) return
        const size = await renderer.renderInvoiceThumbnail(bytes, canvas,
          Math.min(Math.max(canvas.closest('.record-visual-rail')?.clientWidth || 600, 600), 800), maxHeight, controller.signal)
        if (!controller.signal.aborted) setState({ width: size.width, height: size.height, message: '', ready: true })
      } catch (error) {
        if (!controller.signal.aborted) {
          canvas.width = 0
          canvas.height = 0
          setState({ message: error instanceof Error ? error.message : 'Thumbnail unavailable. Use Preview instead.', ready: false })
        }
      } finally {
        clearTimeout(timer)
      }
    })()
    return () => {
      controller.abort()
      clearTimeout(timer)
      canvas.width = 0
      canvas.height = 0
    }
  }, [identity, maxHeight, onSessionExpired])

  const availability = getInvoiceDocumentAvailability(invoice)
  const canRender = availability.isCurrent && !invoice.gigLinesNeedRefresh
  return <div className="invoice-thumbnail-preview">
    <button type="button" className="invoice-thumbnail" aria-label={`Open PDF preview for invoice ${invoice.invoiceNumber}`}
      aria-describedby={state.ready ? tooltipId : undefined}
      disabled={!state.ready || !canRender} hidden={!state.ready || !canRender}
      style={{ width: state.width && state.height
        ? `min(100%, calc(var(--record-visual-max-height, 1200px) * ${state.width / state.height}))` : undefined }}
      onClick={() => void onPreview(invoice)}>
      <canvas ref={canvasRef} aria-hidden="true" data-testid="invoice-thumbnail-canvas" />
    </button>
    {!state.ready && <p role="status">{canRender ? state.message : `${availability.message}. Use the existing invoice PDF actions.`}</p>}
    {state.ready && <span className="visual-preview-tooltip" id={tooltipId} role="tooltip">Page 1 · Open invoice PDF preview</span>}
  </div>
}
