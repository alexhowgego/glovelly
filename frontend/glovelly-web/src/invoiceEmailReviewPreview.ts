import type { InvoiceEmailReview } from './types'

export function buildInvoiceEmailReviewPreview(
  review: InvoiceEmailReview,
  message: string,
  includeReceipts: boolean
) {
  let preview = review.plainTextBody
  if (includeReceipts && review.receiptCount > 0) {
    preview += `${review.receiptNote}\n`
  }

  const additionalMessage = message.trim()
  if (additionalMessage) {
    preview += `\n${review.additionalMessageHeading}\n${additionalMessage}`
  }

  return preview
}
