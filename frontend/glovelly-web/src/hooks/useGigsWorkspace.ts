import { useCallback, useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
} from '../api'
import { defaultGigStatus, emptyGigForm } from '../forms'
import {
  canCancelInvoice,
  formatEditableNumber,
  formatReimbursementStatus,
  hasInvoiceRelevantGigChanges,
  shouldCloseAfterSave,
  toEditableGigExpenses,
  toCreateGigForm,
  toEditableGigForm,
} from './gigWorkspaceHelpers'
import type { NormalizedGigExpensePayload } from './gigWorkspaceHelpers'
import type {
  Client,
  Gig,
  GigExpenseForm,
  GigExpenseReimbursementStatus,
  GigForm,
  GigQuickFilter,
  GigSort,
  Invoice,
} from '../types'
import { useExpenseStatementWorkspace } from './useExpenseStatementWorkspace'

type UseGigsWorkspaceOptions = {
  clientNamesById: ReadonlyMap<string, string>
  clients: Client[]
  onLinkedInvoiceUpdated: (invoice: Invoice, message: string) => void
  onOpenSection: (section: 'gigs') => void
  onSessionExpired: (message: string) => void
}

type MileageEstimateResponse = {
  distanceMiles: number
  distanceMeters: number
  durationSeconds: number | null
  roundTrip: boolean
  originLabel: string
  destinationLabel: string
  provider: string
  calculatedAtUtc: string
}

