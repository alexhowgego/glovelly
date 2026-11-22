// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchWithSession } from '../api'
import type { Invoice } from '../types'
import { useInvoicesWorkspace } from './useInvoicesWorkspace'

vi.mock('../api', async original => ({ ...await original<typeof import('../api')>(), fetchWithSession: vi.fn() }))
vi.mock('../notifications', () => ({ notifications: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks(); vi.restoreAllMocks() })

const draft = { id: 'invoice', invoiceNumber: 'GLV-001', status: 'Draft', description: '', lines: [],
  firstIssuedUtc: null, reissueCount: 0, deliveryCount: 0 } as unknown as Invoice
const issued = { ...draft, status: 'Issued', firstIssuedUtc: '2026-11-21T10:00:00Z', pdfHasDraftWatermark: false, isPdfCurrent: true } as Invoice
const options = { clientNamesById: new Map<string, string>(), onInvoiceDeleted: vi.fn() }
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })

function setup(failure?: 'issue' | 'send') {
  const calls: string[] = []
  let sent = 0
  vi.mocked(fetchWithSession).mockImplementation(async url => {
    if (url.endsWith('/email-review')) return json({ receiptCount: 0 })
    if (url.endsWith('/status')) {
      calls.push('issue')
      return failure === 'issue' ? json({ title: 'Issuance unavailable' }, 400) : json(issued)
    }
    if (url.endsWith('/send-email')) {
      calls.push('send')
      sent++
      return failure === 'send' && sent === 1 ? json({ title: 'Delivery unavailable' }, 503)
        : json({ ...issued, deliveryCount: 1 })
    }
    if (url.endsWith('/publish/google-drive')) {
      calls.push('publish')
      return json({ invoice: issued })
    }
    throw new Error(`Unexpected request ${url}`)
  })
  const hook = renderHook(() => useInvoicesWorkspace(options))
  return { ...hook, calls }
}

describe('invoice issue-and-deliver ordering', () => {
  it('uses the same issue-before-delivery sequence for Drive and skips issuance on delivery retry', async () => {
    const { result, calls } = setup()
    await act(async () => { await result.current.handlePublishInvoiceGoogleDrive(draft) })
    await act(async () => { await result.current.handlePublishInvoiceGoogleDrive(issued) })
    expect(calls).toEqual(['issue', 'publish', 'publish'])
  })

  it('blocks Drive publication when issuing fails', async () => {
    const { result, calls } = setup('issue')
    await act(async () => { expect(await result.current.handlePublishInvoiceGoogleDrive(draft)).toBeNull() })
    expect(calls).toEqual(['issue'])
  })
  it('issues before sending and includes optional gig completion without another prompt', async () => {
    const { result, calls } = setup()
    const confirm = vi.spyOn(window, 'confirm')
    await act(async () => { await result.current.openInvoiceEmailReview(draft) })
    act(() => result.current.setCompleteInvoiceEmailGigs(true))
    const complete = vi.fn(async () => { calls.push('complete') })
    await act(async () => { await result.current.submitInvoiceEmailReview(complete) })
    expect(calls).toEqual(['issue', 'complete', 'send'])
    expect(complete).toHaveBeenCalledWith(issued)
    expect(result.current.invoiceEmailReviewInvoice?.status).toBe('Issued')
    expect(confirm).not.toHaveBeenCalled()
  })

  it('does not send when issuing fails and keeps actionable inline feedback', async () => {
    const { result, calls } = setup('issue')
    await act(async () => { await result.current.openInvoiceEmailReview(draft) })
    await act(async () => { expect(await result.current.submitInvoiceEmailReview()).toBeNull() })
    expect(calls).toEqual(['issue'])
    expect(result.current.invoiceEmailReviewInvoice?.status).toBe('Draft')
    expect(result.current.invoiceEmailReviewError).toContain('no email delivery was attempted')
  })

  it('retains issued state after send failure and retries without issuing again', async () => {
    const { result, calls } = setup('send')
    await act(async () => { await result.current.openInvoiceEmailReview(draft) })
    await act(async () => { expect(await result.current.submitInvoiceEmailReview()).toBeNull() })
    expect(result.current.invoiceEmailReviewInvoice?.status).toBe('Issued')
    expect(result.current.invoiceEmailReviewError).toContain('Invoice is issued')
    await act(async () => { await result.current.submitInvoiceEmailReview() })
    expect(calls).toEqual(['issue', 'send', 'send'])
  })

  it('rejects cancelled delivery before any external mutation', async () => {
    const { result, calls } = setup()
    await act(async () => { await result.current.openInvoiceEmailReview({ ...draft, status: 'Cancelled' }) })
    await act(async () => { await result.current.submitInvoiceEmailReview() })
    expect(calls).toEqual([])
    expect(result.current.invoiceEmailReviewError).toContain('Restore this cancelled invoice')
  })

  it('keeps completion errors inline and does not claim email delivery was attempted', async () => {
    const { result, calls } = setup()
    await act(async () => { await result.current.openInvoiceEmailReview(draft) })
    act(() => result.current.setCompleteInvoiceEmailGigs(true))
    await act(async () => {
      await result.current.submitInvoiceEmailReview(async () => { throw new Error('Linked gig update failed.') })
    })
    expect(calls).toEqual(['issue'])
    expect(result.current.invoiceEmailReviewError).toContain('Invoice is issued, but no email delivery was attempted')
    expect(result.current.invoiceEmailReviewError).toContain('Linked gig update failed')
  })

  it('prevents duplicate issue/send requests from repeated submissions', async () => {
    const { result, calls } = setup()
    await act(async () => { await result.current.openInvoiceEmailReview(draft) })
    await act(async () => {
      const first = result.current.submitInvoiceEmailReview()
      expect(await result.current.submitInvoiceEmailReview()).toBeNull()
      await first
    })
    expect(calls).toEqual(['issue', 'send'])
  })

  it('does not add a second Drive prompt after a combined confirmation', async () => {
    const { result } = setup()
    const confirm = vi.spyOn(window, 'confirm')
    vi.mocked(fetchWithSession).mockResolvedValue(json({ invoice: issued }))
    await act(async () => { await result.current.handlePublishInvoiceGoogleDrive(issued) })
    expect(confirm).not.toHaveBeenCalled()
    expect(fetchWithSession).toHaveBeenCalledWith(expect.stringContaining('/publish/google-drive'), { method: 'POST' })
  })

  it('explains partial success if Drive publishing fails after issuance', async () => {
    const { result } = setup()
    vi.mocked(fetchWithSession).mockResolvedValueOnce(json(issued)).mockResolvedValueOnce(json({ title: 'Drive unavailable' }, 503))
    await act(async () => { expect(await result.current.handlePublishInvoiceGoogleDrive(draft)).toBeNull() })
    expect(result.current.invoiceStatus).toContain('Invoice is issued, but Google Drive delivery could not be confirmed')
  })
})
