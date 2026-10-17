import type { Invoice } from './types'

export function invoiceSaveMessage(invoices: Invoice[], failureOnly = false): string {
  const failed = invoices.find(invoice => invoice.documentState !== 'Current' || invoice.gigLinesNeedRefresh)
  if (failed) return `Receipt saved, but ${failed.invoiceNumber} PDF is unavailable. Retry it from Invoices.`
  if (failureOnly) return ''
  return invoices.length ? 'Receipt details saved and linked draft invoices refreshed.' : 'Attachment updated.'
}
