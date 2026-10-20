import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchWithSession } from './api'
import { invoiceThumbnailIdentity, loadInvoiceThumbnailBytes, MAX_THUMBNAIL_PDF_BYTES, readLimitedThumbnailPdf } from './invoiceThumbnail'
import type { Invoice } from './types'

vi.mock('./api', async importOriginal => ({ ...await importOriginal<typeof import('./api')>(), fetchWithSession: vi.fn() }))
afterEach(() => vi.resetAllMocks())

const invoice = (overrides: Partial<Invoice> = {}) => ({ id: 'one', documentState: 'Current', documentRevision: 2,
  pdfDocumentRevision: 2, pdfGeneratedAt: '2026-10-19T09:00:00Z', pdfSizeBytes: 3, ...overrides }) as Invoice
const pdf = () => new Response(new Uint8Array([1, 2, 3]), { headers: { 'content-type': 'application/pdf' } })

describe('uncached thumbnail PDF reads', () => {
  it('reads only the stored PDF and metadata and retains no reusable bytes between calls', async () => {
    const selected = invoice()
    vi.mocked(fetchWithSession).mockImplementation(async path => path.endsWith('/pdf') ? pdf() : new Response(JSON.stringify(selected)))
    const signal = new AbortController().signal
    const first = await loadInvoiceThumbnailBytes(selected, signal, vi.fn())
    const second = await loadInvoiceThumbnailBytes(selected, signal, vi.fn())
    expect([...first]).toEqual([1, 2, 3])
    expect(second).not.toBe(first)
    expect(fetchWithSession).toHaveBeenCalledTimes(4)
    expect(vi.mocked(fetchWithSession).mock.calls.map(([path]) => path)).toEqual(['/invoices/one/pdf', '/invoices/one', '/invoices/one/pdf', '/invoices/one'])
    for (const [, options] of vi.mocked(fetchWithSession).mock.calls) expect(options?.method).toBeUndefined()
  })

  it.each([
    { documentRevision: 3, pdfDocumentRevision: 3 },
    { pdfDocumentRevision: 1 },
    { pdfGeneratedAt: '2026-10-19T10:00:00Z' },
    { id: 'different' },
    { gigLinesNeedRefresh: true },
  ] satisfies Partial<Invoice>[])('discards changed metadata %j without retries', async changed => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(pdf()).mockResolvedValueOnce(new Response(JSON.stringify(invoice(changed))))
    await expect(loadInvoiceThumbnailBytes(invoice(), new AbortController().signal, vi.fn())).rejects.toThrow('changed')
    expect(fetchWithSession).toHaveBeenCalledTimes(2)
  })

  it.each(['Missing', 'Regenerating', 'Failed'] as const)('does not fetch or generate a %s document', async documentState => {
    await expect(loadInvoiceThumbnailBytes(invoice({ documentState }), new AbortController().signal, vi.fn())).rejects.toThrow('not current')
    expect(fetchWithSession).not.toHaveBeenCalled()
  })

  it('does not fetch an oversized document identified by metadata', async () => {
    await expect(loadInvoiceThumbnailBytes(invoice({ pdfSizeBytes: MAX_THUMBNAIL_PDF_BYTES + 1 }), new AbortController().signal, vi.fn())).rejects.toThrow('too large')
    expect(fetchWithSession).not.toHaveBeenCalled()
  })

  it('enforces advertised and streaming byte limits', async () => {
    await expect(readLimitedThumbnailPdf(new Response('', { headers: { 'content-length': String(MAX_THUMBNAIL_PDF_BYTES + 1) } }), new AbortController().signal)).rejects.toThrow('too large')
    const cancel = vi.fn()
    const body = new ReadableStream({ start(controller) { controller.enqueue(new Uint8Array(MAX_THUMBNAIL_PDF_BYTES + 1)) }, cancel })
    await expect(readLimitedThumbnailPdf(new Response(body), new AbortController().signal)).rejects.toThrow('too large')
    expect(cancel).toHaveBeenCalledOnce()
  })

  it('handles session expiry without retaining bytes', async () => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(null, { status: 401 }))
    const expired = vi.fn()
    await expect(loadInvoiceThumbnailBytes(invoice(), new AbortController().signal, expired)).rejects.toMatchObject({ name: 'SessionExpiredError' })
    expect(expired).toHaveBeenCalledOnce()
  })

  it('ignores an obsolete response, including its session-expiry callback', async () => {
    let complete!: (response: Response) => void
    vi.mocked(fetchWithSession).mockReturnValueOnce(new Promise(resolve => { complete = resolve }))
    const controller = new AbortController()
    const expired = vi.fn()
    const pending = loadInvoiceThumbnailBytes(invoice(), controller.signal, expired)
    controller.abort()
    complete(new Response(null, { status: 401 }))
    await expect(pending).rejects.toMatchObject({ name: 'AbortError' })
    expect(expired).not.toHaveBeenCalled()
  })

  it('ignores unrelated invoice metadata for render identity', () => {
    expect(invoiceThumbnailIdentity(invoice({ description: 'new description', total: 100 }))).toBe(invoiceThumbnailIdentity(invoice()))
  })
})
