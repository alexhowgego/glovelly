// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, renderHook, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { InvoiceActionConfirmation } from './InvoiceActionConfirmation'
import { useInvoiceActionConfirmation } from '../hooks/useInvoiceActionConfirmation'
import type { InvoiceActionRequest } from '../hooks/useInvoiceActionConfirmation'
import type { Invoice } from '../types'

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
    act(() => result.current.finish(null))
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
})