export function useGigsWorkspace({
  clientNamesById,
  clients,
  onLinkedInvoiceUpdated,
  onOpenSection,
  onSessionExpired,
}: UseGigsWorkspaceOptions) {
  const [gigs, setGigs] = useState<Gig[]>([])
  const [selectedGigId, setSelectedGigId] = useState<string>('')
  const [selectedGigIds, setSelectedGigIds] = useState<string[]>([])
  const [gigSearchQuery, setGigSearchQuery] = useState('')
  const [gigQuickFilter, setGigQuickFilter] = useState<GigQuickFilter>('all')
  const [gigSort, setGigSort] = useState<GigSort>({ key: 'priority', direction: 'asc' })
  const [isGigEditorOpen, setIsGigEditorOpen] = useState(false)
  const [gigMode, setGigMode] = useState<'create' | 'edit'>('create')
  const [gigForm, setGigForm] = useState<GigForm>(emptyGigForm)
  const [gigStatus, setGigStatus] = useState(defaultGigStatus)
  const [isGigLoading, setIsGigLoading] = useState(false)
  const [isMileageEstimating, setIsMileageEstimating] = useState(false)
  const [gigExpenseAmount, setGigExpenseAmount] = useState('')
  const [gigExpenseDescription, setGigExpenseDescription] = useState('')
  const deferredGigSearchQuery = useDeferredValue(gigSearchQuery)

  const gigsById = useMemo(() => new Map(gigs.map((gig) => [gig.id, gig])), [gigs])

  const filteredGigs = useMemo(() => {
    const query = deferredGigSearchQuery.trim().toLowerCase()
    const today = new Date().toISOString().slice(0, 10)
    const sortDirection = gigSort.direction === 'asc' ? 1 : -1
    const compareText = (left: string, right: string) => left.localeCompare(right)
    const compareNumber = (left: number, right: number) => left - right
    const getClientName = (gig: Gig) => clientNamesById.get(gig.clientId) ?? ''
    const getPriorityBucket = (gig: Gig) => {
      if (gig.status === 'Cancelled') {
        return 5
      }

      if (gig.status === 'Confirmed' && gig.date >= today) {
        return 0
      }

      if (gig.status === 'Completed' && !gig.isInvoiced && gig.date <= today) {
        return 1
      }

      if (gig.status === 'Confirmed' && !gig.isInvoiced && gig.date < today) {
        return 2
      }

      if (gig.status === 'Draft') {
        return 3
      }

      return 4
    }
    const comparePriority = (left: Gig, right: Gig) => {
      const bucketComparison = getPriorityBucket(left) - getPriorityBucket(right)
      if (bucketComparison !== 0) {
        return bucketComparison
      }

      const bucket = getPriorityBucket(left)
      if (bucket === 0) {
        return compareText(left.date, right.date)
      }

      return compareText(right.date, left.date)
    }
    const compareByKey = (left: Gig, right: Gig) => {
      switch (gigSort.key) {
        case 'client':
          return compareText(getClientName(left), getClientName(right))
        case 'fee':
          return compareNumber(left.fee, right.fee)
        case 'status':
          return compareText(left.status, right.status)
        case 'title':
          return compareText(left.title, right.title)
        case 'venue':
          return compareText(left.venue, right.venue)
        case 'priority':
          return comparePriority(left, right)
        case 'date':
        default:
          return compareText(left.date, right.date)
      }
    }
    const sortedGigs = [...gigs].sort((left, right) => {
      const primaryComparison = compareByKey(left, right)
      if (primaryComparison !== 0) {
        return primaryComparison * sortDirection
      }

      const dateComparison = left.date.localeCompare(right.date)
      if (dateComparison !== 0) {
        return dateComparison
      }

      const titleComparison = left.title.localeCompare(right.title)
      if (titleComparison !== 0) {
        return titleComparison
      }

      return left.id.localeCompare(right.id)
    })
    const quickFilteredGigs = sortedGigs.filter((gig) => {
      switch (gigQuickFilter) {
        case 'completed':
          return gig.status === 'Completed'
        case 'drafts':
          return gig.status === 'Draft'
        case 'uninvoiced':
          return !gig.isInvoiced && gig.status !== 'Cancelled'
        case 'upcoming':
          return gig.status !== 'Cancelled' && gig.date >= today
        case 'all':
        default:
          return true
      }
    })

    if (!query) {
      return quickFilteredGigs
    }

    return quickFilteredGigs.filter((gig) => {
      const clientName = clientNamesById.get(gig.clientId) ?? ''

      return [gig.title, gig.venue, gig.date, gig.status, clientName]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
  }, [clientNamesById, deferredGigSearchQuery, gigQuickFilter, gigSort, gigs])

  const selectedGig = gigsById.get(selectedGigId) ?? filteredGigs[0] ?? null

  const selectedGigs = useMemo(() => {
    const selectedGigIdSet = new Set(selectedGigIds)

    return gigs
      .filter((gig) => selectedGigIdSet.has(gig.id))
      .sort((left, right) => left.date.localeCompare(right.date))
  }, [gigs, selectedGigIds])

  const {
    closeExpenseStatement,
    downloadExpenseStatementPdf,
    expenseStatementExpenseIds,
    expenseStatementGigs,
    expenseStatementPreviewUrl,
    expenseStatementReceiptCount,
    expenseStatementStatus,
    expenseStatementTotal,
    includeStatementReceiptAppendix,
    includeStatementReceiptAttachments,
    isExpenseStatementLoading,
    isExpenseStatementOpen,
    openExpenseStatement,
    previewExpenseStatement,
    resetExpenseStatementWorkspace,
    setIncludeStatementReceiptAppendix,
    setIncludeStatementReceiptAttachments,
    toggleExpenseStatementExpense,
  } = useExpenseStatementWorkspace({
    clientNamesById,
    gigs,
    selectedGig,
    selectedGigs,
    onSessionExpired,
    setGigStatus,
  })

  const hasUnsavedGigEditorChanges = () => {
    if (!isGigEditorOpen) {
      return false
    }

    const baseline =
      gigMode === 'edit' && selectedGig
        ? toEditableGigForm(selectedGig)
        : toCreateGigForm(clients)

    return (
      JSON.stringify(gigForm) !== JSON.stringify(baseline) ||
      gigExpenseAmount.trim().length > 0 ||
      gigExpenseDescription.trim().length > 0
    )
  }

  useEffect(() => {
    setSelectedGigIds((current) =>
      current.filter((gigId) => gigs.some((gig) => gig.id === gigId))
    )
  }, [gigs])

  useEffect(() => {
    if (gigForm.clientId || clients.length === 0) {
      return
    }

    setGigForm((current) => ({
      ...current,
      clientId: clients[0]?.id ?? '',
    }))
  }, [clients, gigForm.clientId])

  const applyGigs = useCallback((nextGigs: Gig[]) => {
    setGigs(nextGigs)
    setSelectedGigId(nextGigs[0]?.id ?? '')
  }, [])

  const resetGigsWorkspace = useCallback(() => {
    setGigs([])
    setSelectedGigId('')
    setSelectedGigIds([])
    setGigSearchQuery('')
    setGigQuickFilter('all')
    setGigSort({ key: 'priority', direction: 'asc' })
    setIsGigEditorOpen(false)
    setGigMode('create')
    setGigForm(emptyGigForm())
    setGigStatus(defaultGigStatus)
    setIsGigLoading(false)
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    resetExpenseStatementWorkspace()
  }, [resetExpenseStatementWorkspace])

  const mergeSavedGig = useCallback((savedGig: Gig) => {
    setGigs((current) => [
      savedGig,
      ...current.filter((gig) => gig.id !== savedGig.id),
    ])
    setGigForm((current) => ({
      ...current,
      expenses: toEditableGigExpenses(savedGig),
    }))
  }, [])

  const startGigCreate = () => {
    if (
      hasUnsavedGigEditorChanges() &&
      !window.confirm('Discard unsaved gig changes and add a new gig?')
    ) {
      return
    }

    setGigMode('create')
    setGigForm(toCreateGigForm(clients))
    setGigStatus(
      clients.length > 0
        ? 'Capture the essentials now and we can build invoicing on top later.'
        : 'Create a client first so the gig can be linked correctly.'
    )
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    setSelectedGigIds([])
    setIsGigEditorOpen(true)
  }

  const startGigEdit = () => {
    if (!selectedGig) {
      return
    }

    setGigMode('edit')
    setGigForm(toEditableGigForm(selectedGig))
    setGigStatus('Editing the selected gig.')
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    setIsGigEditorOpen(true)
  }

  const cloneSelectedGig = async () => {
    if (!selectedGig) {
      setGigStatus('Select a gig before cloning it.')
      return
    }

    if (
      hasUnsavedGigEditorChanges() &&
      !window.confirm('Discard unsaved gig changes and clone the selected gig?')
    ) {
      return
    }

    const includeExpenses =
      selectedGig.expenses.length > 0 &&
      window.confirm('Clone this gig with its expenses? Receipts and invoice links will not be copied.')

    setIsGigLoading(true)
    setGigStatus('Cloning selected gig...')

    try {
      const response = await fetchWithSession(
        buildApiUrl('/gigs'),
        jsonRequestInit('POST', {
          clientId: selectedGig.clientId,
          title: selectedGig.title,
          date: selectedGig.date,
          venue: selectedGig.venue,
          fee: selectedGig.fee,
          travelMiles: selectedGig.travelMiles,
          passengerCount: selectedGig.passengerCount,
          notes: selectedGig.notes,
          wasDriving: selectedGig.wasDriving,
          status: selectedGig.status,
          invoiceId: null,
          expenses: includeExpenses
            ? selectedGig.expenses
                .slice()
                .sort((left, right) => left.sortOrder - right.sortOrder)
                .map((expense, index) => ({
                  sortOrder: index + 1,
                  description: expense.description,
                  amount: expense.amount,
                }))
            : [],
          invoicedAt: null,
        })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to clone gig.'))
      }

      const savedGig = (await response.json()) as Gig
      setGigs((current) => [
        savedGig,
        ...current.filter((gig) => gig.id !== savedGig.id),
      ])
      setSelectedGigId(savedGig.id)
      setSelectedGigIds([])
      setGigMode('edit')
      setGigForm(toEditableGigForm(savedGig))
      setGigExpenseAmount('')
      setGigExpenseDescription('')
      setGigStatus('Gig cloned. Update any details before saving.')
      setIsGigEditorOpen(true)
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to clone gig.')
    } finally {
      setIsGigLoading(false)
    }
  }

  const selectGig = (gigId: string) => {
    if (gigId === selectedGig?.id) {
      return
    }

    const nextGig = gigsById.get(gigId)
    if (!nextGig) {
      return
    }

    if (isGigEditorOpen) {
      if (
        hasUnsavedGigEditorChanges() &&
        !window.confirm('Discard unsaved gig changes and edit the selected gig?')
      ) {
        return
      }

      setGigMode('edit')
      setGigForm(toEditableGigForm(nextGig))
      setGigStatus('Editing the selected gig.')
      setGigExpenseAmount('')
      setGigExpenseDescription('')
    }

    setSelectedGigId(gigId)
  }

  const closeGigEditor = () => {
    if (
      hasUnsavedGigEditorChanges() &&
      !window.confirm('Discard unsaved gig changes and close the editor?')
    ) {
      return
    }

    setIsGigEditorOpen(false)
    setGigMode('create')
    setGigForm(toCreateGigForm(clients))
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    setGigStatus(defaultGigStatus)
  }

  const updateGigField = (
    field: keyof GigForm,
    value: string | boolean | GigExpenseForm[]
  ) => {
    setGigForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const estimateGigMileage = async () => {
    if (gigMode !== 'edit' || !selectedGig) {
      setGigStatus('Save the gig before estimating mileage.')
      return
    }

    const destination = gigForm.venue.trim()
    if (!destination) {
      setGigStatus('Add a location before estimating mileage.')
      return
    }

    setIsMileageEstimating(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/mileage-estimate`),
        jsonRequestInit('POST', {
          destination,
          roundTrip: true,
        })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to estimate mileage.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to estimate mileage.')
        )
      }

      const estimate = (await response.json()) as MileageEstimateResponse
      setGigForm((current) => ({
        ...current,
        wasDriving: true,
        travelMiles: formatEditableNumber(estimate.distanceMiles),
      }))
      setGigStatus(
        `Estimated ${formatEditableNumber(estimate.distanceMiles)} miles from ${estimate.originLabel} to ${estimate.destinationLabel}.`
      )
    } catch (error) {
      setGigStatus(
        error instanceof Error
          ? error.message
          : 'Unable to estimate mileage right now.'
      )
    } finally {
      setIsMileageEstimating(false)
    }
  }

  const handleAddGigExpense = () => {
    const description = gigExpenseDescription.trim()
    const amount = Number(gigExpenseAmount)

    if (!description) {
      setGigStatus('Add an expense description before saving it to the gig.')
      return
    }

    if (!Number.isFinite(amount) || amount < 0) {
      setGigStatus('Expense amount must be a valid non-negative number.')
      return
    }

    setGigForm((current) => ({
      ...current,
      expenses: [
        ...current.expenses,
        {
          id: '',
          sortOrder: current.expenses.length + 1,
          description,
          amount: gigExpenseAmount,
          reimbursementStatus: 'Unreimbursed',
          reimbursedAt: null,
          reimbursementUpdatedAt: null,
          reimbursementMethod: null,
          reimbursementNote: null,
          attachments: [],
        },
      ],
    }))
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    setGigStatus('Expense added to the gig form. Save the gig to persist it.')
  }

  const updateGigExpenseField = (
    index: number,
    field: keyof Pick<GigExpenseForm, 'description' | 'amount'>,
    value: string
  ) => {
    setGigForm((current) => ({
      ...current,
      expenses: current.expenses.map((expense, expenseIndex) =>
        expenseIndex === index
          ? {
              ...expense,
              [field]: value,
            }
          : expense
      ),
    }))
  }

  const removeGigExpense = (index: number) => {
    setGigForm((current) => ({
      ...current,
      expenses: current.expenses
        .filter((_, expenseIndex) => expenseIndex !== index)
        .map((expense, expenseIndex) => ({
          ...expense,
          sortOrder: expenseIndex + 1,
        })),
    }))
  }

  const refreshGig = async (gigId: string) => {
    const response = await fetchWithSession(buildApiUrl(`/gigs/${gigId}`))

    if (
      handleSessionExpired(
        response,
        onSessionExpired,
        'Your session expired. Sign in again to keep managing gigs.'
      )
    ) {
      return null
    }

    if (!response.ok) {
      throw new Error('Unable to refresh gig receipts.')
    }

    const savedGig = (await response.json()) as Gig
    mergeSavedGig(savedGig)
    return savedGig
  }

  const uploadExpenseAttachment = async (index: number, file: File) => {
    const expense = gigForm.expenses[index]
    if (!selectedGig || !expense?.id) {
      setGigStatus('Save the gig before adding receipts.')
      return
    }

    const formData = new FormData()
    formData.append('file', file)
    setIsGigLoading(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/expenses/${expense.id}/attachments`),
        {
          method: 'POST',
          body: formData,
        }
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to upload receipt.'))
      }

      await refreshGig(selectedGig.id)
      setGigStatus('Receipt uploaded.')
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to upload receipt.')
    } finally {
      setIsGigLoading(false)
    }
  }

  const downloadExpenseAttachment = (expense: GigExpenseForm, attachmentId: string) => {
    if (!selectedGig || !expense.id) {
      return
    }

    window.open(
      buildApiUrl(`/gigs/${selectedGig.id}/expenses/${expense.id}/attachments/${attachmentId}`),
      '_blank',
      'noopener,noreferrer'
    )
  }

  const deleteExpenseAttachment = async (
    expense: GigExpenseForm,
    attachmentId: string
  ) => {
    if (!selectedGig || !expense.id) {
      return
    }

    setIsGigLoading(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/expenses/${expense.id}/attachments/${attachmentId}`),
        {
          method: 'DELETE',
        }
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error('Unable to delete receipt.')
      }

      await refreshGig(selectedGig.id)
      setGigStatus('Receipt deleted.')
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to delete receipt.')
    } finally {
      setIsGigLoading(false)
    }
  }

  const deleteGig = async () => {
    if (!selectedGig) {
      return
    }

    if (selectedGig.status !== 'Confirmed') {
      setGigStatus('Only planned gigs can be deleted.')
      return
    }

    if (selectedGig.isInvoiced) {
      setGigStatus('Gigs with linked invoices cannot be deleted.')
      return
    }

    if (
      !window.confirm(
        `Delete ${selectedGig.title}? This cannot be undone.`
      )
    ) {
      return
    }

    setIsGigLoading(true)

    try {
      const response = await fetchWithSession(buildApiUrl(`/gigs/${selectedGig.id}`), {
        method: 'DELETE',
      })

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to delete gig.'))
      }

      const nextGigs = gigs.filter((gig) => gig.id !== selectedGig.id)
      setGigs(nextGigs)
      setSelectedGigId(nextGigs[0]?.id ?? '')
      setSelectedGigIds((current) => current.filter((gigId) => gigId !== selectedGig.id))
      setIsGigEditorOpen(false)
      setGigMode('create')
      setGigForm(toCreateGigForm(clients))
      setGigExpenseAmount('')
      setGigExpenseDescription('')
      setGigStatus('Gig deleted.')
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to delete gig.')
    } finally {
      setIsGigLoading(false)
    }
  }

  const updateExpenseReimbursement = async (
    expense: GigExpenseForm,
    status: GigExpenseReimbursementStatus
  ) => {
    if (!selectedGig || !expense.id) {
      setGigStatus('Save the gig before updating reimbursement.')
      return
    }

    if (status === expense.reimbursementStatus) {
      return
    }

    let reimbursedAt: string | null = null
    let method: string | null = null
    let note: string | null = null

    if (status === 'Reimbursed') {
      const dateValue = window.prompt(
        'Reimbursed date',
        new Date().toISOString().slice(0, 10)
      )
      if (!dateValue) {
        return
      }

      const noteValue = window.prompt('Method or note', expense.reimbursementMethod ?? '')
      if (!noteValue?.trim()) {
        setGigStatus('Add a reimbursement method or note.')
        return
      }

      reimbursedAt = `${dateValue}T00:00:00.000Z`
      method = noteValue.trim()
      note = noteValue.trim()
    }

    setIsGigLoading(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/expenses/reimbursement`),
        jsonRequestInit('PATCH', {
          expenseIds: [expense.id],
          status,
          reimbursedAt,
          method,
          note,
          linkedInvoiceId: null,
        })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to update reimbursement.')
        )
      }

      const savedGig = (await response.json()) as Gig
      mergeSavedGig(savedGig)
      setGigStatus(`Expense marked as ${formatReimbursementStatus(status).toLowerCase()}.`)
      await handleLinkedInvoiceAfterGigSave(selectedGig, savedGig, true)
    } catch (error) {
      setGigStatus(
        error instanceof Error ? error.message : 'Unable to update reimbursement.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  const openGigReceiptDraft = (savedGig: Gig) => {
    mergeSavedGig(savedGig)
    setSelectedGigId(savedGig.id)
    onOpenSection('gigs')
    setGigMode('edit')
    setGigForm(toEditableGigForm(savedGig))
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    setIsGigEditorOpen(true)
  }

  const handleLinkedInvoiceAfterGigSave = async (
    previousGig: Gig,
    savedGig: Gig,
    hasInvoiceRelevantChanges: boolean
  ) => {
    const invoiceId = savedGig.invoiceId ?? previousGig.invoiceId
    if (!invoiceId) {
      return
    }

    const invoiceResponse = await fetchWithSession(buildApiUrl(`/invoices/${invoiceId}`))
    if (
      handleSessionExpired(
        invoiceResponse,
        onSessionExpired,
        'Your session expired. Sign in again to keep managing gigs.'
      )
    ) {
      return
    }

    if (!invoiceResponse.ok) {
      return
    }

    const invoice = (await invoiceResponse.json()) as Invoice

    if (previousGig.status !== 'Cancelled' && savedGig.status === 'Cancelled') {
      await promptToCancelLinkedInvoice(invoice)
      return
    }

    if (!hasInvoiceRelevantChanges || invoice.status !== 'Draft') {
      return
    }

    const shouldRedraft = window.confirm(
      `Regenerate draft invoice ${invoice.invoiceNumber} using the latest gig details?`
    )
    if (!shouldRedraft) {
      return
    }

    const redraftResponse = await fetchWithSession(
      buildApiUrl(`/invoices/${invoice.id}/redraft`),
      {
        method: 'POST',
      }
    )

    if (!redraftResponse.ok) {
      throw new Error(
        await getResponseErrorMessage(
          redraftResponse,
          'Unable to regenerate draft invoice.'
        )
      )
    }

    const redraftedInvoice = (await redraftResponse.json()) as Invoice
    onLinkedInvoiceUpdated(
      redraftedInvoice,
      `Draft invoice ${redraftedInvoice.invoiceNumber} regenerated from updated gig details.`
    )
    setGigStatus(`Gig updated. Draft invoice ${redraftedInvoice.invoiceNumber} regenerated.`)
  }

  const promptToCancelLinkedInvoice = async (invoice: Invoice) => {
    if (!canCancelInvoice(invoice.status)) {
      return
    }

    const shouldCancel = window.confirm(
      `Cancel linked invoice ${invoice.invoiceNumber} as well?`
    )
    if (!shouldCancel) {
      return
    }

    const cancelResponse = await fetchWithSession(
      buildApiUrl(`/invoices/${invoice.id}/status`),
      jsonRequestInit('PUT', {
        status: 'Cancelled',
      })
    )

    if (!cancelResponse.ok) {
      throw new Error(
        await getResponseErrorMessage(cancelResponse, 'Unable to cancel linked invoice.')
      )
    }

    const cancelledInvoice = (await cancelResponse.json()) as Invoice
    onLinkedInvoiceUpdated(
      cancelledInvoice,
      `Linked invoice ${cancelledInvoice.invoiceNumber} cancelled.`
    )
    setGigStatus(`Gig updated. Linked invoice ${cancelledInvoice.invoiceNumber} cancelled.`)
  }

  const handleGigSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const closeAfterSave = shouldCloseAfterSave(event)

    const payload = {
      clientId: gigForm.clientId,
      title: gigForm.title.trim(),
      date: gigForm.date,
      venue: gigForm.venue.trim(),
      fee: gigForm.fee.trim(),
      notes: gigForm.notes.trim(),
      wasDriving: gigForm.wasDriving,
      travelMiles: gigForm.travelMiles.trim(),
      passengerCount: gigForm.passengerCount.trim(),
      status: gigForm.status,
      expenses: gigForm.expenses,
    }

    if (!payload.clientId || !payload.title || !payload.date || !payload.venue) {
      setGigStatus('Client, title, date and location are required.')
      return
    }

    const fee = Number(payload.fee)
    if (!Number.isFinite(fee) || fee < 0) {
      setGigStatus('Fee must be a valid non-negative number.')
      return
    }

    const travelMiles = payload.travelMiles ? Number(payload.travelMiles) : 0
    if (!Number.isFinite(travelMiles) || travelMiles < 0) {
      setGigStatus('Travel miles must be a valid non-negative number.')
      return
    }

    const passengerCount = payload.passengerCount ? Number(payload.passengerCount) : 0
    if (
      !Number.isInteger(passengerCount) ||
      passengerCount < 0
    ) {
      setGigStatus('Passenger count must be a valid whole number.')
      return
    }

    const normalizedExpenses: NormalizedGigExpensePayload[] = []
    for (const [index, expense] of payload.expenses.entries()) {
      const description = expense.description.trim()
      const amount = Number(expense.amount)

      if (!description) {
        setGigStatus(`Expense ${index + 1} needs a description.`)
        return
      }

      if (!Number.isFinite(amount) || amount < 0) {
        setGigStatus(`Expense ${index + 1} must have a valid non-negative amount.`)
        return
      }

      normalizedExpenses.push({
        sortOrder: index + 1,
        description,
        amount,
      })
    }

    setIsGigLoading(true)

    try {
      const isEdit = gigMode === 'edit' && selectedGig
      const previousGig = isEdit ? selectedGig : null
      const hasInvoiceRelevantChanges = previousGig
        ? hasInvoiceRelevantGigChanges(previousGig, payload, fee, normalizedExpenses)
        : false
      const endpoint = isEdit
        ? buildApiUrl(`/gigs/${selectedGig.id}`)
        : buildApiUrl('/gigs')

      const response = await fetchWithSession(
        endpoint,
        jsonRequestInit(isEdit ? 'PUT' : 'POST', {
          clientId: payload.clientId,
          title: payload.title,
          date: payload.date,
          venue: payload.venue,
          fee,
          travelMiles,
          passengerCount: passengerCount === 0 ? null : passengerCount,
          notes: payload.notes || null,
          wasDriving: payload.wasDriving,
          status: payload.status,
          invoiceId: null,
          expenses: normalizedExpenses,
          invoicedAt: null,
        })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to save gig.'))
      }

      const savedGig = (await response.json()) as Gig

      setGigs((current) => {
        if (isEdit) {
          return current.map((gig) => (gig.id === savedGig.id ? savedGig : gig))
        }

        return [
          savedGig,
          ...current.filter((gig) => gig.id !== savedGig.id),
        ]
      })

      setSelectedGigId(savedGig.id)
      setGigMode('edit')
      setGigForm(toEditableGigForm(savedGig))
      setGigExpenseAmount('')
      setGigExpenseDescription('')
      setGigStatus(isEdit ? 'Gig updated.' : 'Gig created.')
      setIsGigEditorOpen(!closeAfterSave)
      if (previousGig) {
        await handleLinkedInvoiceAfterGigSave(
          previousGig,
          savedGig,
          hasInvoiceRelevantChanges
        )
      }
    } catch (error) {
      setGigStatus(
        error instanceof Error ? error.message : 'Unable to save this gig right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  const handleToggleGigSelection = (gigId: string) => {
    const gig = gigsById.get(gigId)
    if (!gig) {
      return
    }

    setSelectedGigIds((current) => {
      if (current.includes(gigId)) {
        return current.filter((value) => value !== gigId)
      }

      const selectedClientId = current
        .map((value) => gigsById.get(value)?.clientId)
        .find((value): value is string => Boolean(value))

      if (selectedClientId && selectedClientId !== gig.clientId) {
        setGigStatus('Select gigs for one client at a time.')
        return current
      }

      return [...current, gigId]
    })
  }

  return {
    applyGigs,
    cloneSelectedGig,
    closeGigEditor,
    completedGigCount: gigs.filter((gig) => gig.status === 'Completed').length,
    deleteGig,
    deleteExpenseAttachment,
    downloadExpenseAttachment,
    filteredGigs,
    closeExpenseStatement,
    downloadExpenseStatementPdf,
    expenseStatementExpenseIds,
    expenseStatementGigs,
    expenseStatementPreviewUrl,
    expenseStatementReceiptCount,
    expenseStatementStatus,
    expenseStatementTotal,
    gigExpenseAmount,
    gigExpenseDescription,
    gigForm,
    gigMode,
    gigQuickFilter,
    gigSearchQuery,
    gigSort,
    gigStatus,
    gigs,
    gigsById,
    estimateGigMileage,
    handleAddGigExpense,
    handleGigSubmit,
    handleToggleGigSelection,
    isGigEditorOpen,
    isExpenseStatementLoading,
    isExpenseStatementOpen,
    isGigLoading,
    isMileageEstimating,
    mergeSavedGig,
    openGigReceiptDraft,
    openExpenseStatement,
    plannedGigCount: gigs.filter((gig) => gig.status === 'Confirmed').length,
    previewExpenseStatement,
    removeGigExpense,
    resetGigsWorkspace,
    selectedGig,
    selectedGigIds,
    selectedGigs,
    selectGig,
    setGigExpenseAmount,
    setGigExpenseDescription,
    setGigs,
    setGigQuickFilter,
    setGigSearchQuery,
    setGigSort,
    setGigStatus,
    setIncludeStatementReceiptAppendix,
    setIncludeStatementReceiptAttachments,
    setSelectedGigId,
    setSelectedGigIds,
    startGigCreate,
    startGigEdit,
    uninvoicedGigCount: gigs.filter((gig) => !gig.isInvoiced && gig.status !== 'Cancelled').length,
    upcomingGigCount: gigs.filter((gig) => gig.date >= new Date().toISOString().slice(0, 10)).length,
    updateGigExpenseField,
    updateGigField,
    updateExpenseReimbursement,
    uploadExpenseAttachment,
    includeStatementReceiptAppendix,
    includeStatementReceiptAttachments,
    toggleExpenseStatementExpense,
  }
}
