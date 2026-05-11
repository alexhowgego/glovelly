import { useCallback, useDeferredValue, useMemo, useState } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  parseProblemDetails,
} from '../api'
import { defaultInvoiceStatus } from '../forms'
import { formatCurrency, formatDateTime } from '../formatters'
import type { Invoice, InvoiceStatus } from '../types'

type GoogleDrivePublishLink = {
  href: string
  fileName: string | null
}

type GoogleDrivePublishResponse = {
  invoice: Invoice
  fileId: string | null
  fileName: string | null
  webViewLink: string | null
}

type UseInvoicesWorkspaceOptions = {
  clientNamesById: ReadonlyMap<string, string>
  onInvoiceDeleted: (invoice: Invoice) => void
}

function extractDownloadFilename(contentDisposition: string | null) {
  if (!contentDisposition) {
    return null
  }

  const encodedMatch = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i)
  if (encodedMatch?.[1]) {
    try {
      return decodeURIComponent(encodedMatch[1])
    } catch {
      return encodedMatch[1]
    }
  }

  const quotedMatch = contentDisposition.match(/filename="([^"]+)"/i)
  if (quotedMatch?.[1]) {
    return quotedMatch[1]
  }

  const plainMatch = contentDisposition.match(/filename=([^;]+)/i)
  return plainMatch?.[1]?.trim() ?? null
}

