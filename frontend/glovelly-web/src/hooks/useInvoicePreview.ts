import { useCallback, useEffect, useState } from 'react'
import {
  buildApiUrl,
  createBlobObjectUrl,
  downloadResponseBlob,
  fetchWithSession,
} from '../api'
import type { Invoice } from '../types'

type UseInvoicePreviewOptions = {
  onDownloaded: (message: string) => void
}

export function useInvoicePreview({ onDownloaded }: UseInvoicePreviewOptions) {
  const [invoicePreviewInvoice, setInvoicePreviewInvoice] = useState<Invoice | null>(null)
  const [invoicePreviewPdfUrl, setInvoicePreviewPdfUrl] = useState<string | null>(null)
  const [invoicePreviewStatus, setInvoicePreviewStatus] = useState('')
  const [isInvoicePreviewLoading, setIsInvoicePreviewLoading] = useState(false)

  const clearInvoicePreviewPdfUrl = useCallback(() => {
    setInvoicePreviewPdfUrl((current) => {
      if (current) {
        window.URL.revokeObjectURL(current)
      }

      return null
    })
  }, [])

  useEffect(() => clearInvoicePreviewPdfUrl, [clearInvoicePreviewPdfUrl])

  const closeInvoicePreview = useCallback(() => {
    clearInvoicePreviewPdfUrl()
    setInvoicePreviewInvoice(null)
    setInvoicePreviewStatus('')
    setIsInvoicePreviewLoading(false)
  }, [clearInvoicePreviewPdfUrl])

  const openInvoicePreview = async (invoice: Invoice) => {
    setInvoicePreviewInvoice(invoice)
    setInvoicePreviewStatus(`Preparing ${invoice.invoiceNumber} preview...`)
    setIsInvoicePreviewLoading(true)
    clearInvoicePreviewPdfUrl()

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/pdf`))

      if (!response.ok) {
        throw new Error('Unable to prepare the invoice PDF preview.')
      }

      const previewUrl = await createBlobObjectUrl(response)
      setInvoicePreviewPdfUrl((current) => {
        if (current) {
          window.URL.revokeObjectURL(current)
        }

        return previewUrl
      })
      setInvoicePreviewStatus(`Invoice ${invoice.invoiceNumber} is ready to review.`)
    } catch (error) {
      setInvoicePreviewStatus(
        error instanceof Error ? error.message : 'Unable to prepare the invoice PDF preview.'
      )
    } finally {
      setIsInvoicePreviewLoading(false)
    }
  }

  const downloadInvoicePreviewPdf = async () => {
    if (!invoicePreviewInvoice) {
      return
    }

    const fallbackFilename = `${invoicePreviewInvoice.invoiceNumber}.pdf`
    setIsInvoicePreviewLoading(true)
    setInvoicePreviewStatus(`Preparing ${fallbackFilename}...`)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoicePreviewInvoice.id}/pdf`)
      )

      if (!response.ok) {
        throw new Error('Unable to download the invoice PDF.')
      }

      const filename = await downloadResponseBlob(response, fallbackFilename)
      const message = `Downloaded ${filename}.`
      setInvoicePreviewStatus(message)
      onDownloaded(message)
    } catch (error) {
      setInvoicePreviewStatus(
        error instanceof Error ? error.message : 'Unable to download the invoice PDF.'
      )
    } finally {
      setIsInvoicePreviewLoading(false)
    }
  }

  return {
    closeInvoicePreview,
    downloadInvoicePreviewPdf,
    invoicePreviewInvoice,
    invoicePreviewPdfUrl,
    invoicePreviewStatus,
    isInvoicePreviewLoading,
    openInvoicePreview,
  }
}
