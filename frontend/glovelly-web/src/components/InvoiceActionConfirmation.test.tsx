// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, renderHook, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { InvoiceActionConfirmation } from './InvoiceActionConfirmation'
import { useInvoiceActionConfirmation } from '../hooks/useInvoiceActionConfirmation'
import type { InvoiceActionRequest } from '../hooks/useInvoiceActionConfirmation'
import type { EntitlementSnapshot, Invoice } from '../types'

const request: InvoiceActionRequest = {
  invoice: { id: 'invoice', invoiceNumber: 'GLV-001', status: 'Draft' } as Invoice,
  linkedGigCount: 1, action: 'issue',
}
beforeEach(() => {
  Object.defineProperties(HTMLDialogElement.prototype, {
    showModal: { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } },
    close: { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } },
  })
})
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal')
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'close')
})

describe('single invoice issuance confirmation', () => {
  it('explains issuance and returns the optional gig choice only on confirmation', () => {
    const finish = vi.fn()
    render(<InvoiceActionConfirmation request={request} onFinish={finish} />)
    expect(screen.getByText('This removes the DRAFT watermark. It does not send the invoice.')).toBeDefined()
    expect(finish).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('checkbox'))
    fireEvent.click(screen.getByRole('button', { name: 'Issue invoice' }))
    expect(finish).toHaveBeenCalledWith({ completeLinkedGigs: true })
  })

  it('cancels through the button or Escape without confirming', () => {
    const finish = vi.fn()
    render(<InvoiceActionConfirmation request={request} onFinish={finish} />)
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    fireEvent(screen.getByTestId('invoice-issue-confirmation'), new Event('cancel', { bubbles: true, cancelable: true }))
    expect(finish.mock.calls).toEqual([[null], [null]])
  })

  it('requires issue-and-publish for drafts without a watermarked alternative', () => {
    const finish = vi.fn()
    render(<InvoiceActionConfirmation request={{ ...request, action: 'publish', linkedGigCount: 0 }} onFinish={finish} />)
    expect(screen.queryByRole('checkbox')).toBeNull()
    expect(screen.getByRole('button', { name: 'Issue and publish' })).toBeDefined()
    expect(screen.queryByRole('button', { name: 'Publish watermarked draft' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Issue and publish' }))
    expect(finish).toHaveBeenCalledWith({ completeLinkedGigs: false })
  })

  it('uses the same styled dialog for already-issued publication without reissuance', () => {
    render(<InvoiceActionConfirmation request={{ ...request, action: 'publish', invoice: { ...request.invoice, status: 'Paid' } }} onFinish={vi.fn()} />)
    expect(screen.getByRole('button', { name: 'Publish invoice' })).toBeDefined()
    expect(screen.queryByRole('checkbox')).toBeNull()
    expect(screen.getByText('Publish this invoice PDF to your connected Google Drive.')).toBeDefined()
  })

  it('resolves cancelled requests and prevents stacked confirmations', async () => {
    const { result } = renderHook(() => useInvoiceActionConfirmation())
    let pending!: ReturnType<typeof result.current.confirm>
    act(() => { pending = result.current.confirm(request) })
    expect(result.current.request).toBe(request)
    expect(await result.current.confirm(request)).toBeNull()
    await act(async () => { await result.current.finish(null) })
    expect(await pending).toBeNull()
    expect(result.current.request).toBeNull()
  })

  it('cancels pending consent when the authenticated session ends', async () => {
    const { result, rerender } = renderHook(({ enabled }) => useInvoiceActionConfirmation(enabled), { initialProps: { enabled: true } })
    let pending!: ReturnType<typeof result.current.confirm>
    act(() => { pending = result.current.confirm(request) })
    rerender({ enabled: false })
    expect(await pending).toBeNull()
    expect(result.current.request).toBeNull()
  })

  it('keeps a failed action open with inline feedback and retains the draft', async () => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation(async () => new Response(JSON.stringify(request.invoice))))
    const execute = vi.fn().mockRejectedValue(new Error('Monthly allowance reached. Resets next month.'))
    const { result } = renderHook(() => useInvoiceActionConfirmation())
    let pending!: ReturnType<typeof result.current.confirm>
    act(() => { pending = result.current.confirm(request, execute) })
    await act(async () => { await result.current.finish({ completeLinkedGigs: false }) })
    expect(result.current.error).toContain('Monthly allowance reached')
    expect(result.current.request?.invoice.status).toBe('Draft')
    expect(result.current.busy).toBe(false)
    await act(async () => { await result.current.finish(null) })
    expect(await pending).toBeNull()
  })

  it('refreshes Issued state after partial success and retries without repeating issuance', async () => {
    const issued = { ...request.invoice, status: 'Issued' }
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(request.invoice)))
      .mockImplementation(async () => new Response(JSON.stringify(issued))))
    const execute = vi.fn().mockRejectedValueOnce(new Error('Issued, but delivery failed.')).mockResolvedValueOnce(undefined)
    const refreshed = vi.fn()
    const { result } = renderHook(() => useInvoiceActionConfirmation())
    let pending!: ReturnType<typeof result.current.confirm>
    act(() => { pending = result.current.confirm({ ...request, action: 'publish' }, execute, refreshed) })
    await act(async () => { await result.current.finish({ completeLinkedGigs: false }) })
    expect(result.current.request?.invoice.status).toBe('Issued')
    expect(refreshed).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Issued' }))
    await act(async () => { await result.current.finish({ completeLinkedGigs: false }) })
    expect(execute.mock.calls[1][1].status).toBe('Issued')
    expect(await pending).toEqual({ completeLinkedGigs: false })
    expect(result.current.request).toBeNull()
  })

  it('blocks exhausted first issue but permits corrections within the same confirmation', () => {
    const snapshot = {
      effectivePlan: { code: 'free', displayName: 'Free' },
      invoiceIssuance: { used: 2, limit: 2, remaining: 0, resetsAtUtc: '2026-12-01T00:00:00Z' },
    } as EntitlementSnapshot
    const view = render(<InvoiceActionConfirmation request={request} entitlements={snapshot} onFinish={vi.fn()} />)
    expect((screen.getByRole('button', { name: 'Issue invoice' }) as HTMLButtonElement).disabled).toBe(true)
    view.rerender(<InvoiceActionConfirmation request={{ ...request, invoice: { ...request.invoice, hasBeenIssued: true } }}
      entitlements={snapshot} onFinish={vi.fn()} />)
    expect((screen.getByRole('button', { name: 'Issue invoice' }) as HTMLButtonElement).disabled).toBe(false)
    expect(screen.getByText(/uses no additional monthly allowance/)).toBeTruthy()
  })
})
