import { useCallback, useDeferredValue, useEffect, useMemo, useState } from 'react'
import {
  buildApiUrl,
  downloadResponseBlob,
  fetchWithSession,
  getProblemDetailsMessage,
  getResponseErrorMessage,
  jsonRequestInit,
  parseProblemDetails,
} from '../api'
import { defaultInvoiceStatus } from '../forms'
import { formatCurrency, formatDateTime } from '../formatters'
import type {
  Invoice,
  InvoiceEmailReview,
  InvoiceLine,
  InvoiceQuickFilter,
  InvoiceSort,
  InvoiceStatus,
  PaidIncomeSummary,
} from '../types'
import type { PaidIncomeSummaryState } from '../dashboardCards'
import { notifications } from '../notifications'

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

export function useInvoicesWorkspace({
  clientNamesById,
  onInvoiceDeleted,
}: UseInvoicesWorkspaceOptions) {
  const [invoices, setInvoices] = useState<Invoice[]>([])
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<string>('')
  const [isInvoiceEditorOpen, setIsInvoiceEditorOpen] = useState(false)
  const [invoiceSearchQuery, setInvoiceSearchQuery] = useState('')
  const [invoiceQuickFilter, setInvoiceQuickFilter] =
    useState<InvoiceQuickFilter>('all')
  const [invoiceSort, setInvoiceSort] = useState<InvoiceSort>({
    key: 'priority',
    direction: 'asc',
  })
  const [invoiceStatus, setInvoiceStatus] = useState(defaultInvoiceStatus)
  const [googleDrivePublishLink, setGoogleDrivePublishLink] =
    useState<GoogleDrivePublishLink | null>(null)
  const [isInvoiceLoading, setIsInvoiceLoading] = useState(false)
  const [paidIncomeSummary, setPaidIncomeSummary] = useState<PaidIncomeSummaryState>({
    status: 'loading',
  })
  const [incomeInvoiceIds, setIncomeInvoiceIds] = useState<string[]>([])
  const [adjustmentAmount, setAdjustmentAmount] = useState('')
  const [adjustmentReason, setAdjustmentReason] = useState('')
  const [invoiceDescription, setInvoiceDescription] = useState('')
  const [invoiceEmailReviewInvoice, setInvoiceEmailReviewInvoice] = useState<Invoice | null>(null)
  const [invoiceEmailReview, setInvoiceEmailReview] = useState<InvoiceEmailReview | null>(null)
  const [invoiceEmailReviewMessage, setInvoiceEmailReviewMessage] = useState('')
  const [includeInvoiceEmailReceipts, setIncludeInvoiceEmailReceipts] = useState(false)
  const [issueInvoiceAfterEmail, setIssueInvoiceAfterEmail] = useState(false)
  const [invoiceEmailReviewError, setInvoiceEmailReviewError] = useState('')
  const [isInvoiceEmailReviewLoading, setIsInvoiceEmailReviewLoading] = useState(false)
  const deferredInvoiceSearchQuery = useDeferredValue(invoiceSearchQuery)

  const invoicesById = useMemo(
    () => new Map(invoices.map((invoice) => [invoice.id, invoice])),
    [invoices]
  )

  const loadPaidIncomeSummary = useCallback(async () => {
    setPaidIncomeSummary({ status: 'loading' })

    try {
      const response = await fetchWithSession(buildApiUrl('/invoices/paid-income-summary'))
      if (!response.ok) {
        throw new Error('Unable to load paid income.')
      }

      const summary = (await response.json()) as PaidIncomeSummary
      setPaidIncomeSummary({ status: 'ready', summary })
      setIncomeInvoiceIds(summary.invoiceIds)
    } catch {
      setPaidIncomeSummary({ status: 'error' })
    }
  }, [])

  const filteredInvoices = useMemo(() => {
    const query = deferredInvoiceSearchQuery.trim().toLowerCase()
    const sortDirection = invoiceSort.direction === 'asc' ? 1 : -1
    const compareText = (left: string, right: string) => left.localeCompare(right)
    const compareNumber = (left: number, right: number) => left - right
    const getClientName = (invoice: Invoice) => clientNamesById.get(invoice.clientId) ?? ''
    const getPriorityBucket = (invoice: Invoice) => {
      switch (invoice.status) {
        case 'Overdue':
          return 0
        case 'Issued':
          return 1
        case 'Draft':
          return 2
        case 'Paid':
          return 4
        case 'Cancelled':
          return 5
        default:
          return 3
      }
    }
    const comparePriority = (left: Invoice, right: Invoice) => {
      const bucketComparison = getPriorityBucket(left) - getPriorityBucket(right)
      if (bucketComparison !== 0) {
        return bucketComparison
      }

      const bucket = getPriorityBucket(left)
      if (bucket === 0 || bucket === 1) {
        return compareText(left.dueDate, right.dueDate)
      }

      return compareText(right.invoiceDate, left.invoiceDate)
    }
    const compareByKey = (left: Invoice, right: Invoice) => {
      switch (invoiceSort.key) {
        case 'client':
          return compareText(getClientName(left), getClientName(right))
        case 'dueDate':
          return compareText(left.dueDate, right.dueDate)
        case 'invoiceNumber':
          return compareText(left.invoiceNumber, right.invoiceNumber)
        case 'status':
          return compareText(left.status, right.status)
        case 'total':
          return compareNumber(left.total, right.total)
        case 'priority':
          return comparePriority(left, right)
        case 'invoiceDate':
        default:
          return compareText(left.invoiceDate, right.invoiceDate)
      }
    }
    const sortedInvoices = [...invoices].sort((left, right) => {
      const primaryComparison = compareByKey(left, right)
      if (primaryComparison !== 0) {
        return primaryComparison * sortDirection
      }

      const dateComparison = left.invoiceDate.localeCompare(right.invoiceDate)
      if (dateComparison !== 0) {
        return dateComparison
      }

      const numberComparison = left.invoiceNumber.localeCompare(right.invoiceNumber)
      if (numberComparison !== 0) {
        return numberComparison
      }

      return left.id.localeCompare(right.id)
    })
    const quickFilteredInvoices = sortedInvoices.filter((invoice) => {
      switch (invoiceQuickFilter) {
        case 'drafts':
          return invoice.status === 'Draft'
        case 'outstanding':
          return invoice.status !== 'Paid' && invoice.status !== 'Cancelled'
        case 'overdue':
          return invoice.status === 'Overdue'
        case 'paid':
          return invoice.status === 'Paid'
        case 'income-this-financial-year':
          return incomeInvoiceIds.includes(invoice.id)
        case 'all':
        default:
          return true
      }
    })

    if (!query) {
      return quickFilteredInvoices
    }

    return quickFilteredInvoices.filter((invoice) => {
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
  }, [clientNamesById, deferredInvoiceSearchQuery, incomeInvoiceIds, invoiceQuickFilter, invoiceSort, invoices])

  const selectedInvoice = selectedInvoiceId
    ? invoicesById.get(selectedInvoiceId) ?? null
    : filteredInvoices[0] ?? null

  useEffect(() => {
    setInvoiceDescription(selectedInvoice?.description ?? '')
  }, [selectedInvoice?.description, selectedInvoice?.id])

  const applyInvoices = useCallback((nextInvoices: Invoice[]) => {
    setInvoices(nextInvoices)
    setSelectedInvoiceId(nextInvoices[0]?.id ?? '')
  }, [])

  const resetInvoicesWorkspace = useCallback(() => {
    setInvoices([])
    setSelectedInvoiceId('')
    setIsInvoiceEditorOpen(false)
    setInvoiceSearchQuery('')
    setInvoiceQuickFilter('all')
    setInvoiceSort({ key: 'priority', direction: 'asc' })
    setInvoiceStatus(defaultInvoiceStatus)
    setGoogleDrivePublishLink(null)
    setIsInvoiceLoading(false)
    setPaidIncomeSummary({ status: 'loading' })
    setIncomeInvoiceIds([])
    setAdjustmentAmount('')
    setAdjustmentReason('')
    setInvoiceDescription('')
    setInvoiceEmailReviewInvoice(null)
    setInvoiceEmailReview(null)
    setInvoiceEmailReviewMessage('')
    setIncludeInvoiceEmailReceipts(false)
    setIssueInvoiceAfterEmail(false)
    setInvoiceEmailReviewError('')
    setIsInvoiceEmailReviewLoading(false)
  }, [])

  const startInvoiceEdit = () => {
    if (!selectedInvoice) {
      return
    }

    setIsInvoiceEditorOpen(true)
  }

  const closeInvoiceEditor = () => {
    if (
      (adjustmentAmount.trim().length > 0 ||
        adjustmentReason.trim().length > 0 ||
        invoiceDescription !== (selectedInvoice?.description ?? '')) &&
      !window.confirm('Discard unsaved invoice changes and close line items?')
    ) {
      return
    }

    setIsInvoiceEditorOpen(false)
    setAdjustmentAmount('')
    setAdjustmentReason('')
    setInvoiceDescription(selectedInvoice?.description ?? '')
  }

  const handleDownloadInvoicePdf = async (invoice: Invoice) => {
    const fallbackFilename = `${invoice.invoiceNumber}.pdf`
    setIsInvoiceLoading(true)
    setInvoiceStatus(`Preparing ${fallbackFilename}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/pdf`))
      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to download the invoice PDF.'))
      }

      const filename = await downloadResponseBlob(response, fallbackFilename)
      setInvoiceStatus('')
      notifications.success(`Downloaded ${filename}.`, {
        dedupeKey: `invoice:${invoice.id}:pdf-download`,
      })
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to download the invoice PDF.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:pdf-download` })
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleInvoiceStatusChange = async (invoice: Invoice, status: InvoiceStatus) => {
    if (invoice.status === status) {
      return invoice
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Updating ${invoice.invoiceNumber} to ${status}...`)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/status`),
        jsonRequestInit('PUT', { status })
      )

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const fieldError = problem?.errors?.status?.[0]
        throw new Error(
          fieldError ?? getProblemDetailsMessage(problem, 'Unable to update status.')
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setInvoiceStatus('')
      notifications.success(`Invoice ${updatedInvoice.invoiceNumber} is now ${updatedInvoice.status}.`)
      await loadPaidIncomeSummary()
      return updatedInvoice
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to update invoice status.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:status` })
      return null
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
      return null
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
            getProblemDetailsMessage(
              problem,
              `Unable to ${actionLabel.toLowerCase()} invoice.`
            )
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )

      if (isRedraft) {
        notifications.success(`Invoice ${updatedInvoice.invoiceNumber} draft regenerated.`)
      } else {
        const reissuedAt = formatDateTime(updatedInvoice.lastReissuedUtc)
        notifications.success(`Invoice ${updatedInvoice.invoiceNumber} re-issued at ${reissuedAt}.`)
      }
      setInvoiceStatus('')

      await loadPaidIncomeSummary()
      return updatedInvoice
    } catch (error) {
      const message = error instanceof Error ? error.message : `Unable to ${actionLabel.toLowerCase()} invoice.`
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:${isRedraft ? 'redraft' : 'reissue'}` })
      return null
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const loadInvoiceEmailReview = async (invoice: Invoice) => {
    setIsInvoiceEmailReviewLoading(true)
    setInvoiceEmailReviewError('')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/email-review`),
        { method: 'POST' }
      )
      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to prepare invoice email.'))
      }
      const review = (await response.json()) as InvoiceEmailReview
      setInvoiceEmailReview(review)
      setIncludeInvoiceEmailReceipts(review.receiptCount > 0)
      setIssueInvoiceAfterEmail(true)
    } catch (error) {
      setInvoiceEmailReview(null)
      setInvoiceEmailReviewError(
        error instanceof Error ? error.message : 'Unable to prepare invoice email.'
      )
    } finally {
      setIsInvoiceEmailReviewLoading(false)
    }
  }

  const openInvoiceEmailReview = async (invoice: Invoice) => {
    setInvoiceEmailReviewInvoice(invoice)
    setInvoiceEmailReview(null)
    setInvoiceEmailReviewMessage('')
    setIncludeInvoiceEmailReceipts(false)
    setIssueInvoiceAfterEmail(false)
    await loadInvoiceEmailReview(invoice)
    return invoice
  }

  const closeInvoiceEmailReview = () => {
    if (isInvoiceEmailReviewLoading) {
      return
    }
    setInvoiceEmailReviewInvoice(null)
    setInvoiceEmailReview(null)
    setInvoiceEmailReviewError('')
  }

  const changeInvoiceEmailReceiptInclusion = (includeReceipts: boolean) => {
    setIncludeInvoiceEmailReceipts(includeReceipts)
  }

  const handleDownloadInvoiceReceiptArchive = async (invoice: Invoice) => {
    setIsInvoiceEmailReviewLoading(true)
    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/email-receipts`))
      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to download receipt attachments.'))
      }
      const filename = await downloadResponseBlob(
        response,
        `Invoice-${invoice.invoiceNumber}-Receipts.zip`
      )
      notifications.success(`Downloaded ${filename}.`, {
        dedupeKey: `invoice:${invoice.id}:receipt-attachments`,
      })
    } catch (error) {
      notifications.error(
        error instanceof Error ? error.message : 'Unable to download receipt attachments.',
        { dedupeKey: `invoice:${invoice.id}:receipt-attachments` }
      )
    } finally {
      setIsInvoiceEmailReviewLoading(false)
    }
  }

  const submitInvoiceEmailReview = async () => {
    const invoice = invoiceEmailReviewInvoice
    if (!invoice || !invoiceEmailReview || isInvoiceEmailReviewLoading) {
      return null
    }

    setIsInvoiceEmailReviewLoading(true)
    setInvoiceEmailReviewError('')
    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/send-email`),
        jsonRequestInit('POST', {
          message: invoiceEmailReviewMessage.trim() || null,
          includeReceipts: includeInvoiceEmailReceipts,
        })
      )
      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to send invoice email.'))
      }
      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) => current.map((value) => value.id === updatedInvoice.id ? updatedInvoice : value))
      return updatedInvoice
    } catch (error) {
      setInvoiceEmailReviewError(
        error instanceof Error ? error.message : 'Unable to send invoice email.'
      )
      return null
    } finally {
      setIsInvoiceEmailReviewLoading(false)
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
            getProblemDetailsMessage(problem, 'Unable to publish invoice to Google Drive.')
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
        notifications.success(`Uploaded ${updatedInvoice.invoiceNumber} to Google Drive.`, {
          dedupeKey: `invoice:${invoice.id}:google-drive`,
        })
      } else {
        notifications.success(`Invoice ${updatedInvoice.invoiceNumber} published to Google Drive.`, {
          dedupeKey: `invoice:${invoice.id}:google-drive`,
        })
      }
      setInvoiceStatus('')
      return updatedInvoice
    } catch (error) {
      setGoogleDrivePublishLink(null)
      const message = error instanceof Error ? error.message : 'Unable to publish invoice to Google Drive.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:google-drive` })
      return null
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
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/adjustments`),
        jsonRequestInit('POST', {
          amount,
          reason,
        })
      )

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const amountError = problem?.errors?.amount?.[0]
        const reasonError = problem?.errors?.reason?.[0]
        throw new Error(
          amountError ??
            reasonError ??
            getProblemDetailsMessage(problem, 'Unable to add invoice adjustment.')
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setAdjustmentAmount('')
      setAdjustmentReason('')
      if (updatedInvoice.documentState === 'Current') {
        setInvoiceStatus('')
        notifications.success(
          `Adjustment saved and PDF regenerated. ${updatedInvoice.invoiceNumber} now totals ${formatCurrency(updatedInvoice.total)}.`,
          { dedupeKey: `invoice:${invoice.id}:adjustment` }
        )
      } else {
        const message = updatedInvoice.documentFailureMessage ?? 'Adjustment saved, but the invoice PDF is unavailable.'
        setInvoiceStatus(message)
        notifications.error(message, { dedupeKey: `invoice:${invoice.id}:adjustment` })
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to add invoice adjustment.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:adjustment` })
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleRegenerateInvoicePdf = async (invoice: Invoice) => {
    setIsInvoiceLoading(true)
    setInvoiceStatus(`Regenerating ${invoice.invoiceNumber} PDF...`)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/regenerate-pdf`),
        { method: 'POST' }
      )
      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to regenerate invoice PDF.'))
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      if (updatedInvoice.documentState === 'Current') {
        setInvoiceStatus('')
        notifications.success(`Invoice ${updatedInvoice.invoiceNumber} PDF regenerated.`, {
          dedupeKey: `invoice:${invoice.id}:regenerate-pdf`,
        })
      } else {
        const message = updatedInvoice.documentFailureMessage ?? 'Invoice PDF could not be regenerated. Try again.'
        setInvoiceStatus(message)
        notifications.error(message, { dedupeKey: `invoice:${invoice.id}:regenerate-pdf` })
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to regenerate invoice PDF.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:regenerate-pdf` })
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleInvoiceDescriptionSave = async (invoice: Invoice) => {
    setIsInvoiceLoading(true)
    setInvoiceStatus(`Saving description for ${invoice.invoiceNumber}...`)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/description`),
        jsonRequestInit('PUT', { description: invoiceDescription })
      )

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const descriptionError = problem?.errors?.description?.[0]
        const statusError = problem?.errors?.status?.[0]
        throw new Error(
          descriptionError ??
            statusError ??
            getProblemDetailsMessage(problem, 'Unable to save invoice description.')
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setInvoiceDescription(updatedInvoice.description ?? '')
      setInvoiceStatus('')
      notifications.success(
        `Description saved for ${updatedInvoice.invoiceNumber}. Redraft the invoice to update its PDF.`
      )
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to save invoice description.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:description` })
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleDeleteInvoiceAdjustment = async (invoice: Invoice, line: InvoiceLine) => {
    if (line.type !== 'ManualAdjustment') {
      setInvoiceStatus('Only manual adjustments can be removed from here.')
      return
    }

    if (!window.confirm(`Remove adjustment "${line.description}" from ${invoice.invoiceNumber}?`)) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Removing adjustment from ${invoice.invoiceNumber}...`)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/invoices/${invoice.id}/adjustments/${line.id}`),
        { method: 'DELETE' }
      )

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to remove invoice adjustment.'))
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      if (updatedInvoice.documentState === 'Current') {
        setInvoiceStatus('')
        notifications.success(
          `Adjustment removed and PDF regenerated. ${updatedInvoice.invoiceNumber} now totals ${formatCurrency(updatedInvoice.total)}.`,
          { dedupeKey: `invoice:${invoice.id}:adjustment` }
        )
      } else {
        const message = updatedInvoice.documentFailureMessage ?? 'Adjustment removed, but the invoice PDF is unavailable.'
        setInvoiceStatus(message)
        notifications.error(message, { dedupeKey: `invoice:${invoice.id}:adjustment` })
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to remove invoice adjustment.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:adjustment` })
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
          statusError ?? getProblemDetailsMessage(problem, 'Unable to delete invoice.')
        )
      }

      setInvoices((current) => current.filter((value) => value.id !== invoice.id))
      onInvoiceDeleted(invoice)
      setSelectedInvoiceId((current) => (current === invoice.id ? '' : current))
      setInvoiceStatus('')
      notifications.success(`Invoice ${invoice.invoiceNumber} deleted.`, {
        dedupeKey: `invoice:${invoice.id}:delete`,
      })
      setIsInvoiceEditorOpen(false)
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to delete invoice.'
      setInvoiceStatus(message)
      notifications.error(message, { dedupeKey: `invoice:${invoice.id}:delete` })
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  return {
    adjustmentAmount,
    adjustmentReason,
    invoiceDescription,
    applyInvoices,
    closeInvoiceEditor,
    draftInvoiceCount: invoices.filter((invoice) => invoice.status === 'Draft').length,
    filteredInvoices,
    googleDrivePublishLink,
    handleAddInvoiceAdjustment,
    handleDeleteInvoiceAdjustment,
    handleDeleteInvoice,
    handleDownloadInvoicePdf,
    handleInvoiceReissue,
    handleRegenerateInvoicePdf,
    handleInvoiceDescriptionSave,
    handleInvoiceStatusChange,
    handlePublishInvoiceGoogleDrive,
    handleDownloadInvoiceReceiptArchive,
    changeInvoiceEmailReceiptInclusion,
    closeInvoiceEmailReview,
    openInvoiceEmailReview,
    submitInvoiceEmailReview,
    invoices,
    invoiceQuickFilter,
    invoiceSearchQuery,
    invoiceSort,
    invoiceStatus,
    invoiceEmailReview,
    invoiceEmailReviewError,
    invoiceEmailReviewInvoice,
    invoiceEmailReviewMessage,
    includeInvoiceEmailReceipts,
    issueInvoiceAfterEmail,
    isInvoiceEditorOpen,
    issuedInvoiceCount: invoices.filter((invoice) => invoice.status === 'Issued').length,
    isInvoiceLoading,
    isInvoiceEmailReviewLoading,
    loadPaidIncomeSummary,
    overdueInvoiceCount: invoices.filter((invoice) => invoice.status === 'Overdue').length,
    paidIncomeSummary,
    resetInvoicesWorkspace,
    selectedInvoice,
    setAdjustmentAmount,
    setAdjustmentReason,
    setInvoiceDescription,
    setInvoices,
    setInvoiceStatus,
    setInvoiceEmailReviewMessage,
    setIssueInvoiceAfterEmail,
    setIsInvoiceLoading,
    setInvoiceQuickFilter,
    setSelectedInvoiceId,
    setInvoiceSearchQuery,
    setInvoiceSort,
    startInvoiceEdit,
  }
}
