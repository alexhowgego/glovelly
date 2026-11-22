import type { Invoice } from './types'

export function invoiceDeliveryRestriction(invoice: Invoice): string | null {
  return invoice.status === 'Cancelled'
    ? 'Restore this cancelled invoice to Draft and issue it before delivery.'
    : null
}

/** One issue-before-deliver sequence for email and Drive; confirmation belongs to the UI. */
export async function executeInvoiceDelivery<T>(invoice: Invoice, workflow: {
  destination: string
  issue: (invoice: Invoice) => Promise<Invoice>
  onIssued: (invoice: Invoice) => void
  completeLinkedGigs?: (invoice: Invoice) => Promise<void>
  deliver: (invoice: Invoice) => Promise<T>
}): Promise<T> {
  const restriction = invoiceDeliveryRestriction(invoice)
  if (restriction) throw new Error(restriction)
  let document = invoice
  let issued = false
  let deliveryStarted = false
  try {
    if (document.status === 'Draft') {
      document = await workflow.issue(document)
      issued = true
      workflow.onIssued(document)
      await workflow.completeLinkedGigs?.(document)
    }
    deliveryStarted = true
    return await workflow.deliver(document)
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'Please try again.'
    const prefix = issued
      ? deliveryStarted
        ? `Invoice is issued, but ${workflow.destination} delivery could not be confirmed. Retry delivery without issuing again. `
        : `Invoice is issued, but no ${workflow.destination} delivery was attempted. `
      : deliveryStarted ? '' : `Invoice could not be issued; no ${workflow.destination} delivery was attempted. `
    throw new Error(`${prefix}${detail}`, { cause: error })
  }
}