export function useInvoicesWorkspace({
  clientNamesById,
  onInvoiceDeleted,
}: UseInvoicesWorkspaceOptions) {
  const [invoices, setInvoices] = useState<Invoice[]>([])
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<string>('')
  const [isInvoiceEditorOpen, setIsInvoiceEditorOpen] = useState(false)
  const [invoiceSearchQuery, setInvoiceSearchQuery] = useState('')
  const [invoiceStatus, setInvoiceStatus] = useState(defaultInvoiceStatus)
  const [googleDrivePublishLink, setGoogleDrivePublishLink] =
    useState<GoogleDrivePublishLink | null>(null)
  const [isInvoiceLoading, setIsInvoiceLoading] = useState(false)
  const [adjustmentAmount, setAdjustmentAmount] = useState('')
  const [adjustmentReason, setAdjustmentReason] = useState('')
  const deferredInvoiceSearchQuery = useDeferredValue(invoiceSearchQuery)

  const invoicesById = useMemo(
    () => new Map(invoices.map((invoice) => [invoice.id, invoice])),
    [invoices]
  )

  const filteredInvoices = useMemo(() => {
    const query = deferredInvoiceSearchQuery.trim().toLowerCase()
    const sortedInvoices = [...invoices].sort((left, right) => {
      const dateComparison = right.invoiceDate.localeCompare(left.invoiceDate)
      if (dateComparison !== 0) {
        return dateComparison
      }

      return left.invoiceNumber.localeCompare(right.invoiceNumber)
    })

    if (!query) {
      return sortedInvoices
    }

    return sortedInvoices.filter((invoice) => {
      const clientName = clientNamesById.get(invoice.clientId) ?? ''

      return [
        invoice.invoiceNumber,
        invoice.description ?? '',
        invoice.status,
        clientName,
      ]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
  }, [clientNamesById, deferredInvoiceSearchQuery, invoices])

  const selectedInvoice =
    invoicesById.get(selectedInvoiceId) ?? filteredInvoices[0] ?? null

  const applyInvoices = useCallback((nextInvoices: Invoice[]) => {
    setInvoices(nextInvoices)
    setSelectedInvoiceId(nextInvoices[0]?.id ?? '')
  }, [])

  const resetInvoicesWorkspace = useCallback(() => {
    setInvoices([])
    setSelectedInvoiceId('')
    setIsInvoiceEditorOpen(false)
    setInvoiceSearchQuery('')
    setInvoiceStatus(defaultInvoiceStatus)
    setGoogleDrivePublishLink(null)
    setIsInvoiceLoading(false)
    setAdjustmentAmount('')
    setAdjustmentReason('')
  }, [])

  const startInvoiceEdit = () => {
    if (!selectedInvoice) {
      return
    }

    setIsInvoiceEditorOpen(true)
  }

  const closeInvoiceEditor = () => {
    setIsInvoiceEditorOpen(false)
    setAdjustmentAmount('')
    setAdjustmentReason('')
  }

  const handleDownloadInvoicePdf = async (invoice: Invoice) => {
    const fallbackFilename = `${invoice.invoiceNumber}.pdf`
    setIsInvoiceLoading(true)
    setInvoiceStatus(`Preparing ${fallbackFilename}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/pdf`))
      if (!response.ok) {
        throw new Error('Unable to download the invoice PDF.')
      }

      const contentDisposition = response.headers.get('Content-Disposition')
      const blob = await response.blob()
      const downloadUrl = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = downloadUrl
      link.download = extractDownloadFilename(contentDisposition) ?? fallbackFilename
      document.body.append(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(downloadUrl)
      setInvoiceStatus(`Downloaded ${link.download}.`)
    } catch (error) {
      setInvoiceStatus(
        error instanceof Error ? error.message : 'Unable to download the invoice PDF.'
      )
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleInvoiceStatusChange = async (invoice: Invoice, status: InvoiceStatus) => {
    if (invoice.status === status) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Updating ${invoice.invoiceNumber} to ${status}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/status`), {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ status }),
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const fieldError = problem?.errors?.status?.[0]
        throw new Error(fieldError ?? problem?.detail ?? problem?.title ?? 'Unable to update status.')
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setInvoiceStatus(`Invoice ${updatedInvoice.invoiceNumber} is now ${updatedInvoice.status}.`)
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to update invoice status.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleInvoiceReissue = async (invoice: Invoice) => {
    const isRedraft = invoice.status === 'Draft'
    const actionLabel = isRedraft ? 'Redraft' : 'Re-issue'
    const actionVerb = isRedraft ? 'Redrafting' : 'Re-issuing'
    const shouldProceed = window.confirm(
      isRedraft
        ? `Redraft ${invoice.invoiceNumber}? This will regenerate the draft document without changing reissue history.`
        : `Re-issue ${invoice.invoiceNumber}? This will regenerate the document and log the action.`
    )
    if (!shouldProceed) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`${actionVerb} ${invoice.invoiceNumber}...`)

    try {
      const actionPath = isRedraft ? 'redraft' : 'reissue'
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/${actionPath}`), {
        method: 'POST',
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const fieldError = problem?.errors?.recipient?.[0]
        const statusError = problem?.errors?.status?.[0]
        throw new Error(
          fieldError ??
            statusError ??
            problem?.detail ??
            problem?.title ??
            `Unable to ${actionLabel.toLowerCase()} invoice.`
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )

      if (isRedraft) {
        setInvoiceStatus(`Invoice ${updatedInvoice.invoiceNumber} draft regenerated.`)
      } else {
        const reissuedAt = formatDateTime(updatedInvoice.lastReissuedUtc)
        setInvoiceStatus(`Invoice ${updatedInvoice.invoiceNumber} re-issued at ${reissuedAt}.`)
      }
    } catch (error) {
      setInvoiceStatus(
        error instanceof Error ? error.message : `Unable to ${actionLabel.toLowerCase()} invoice.`
      )
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleSendInvoiceEmail = async (invoice: Invoice) => {
    const message = window.prompt(
      `Add an optional message for ${invoice.invoiceNumber}, or leave blank to send the standard note.`
    )
    if (message === null) {
      return
    }
    const includeReceipts = window.confirm(
      `Include any expense receipt attachments for ${invoice.invoiceNumber}?`
    )

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Sending ${invoice.invoiceNumber} to client...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/send-email`), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          message: message.trim() || null,
          includeReceipts,
        }),
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const recipientError = problem?.errors?.recipient?.[0]
        const pdfError = problem?.errors?.pdf?.[0]
        const attachmentError = problem?.errors?.attachments?.[0]
        throw new Error(
          recipientError ??
            pdfError ??
            attachmentError ??
            problem?.detail ??
            problem?.title ??
            'Unable to send invoice email.'
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setInvoiceStatus(
        `Invoice ${updatedInvoice.invoiceNumber} sent to ${updatedInvoice.lastDeliveryRecipient}.`
      )
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to send invoice email.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handlePublishInvoiceGoogleDrive = async (invoice: Invoice) => {
    const shouldProceed = window.confirm(
      `Publish ${invoice.invoiceNumber} to your connected Google Drive?`
    )
    if (!shouldProceed) {
      return
    }

    setIsInvoiceLoading(true)
    setGoogleDrivePublishLink(null)
    setInvoiceStatus(`Publishing ${invoice.invoiceNumber} to Google Drive...`)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/publish/google-drive`),
        {
          method: 'POST',
        }
      )

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const folderError = problem?.errors?.folderId?.[0]
        const pdfError = problem?.errors?.pdf?.[0]
        throw new Error(
          folderError ??
            pdfError ??
            problem?.detail ??
            problem?.title ??
            'Unable to publish invoice to Google Drive.'
        )
      }

      const publishResult = (await response.json()) as GoogleDrivePublishResponse
      const updatedInvoice = publishResult.invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      const driveLink = publishResult.webViewLink?.trim()
      if (driveLink) {
        setGoogleDrivePublishLink({
          href: driveLink,
          fileName: publishResult.fileName,
        })
        setInvoiceStatus(`Uploaded ${updatedInvoice.invoiceNumber} to Google Drive.`)
      } else {
        setInvoiceStatus(`Invoice ${updatedInvoice.invoiceNumber} published to Google Drive.`)
      }
    } catch (error) {
      setGoogleDrivePublishLink(null)
      setInvoiceStatus(
        error instanceof Error
          ? error.message
          : 'Unable to publish invoice to Google Drive.'
      )
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleAddInvoiceAdjustment = async (invoice: Invoice) => {
    const amount = Number.parseFloat(adjustmentAmount)
    if (!Number.isFinite(amount) || amount === 0) {
      setInvoiceStatus('Enter a non-zero adjustment amount.')
      return
    }

    const reason = adjustmentReason.trim()
    if (!reason) {
      setInvoiceStatus('Add a reason before saving an adjustment.')
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Saving adjustment on ${invoice.invoiceNumber}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/adjustments`), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          amount,
          reason,
        }),
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const amountError = problem?.errors?.amount?.[0]
        const reasonError = problem?.errors?.reason?.[0]
        throw new Error(
          amountError ??
            reasonError ??
            problem?.detail ??
            problem?.title ??
            'Unable to add invoice adjustment.'
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setAdjustmentAmount('')
      setAdjustmentReason('')
      setInvoiceStatus(`Adjustment saved. ${updatedInvoice.invoiceNumber} now totals ${formatCurrency(updatedInvoice.total)}.`)
      setIsInvoiceEditorOpen(false)
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to add invoice adjustment.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleDeleteInvoice = async (invoice: Invoice) => {
    if (invoice.status !== 'Draft') {
      setInvoiceStatus(
        `Only Draft invoices can be deleted. ${invoice.invoiceNumber} is currently ${invoice.status}.`
      )
      return
    }

    const shouldProceed = window.confirm(
      `Delete ${invoice.invoiceNumber}? This cannot be undone and should only be used for draft mistakes.`
    )
    if (!shouldProceed) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Deleting ${invoice.invoiceNumber}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}`), {
        method: 'DELETE',
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const statusError = problem?.errors?.status?.[0]
        throw new Error(
          statusError ?? problem?.detail ?? problem?.title ?? 'Unable to delete invoice.'
        )
      }

      setInvoices((current) => current.filter((value) => value.id !== invoice.id))
      onInvoiceDeleted(invoice)
      setSelectedInvoiceId((current) => (current === invoice.id ? '' : current))
      setInvoiceStatus(`Invoice ${invoice.invoiceNumber} deleted.`)
      setIsInvoiceEditorOpen(false)
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to delete invoice.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  return {
    adjustmentAmount,
    adjustmentReason,
    applyInvoices,
    closeInvoiceEditor,
    draftInvoiceCount: invoices.filter((invoice) => invoice.status === 'Draft').length,
    filteredInvoices,
    googleDrivePublishLink,
    handleAddInvoiceAdjustment,
    handleDeleteInvoice,
    handleDownloadInvoicePdf,
    handleInvoiceReissue,
    handleInvoiceStatusChange,
    handlePublishInvoiceGoogleDrive,
    handleSendInvoiceEmail,
    invoices,
    invoiceSearchQuery,
    invoiceStatus,
    isInvoiceEditorOpen,
    issuedInvoiceCount: invoices.filter((invoice) => invoice.status === 'Issued').length,
    isInvoiceLoading,
    overdueInvoiceCount: invoices.filter((invoice) => invoice.status === 'Overdue').length,
    resetInvoicesWorkspace,
    selectedInvoice,
    setAdjustmentAmount,
    setAdjustmentReason,
    setInvoices,
    setInvoiceStatus,
    setIsInvoiceLoading,
    setSelectedInvoiceId,
    setInvoiceSearchQuery,
    startInvoiceEdit,
  }
}
