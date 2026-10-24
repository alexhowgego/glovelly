// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { InvoiceThumbnail } from './InvoiceThumbnail'
import { loadInvoiceThumbnailBytes } from '../invoiceThumbnail'
import { renderInvoiceThumbnail } from '../renderInvoiceThumbnail'
import type { Invoice } from '../types'

vi.mock('../invoiceThumbnail', async importOriginal => ({
  ...await importOriginal<typeof import('../invoiceThumbnail')>(), loadInvoiceThumbnailBytes: vi.fn(),
}))
vi.mock('../renderInvoiceThumbnail', () => ({ renderInvoiceThumbnail: vi.fn() }))
afterEach(() => { cleanup(); vi.useRealTimers(); vi.resetAllMocks() })

const invoice = (overrides: Partial<Invoice> = {}) => ({ id: 'one', invoiceNumber: 'INV-1', documentState: 'Current',
  documentRevision: 2, pdfDocumentRevision: 2, pdfGeneratedAt: '2026-10-19T09:00:00Z', pdfSizeBytes: 3, ...overrides }) as Invoice

describe('invoice thumbnail lifecycle', () => {
  function ready() {
    vi.mocked(loadInvoiceThumbnailBytes).mockResolvedValue(new Uint8Array([1, 2, 3]))
    vi.mocked(renderInvoiceThumbnail).mockImplementation(async (_, canvas) => {
      canvas.width = 300
      canvas.height = 400
      return { width: 180, height: 240 }
    })
  }

  it('opens the existing preview without sharing bytes or refetching on ordinary renders', async () => {
    ready()
    const selected = invoice()
    const preview = vi.fn().mockResolvedValue(undefined)
    const expired = vi.fn()
    const view = render(<InvoiceThumbnail invoice={selected} maxHeight={250} onPreview={preview} onSessionExpired={expired} />)
    const button = await screen.findByRole('button', { name: 'Open PDF preview for invoice INV-1' })
    expect(button.getAttribute('style')).toContain('var(--record-visual-max-height')
    const tooltip = screen.getByRole('tooltip')
    expect(button.getAttribute('aria-describedby')).toBe(tooltip.id)
    expect(tooltip.tagName).toBe('SPAN')
    expect(tooltip.closest('.invoice-thumbnail-preview')?.querySelector('p')).toBeNull()
    fireEvent.click(button)
    expect(preview).toHaveBeenCalledWith(selected)
    view.rerender(<InvoiceThumbnail invoice={{ ...selected, description: 'Unrelated change' }} maxHeight={250} onPreview={preview} onSessionExpired={expired} />)
    expect(loadInvoiceThumbnailBytes).toHaveBeenCalledOnce()
    expect(renderInvoiceThumbnail).toHaveBeenCalledOnce()
    const canvas = screen.getByTestId('invoice-thumbnail-canvas') as HTMLCanvasElement
    const signal = vi.mocked(loadInvoiceThumbnailBytes).mock.calls[0][1]
    view.unmount()
    expect(signal.aborted).toBe(true)
    expect(canvas.width).toBe(0)
    expect(canvas.height).toBe(0)
    render(<InvoiceThumbnail invoice={selected} maxHeight={250} onPreview={preview} onSessionExpired={expired} />)
    await waitFor(() => expect(loadInvoiceThumbnailBytes).toHaveBeenCalledTimes(2))
  })

  it('discards a pending download after selection changes and renders only the new selection', async () => {
    ready()
    let finish!: (bytes: Uint8Array<ArrayBuffer>) => void
    vi.mocked(loadInvoiceThumbnailBytes).mockReturnValueOnce(new Promise(resolve => { finish = resolve }))
    const preview = vi.fn().mockResolvedValue(undefined)
    const view = render(<InvoiceThumbnail invoice={invoice()} maxHeight={250} onPreview={preview} />)
    const oldSignal = vi.mocked(loadInvoiceThumbnailBytes).mock.calls[0][1]
    view.rerender(<InvoiceThumbnail invoice={invoice({ id: 'two', invoiceNumber: 'INV-2' })} maxHeight={250} onPreview={preview} />)
    await screen.findByRole('button', { name: 'Open PDF preview for invoice INV-2' })
    await act(async () => finish(new Uint8Array([4])))
    expect(oldSignal.aborted).toBe(true)
    expect(renderInvoiceThumbnail).toHaveBeenCalledOnce()
    expect(screen.queryByRole('button', { name: /INV-1/ })).toBeNull()
  })

  it('clears a ready canvas immediately on revision/readiness changes', async () => {
    ready()
    const preview = vi.fn().mockResolvedValue(undefined)
    const view = render(<InvoiceThumbnail invoice={invoice()} maxHeight={250} onPreview={preview} />)
    await screen.findByRole('button')
    const oldCanvas = screen.getByTestId('invoice-thumbnail-canvas') as HTMLCanvasElement
    view.rerender(<InvoiceThumbnail invoice={invoice({ documentState: 'Regenerating', documentRevision: 3 })} maxHeight={250} onPreview={preview} />)
    expect(screen.queryByRole('button')).toBeNull()
    expect(oldCanvas.width).toBe(0)
    expect(loadInvoiceThumbnailBytes).toHaveBeenCalledOnce()
    expect(screen.getByRole('status').textContent).toContain('regenerating')
  })

  it('shows a quiet render failure without retrying or invoking preview/generation', async () => {
    ready()
    vi.mocked(renderInvoiceThumbnail).mockRejectedValueOnce(new Error('Unable to render this PDF.'))
    const preview = vi.fn()
    render(<InvoiceThumbnail invoice={invoice()} maxHeight={250} onPreview={preview} />)
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Unable to render'))
    expect(loadInvoiceThumbnailBytes).toHaveBeenCalledOnce()
    expect(preview).not.toHaveBeenCalled()
  })

  it('times out and aborts a stuck operation', async () => {
    vi.useFakeTimers()
    vi.mocked(loadInvoiceThumbnailBytes).mockReturnValueOnce(new Promise(() => {}))
    render(<InvoiceThumbnail invoice={invoice()} maxHeight={250} onPreview={vi.fn()} />)
    await act(async () => { vi.advanceTimersByTime(20000) })
    expect(screen.getByRole('status').textContent).toContain('timed out')
    expect(vi.mocked(loadInvoiceThumbnailBytes).mock.calls[0][1].aborted).toBe(true)
  })
})
