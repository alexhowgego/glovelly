import { useEffect, useId, useRef, useState } from 'react'
import { getInvoiceDocumentAvailability } from '../invoiceDocumentState'
import { invoiceThumbnailIdentity, loadInvoiceThumbnailBytes } from '../invoiceThumbnail'
import type { Invoice } from '../types'

type InvoiceThumbnailProps = {
  invoice: Invoice
  maxHeight: number
  updating?: boolean
  onPreview: (invoice: Invoice) => Promise<void>
  onSessionExpired?: (message: string) => void
}

export function InvoiceThumbnail(props: InvoiceThumbnailProps) {
  return <Thumbnail key={props.invoice.id} {...props} />
}

function Thumbnail({ invoice, maxHeight, updating = false, onPreview, onSessionExpired }: InvoiceThumbnailProps) {
  const tooltipId = useId()
  const statusId = useId()
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const identity = invoiceThumbnailIdentity(invoice)
  const [state, setState] = useState<{
    identity: string; width?: number; height?: number; hasImage: boolean
    message: string; phase: 'loading' | 'ready' | 'error'
  }>({ identity, message: 'Loading invoice thumbnail…', hasImage: false, phase: 'loading' })
  const invoiceRef = useRef(invoice)
  useEffect(() => { invoiceRef.current = invoice })
  // Only the mounted selection's displayed pixels survive a refresh, never PDF bytes.
  useEffect(() => {
    const canvas = canvasRef.current
    return () => { if (canvas) { canvas.width = 0; canvas.height = 0 } }
  }, [])
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const selected = invoiceRef.current
    const availability = getInvoiceDocumentAvailability(selected)
    const clearImage = () => { canvas.width = 0; canvas.height = 0 }
    const fail = (message: string) => {
      clearImage()
      setState(previous => ({ ...previous, identity, hasImage: false, message, phase: 'error' }))
    }
    const pending = updating || selected.documentState === 'Regenerating'
    if ((!availability.isCurrent || selected.gigLinesNeedRefresh) && !pending) {
      fail(`${selected.gigLinesNeedRefresh ? 'Invoice lines need refreshing' : availability.message}. Use the existing invoice PDF actions.`)
      return
    }
    setState(previous => ({ ...previous, identity, phase: 'loading',
      message: previous.hasImage || pending ? 'Updating preview…' : 'Loading invoice thumbnail…' }))
    if (pending) return
    const controller = new AbortController()
    const staging = document.createElement('canvas')
    const timer = setTimeout(() => {
      controller.abort()
      staging.width = 0
      staging.height = 0
      fail('Thumbnail loading timed out. Use Preview instead.')
    }, 20000)
    void (async () => {
      try {
        const bytes = await loadInvoiceThumbnailBytes(selected, controller.signal, onSessionExpired ?? (() => {}))
        if (controller.signal.aborted) return
        const renderer = await import('../renderInvoiceThumbnail')
        if (controller.signal.aborted) return
        const size = await renderer.renderInvoiceThumbnail(bytes, staging,
          Math.min(Math.max(canvas.closest('.record-visual-rail')?.clientWidth || 600, 600), 800), maxHeight, controller.signal)
        if (controller.signal.aborted) return
        const context = canvas.getContext('2d')
        if (!context) throw new Error('Thumbnail canvas is unavailable. Use Preview instead.')
        canvas.width = staging.width
        canvas.height = staging.height
        context.drawImage(staging, 0, 0)
        setState({ identity, width: size.width, height: size.height, hasImage: true, message: '', phase: 'ready' })
      } catch (error) {
        if (!controller.signal.aborted) {
          fail(error instanceof Error ? error.message : 'Thumbnail unavailable. Use Preview instead.')
        }
      } finally {
        clearTimeout(timer)
        staging.width = 0
        staging.height = 0
      }
    })()
    return () => {
      controller.abort()
      clearTimeout(timer)
      staging.width = 0
      staging.height = 0
    }
  }, [identity, maxHeight, updating, onSessionExpired])

  const availability = getInvoiceDocumentAvailability(invoice)
  const canRender = availability.isCurrent && !invoice.gigLinesNeedRefresh
  const ready = state.phase === 'ready' && state.identity === identity && canRender && !updating
  const pending = updating || state.identity !== identity || state.phase === 'loading'
  const ratio = state.width && state.height ? state.width / state.height : 595 / 842
  return <div className="invoice-thumbnail-preview">
    <div className={`invoice-thumbnail-frame${!ready && state.hasImage && pending ? ' is-updating' : ''}`}
      style={{ width: `min(100%, calc(var(--record-visual-max-height, 1200px) * ${ratio}))`, aspectRatio: String(ratio) }}
      aria-busy={pending} data-testid="invoice-thumbnail-frame">
      <button type="button" className="invoice-thumbnail" aria-label={`Open PDF preview for invoice ${invoice.invoiceNumber}`}
        aria-describedby={ready ? tooltipId : statusId} disabled={!ready}
        onClick={() => { if (ready) void onPreview(invoice) }}>
        <canvas ref={canvasRef} hidden={!state.hasImage} aria-hidden="true" data-testid="invoice-thumbnail-canvas" />
      </button>
      {!ready && <p className="invoice-thumbnail-status" id={statusId} role="status">{pending ? 'Updating preview…' : state.message}</p>}
    </div>
    {ready && <span className="visual-preview-tooltip" id={tooltipId} role="tooltip">Page 1 · Open invoice PDF preview</span>}
  </div>
}
