import { buildApiUrl, fetchWithSession, getResponseErrorMessage, handleSessionExpired, SessionExpiredError } from './api'
import { getInvoiceDocumentAvailability } from './invoiceDocumentState'
import type { Invoice } from './types'

export const MAX_THUMBNAIL_PDF_BYTES = 15 * 1024 * 1024

// PostgreSQL stores microseconds, whereas an immediate .NET mutation response
// can still contain a seventh fractional digit. Compare instants at storage
// precision, retaining microseconds so real replacements are not hidden.
function persistedPdfTimestamp(value: string | null | undefined) {
  if (!value) return value
  const milliseconds = Date.parse(value)
  if (!Number.isFinite(milliseconds)) return value
  const fraction = value.match(/\.(\d+)(?:Z|[+-]\d{2}:\d{2})$/i)?.[1] ?? ''
  return milliseconds * 1000 + Number(fraction.slice(3, 6).padEnd(3, '0'))
}

export function invoiceThumbnailIdentity(invoice: Invoice): string {
  return JSON.stringify([invoice.id, invoice.documentState, invoice.documentRevision,
    invoice.pdfDocumentRevision, persistedPdfTimestamp(invoice.pdfGeneratedAt), invoice.pdfSizeBytes, invoice.gigLinesNeedRefresh ?? false])
}

export async function readLimitedThumbnailPdf(response: Response, signal: AbortSignal): Promise<Uint8Array<ArrayBuffer>> {
  if (Number(response.headers.get('content-length')) > MAX_THUMBNAIL_PDF_BYTES) {
    await response.body?.cancel()
    throw new Error('This PDF is too large for an automatic thumbnail. Use Preview instead.')
  }
  const reader = response.body?.getReader()
  if (!reader) throw new Error('Thumbnail unavailable. Use Preview instead.')
  const cancel = () => { void reader.cancel().catch(() => {}) }
  signal.addEventListener('abort', cancel, { once: true })
  const chunks: Uint8Array[] = []
  let length = 0
  try {
    while (true) {
      signal.throwIfAborted()
      const { done, value } = await reader.read()
      signal.throwIfAborted()
      if (done) break
      length += value.byteLength
      if (length > MAX_THUMBNAIL_PDF_BYTES) throw new Error('This PDF is too large for an automatic thumbnail. Use Preview instead.')
      chunks.push(value)
    }
    const bytes = new Uint8Array(length)
    let offset = 0
    for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength }
    return bytes
  } finally {
    signal.removeEventListener('abort', cancel)
    await reader.cancel().catch(() => {})
    reader.releaseLock()
  }
}

export async function loadInvoiceThumbnailBytes(invoice: Invoice, signal: AbortSignal,
  onSessionExpired: (message: string) => void): Promise<Uint8Array<ArrayBuffer>> {
  signal.throwIfAborted()
  if (!getInvoiceDocumentAvailability(invoice).isCurrent || invoice.gigLinesNeedRefresh) {
    throw new Error('Thumbnail unavailable while the invoice PDF is not current.')
  }
  if ((invoice.pdfSizeBytes ?? 0) > MAX_THUMBNAIL_PDF_BYTES) {
    throw new Error('This PDF is too large for an automatic thumbnail. Use Preview instead.')
  }

  const read = async (path: string) => {
    const response = await fetchWithSession(buildApiUrl(path), { signal })
    signal.throwIfAborted()
    if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again.')) throw new SessionExpiredError()
    if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Thumbnail unavailable. Use Preview instead.'))
    return response
  }

  const response = await read(`/invoices/${invoice.id}/pdf`)
  if (response.headers.get('content-type')?.split(';')[0].toLowerCase() !== 'application/pdf') {
    await response.body?.cancel()
    throw new Error('Thumbnail unavailable. Use Preview instead.')
  }
  const bytes = await readLimitedThumbnailPdf(response, signal)
  const metadata = await read(`/invoices/${invoice.id}`)
  const current = await metadata.json() as Invoice
  signal.throwIfAborted()
  if (!getInvoiceDocumentAvailability(current).isCurrent || invoiceThumbnailIdentity(current) !== invoiceThumbnailIdentity(invoice)) {
    throw new Error('The invoice PDF changed. Refresh or reselect the invoice to load its thumbnail.')
  }
  return bytes
}
