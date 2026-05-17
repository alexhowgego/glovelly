import { useCallback, useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  handleSessionExpired,
  parseProblemDetails,
} from '../api'
import { defaultGigStatus, emptyGigForm } from '../forms'
import { toGigExpenseForm } from '../formatters'
import type {
  Client,
  Gig,
  GigExpenseForm,
  GigExpenseReimbursementStatus,
  GigForm,
  Invoice,
  InvoiceStatus,
} from '../types'

type UseGigsWorkspaceOptions = {
  clientNamesById: ReadonlyMap<string, string>
  clients: Client[]
  onLinkedInvoiceUpdated: (invoice: Invoice, message: string) => void
  onOpenSection: (section: 'gigs') => void
  onSessionExpired: (message: string) => void
}

type NormalizedGigExpensePayload = {
  sortOrder: number
  description: string
  amount: number
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

function formatEditableNumber(value: number | null) {
  if (value === null || value === 0) {
    return ''
  }

  return Number.isInteger(value) ? String(value) : value.toFixed(2)
}

function shouldCloseAfterSave(event: FormEvent<HTMLFormElement>) {
  const submitter = (event.nativeEvent as SubmitEvent).submitter as
    | HTMLButtonElement
    | null

  return submitter?.dataset.closeAfterSave !== 'false'
}

function toEditableGigForm(gig: Gig): GigForm {
  return {
    clientId: gig.clientId,
    title: gig.title,
    date: gig.date,
    venue: gig.venue,
    fee: String(gig.fee),
    travelMiles: formatEditableNumber(gig.travelMiles),
    passengerCount: formatEditableNumber(gig.passengerCount),
    notes: gig.notes ?? '',
    wasDriving: gig.wasDriving,
    status: gig.status,
    expenses: gig.expenses
      .slice()
      .sort((left, right) => left.sortOrder - right.sortOrder)
      .map(toGigExpenseForm),
  }
}

function toCreateGigForm(clients: Client[]): GigForm {
  return {
    ...emptyGigForm(),
    clientId: clients[0]?.id ?? '',
  }
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
  const [isGigEditorOpen, setIsGigEditorOpen] = useState(false)
  const [gigMode, setGigMode] = useState<'create' | 'edit'>('create')
  const [gigForm, setGigForm] = useState<GigForm>(emptyGigForm)
  const [gigStatus, setGigStatus] = useState(defaultGigStatus)
  const [isGigLoading, setIsGigLoading] = useState(false)
  const [gigExpenseAmount, setGigExpenseAmount] = useState('')
  const [gigExpenseDescription, setGigExpenseDescription] = useState('')
  const [isExpenseStatementOpen, setIsExpenseStatementOpen] = useState(false)
  const [expenseStatementGigIds, setExpenseStatementGigIds] = useState<string[]>([])
  const [expenseStatementExpenseIds, setExpenseStatementExpenseIds] = useState<string[]>([])
  const [includeStatementReceiptAttachments, setIncludeStatementReceiptAttachments] =
    useState(true)
  const [includeStatementReceiptAppendix, setIncludeStatementReceiptAppendix] =
    useState(true)
  const [expenseStatementStatus, setExpenseStatementStatus] = useState('')
  const [expenseStatementPreviewUrl, setExpenseStatementPreviewUrl] =
    useState<string | null>(null)
  const [isExpenseStatementLoading, setIsExpenseStatementLoading] = useState(false)
  const deferredGigSearchQuery = useDeferredValue(gigSearchQuery)

  const clearExpenseStatementPreviewUrl = useCallback(() => {
    setExpenseStatementPreviewUrl((current) => {
      if (current) {
        window.URL.revokeObjectURL(current)
      }

      return null
    })
  }, [])

  const gigsById = useMemo(() => new Map(gigs.map((gig) => [gig.id, gig])), [gigs])

  const filteredGigs = useMemo(() => {
    const query = deferredGigSearchQuery.trim().toLowerCase()
    const sortedGigs = [...gigs].sort((left, right) => {
      const dateComparison = right.date.localeCompare(left.date)
      if (dateComparison !== 0) {
        return dateComparison
      }

      return left.title.localeCompare(right.title)
    })

    if (!query) {
      return sortedGigs
    }

    return sortedGigs.filter((gig) => {
      const clientName = clientNamesById.get(gig.clientId) ?? ''

      return [gig.title, gig.venue, gig.date, gig.status, clientName]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
  }, [clientNamesById, deferredGigSearchQuery, gigs])

  const selectedGig = gigsById.get(selectedGigId) ?? filteredGigs[0] ?? null

  const selectedGigs = useMemo(() => {
    const selectedGigIdSet = new Set(selectedGigIds)

    return gigs
      .filter((gig) => selectedGigIdSet.has(gig.id))
      .sort((left, right) => left.date.localeCompare(right.date))
  }, [gigs, selectedGigIds])

  const expenseStatementGigs = useMemo(() => {
    const statementGigIdSet = new Set(expenseStatementGigIds)

    return gigs
      .filter((gig) => statementGigIdSet.has(gig.id))
      .sort((left, right) => left.date.localeCompare(right.date))
  }, [expenseStatementGigIds, gigs])

  const expenseStatementSelectedExpenses = useMemo(() => {
    const selectedExpenseIdSet = new Set(expenseStatementExpenseIds)
    return expenseStatementGigs.flatMap((gig) =>
      gig.expenses.filter((expense) => selectedExpenseIdSet.has(expense.id))
    )
  }, [expenseStatementExpenseIds, expenseStatementGigs])

  const expenseStatementTotal = expenseStatementSelectedExpenses.reduce(
    (total, expense) => total + expense.amount,
    0
  )

  const expenseStatementReceiptCount = expenseStatementSelectedExpenses.reduce(
    (count, expense) => count + expense.attachments.length,
    0
  )

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

  useEffect(() => clearExpenseStatementPreviewUrl, [clearExpenseStatementPreviewUrl])

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
    setIsGigEditorOpen(false)
    setGigMode('create')
    setGigForm(emptyGigForm())
    setGigStatus(defaultGigStatus)
    setIsGigLoading(false)
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    setIsExpenseStatementOpen(false)
    setExpenseStatementGigIds([])
    setExpenseStatementExpenseIds([])
    setExpenseStatementStatus('')
    clearExpenseStatementPreviewUrl()
    setIsExpenseStatementLoading(false)
  }, [clearExpenseStatementPreviewUrl])

  const mergeSavedGig = useCallback((savedGig: Gig) => {
    setGigs((current) => current.map((gig) => (gig.id === savedGig.id ? savedGig : gig)))
    setGigForm((current) => ({
      ...current,
      expenses: savedGig.expenses
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder)
        .map(toGigExpenseForm),
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
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to upload receipt.')
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
        {
          method: 'PATCH',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            expenseIds: [expense.id],
            status,
            reimbursedAt,
            method,
            note,
            linkedInvoiceId: null,
          }),
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
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to update reimbursement.')
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
      const problem = await parseProblemDetails(redraftResponse)
      const validationMessages = problem?.errors
        ? Object.values(problem.errors).flat().join(' ')
        : problem?.detail ?? problem?.title

      throw new Error(validationMessages || 'Unable to regenerate draft invoice.')
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
      {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          status: 'Cancelled',
        }),
      }
    )

    if (!cancelResponse.ok) {
      const problem = await parseProblemDetails(cancelResponse)
      const validationMessages = problem?.errors
        ? Object.values(problem.errors).flat().join(' ')
        : problem?.detail ?? problem?.title

      throw new Error(validationMessages || 'Unable to cancel linked invoice.')
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

      const response = await fetchWithSession(endpoint, {
        method: isEdit ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
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
        }),
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
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to save gig.')
      }

      const savedGig = (await response.json()) as Gig

      setGigs((current) => {
        if (isEdit) {
          return current.map((gig) => (gig.id === savedGig.id ? savedGig : gig))
        }

        return [savedGig, ...current]
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

  const openExpenseStatement = async () => {
    const targetGigs = selectedGigs.length > 0 ? selectedGigs : selectedGig ? [selectedGig] : []
    const clientIds = new Set(targetGigs.map((gig) => gig.clientId))

    if (targetGigs.length === 0) {
      setGigStatus('Select a gig before generating an expense statement.')
      return
    }

    if (clientIds.size > 1) {
      setGigStatus('Expense statements can only include gigs for one client.')
      return
    }

    if (!targetGigs.some((gig) => gig.expenses.length > 0)) {
      setGigStatus('Add expenses before generating an expense statement.')
      return
    }

    const defaultExpenseIds = targetGigs.flatMap((gig) =>
      gig.expenses
        .filter((expense) => expense.reimbursementStatus === 'Unreimbursed')
        .map((expense) => expense.id)
    )

    setExpenseStatementGigIds(targetGigs.map((gig) => gig.id))
    setExpenseStatementExpenseIds(defaultExpenseIds)
    setIncludeStatementReceiptAttachments(true)
    setIncludeStatementReceiptAppendix(true)
    clearExpenseStatementPreviewUrl()
    setExpenseStatementStatus(
      defaultExpenseIds.length > 0
        ? 'Review expenses before downloading the PDF.'
        : 'All expenses are reimbursed or not claimable. Include at least one to download a statement.'
    )
    setIsExpenseStatementOpen(true)
  }

  const closeExpenseStatement = () => {
    setIsExpenseStatementOpen(false)
    setExpenseStatementStatus('')
    clearExpenseStatementPreviewUrl()
  }

  const toggleExpenseStatementExpense = (expenseId: string) => {
    setExpenseStatementExpenseIds((current) =>
      current.includes(expenseId)
        ? current.filter((value) => value !== expenseId)
        : [...current, expenseId]
    )
    clearExpenseStatementPreviewUrl()
  }

  const updateIncludeStatementReceiptAttachments = (value: boolean) => {
    setIncludeStatementReceiptAttachments(value)
    if (!value) {
      setIncludeStatementReceiptAppendix(false)
    }
    clearExpenseStatementPreviewUrl()
  }

  const updateIncludeStatementReceiptAppendix = (value: boolean) => {
    setIncludeStatementReceiptAppendix(value)
    clearExpenseStatementPreviewUrl()
  }

  const buildExpenseStatementPayload = () => {
    const clientId = expenseStatementGigs[0]?.clientId ?? ''

    return {
      clientId,
      gigIds: expenseStatementGigIds,
      expenseIds: expenseStatementExpenseIds,
      includeReceiptAttachments: includeStatementReceiptAttachments,
      includeReceiptAppendix:
        includeStatementReceiptAttachments && includeStatementReceiptAppendix,
      includeReimbursedExpenses: true,
    }
  }

  const previewExpenseStatement = async () => {
    if (expenseStatementGigs.length === 0 || expenseStatementExpenseIds.length === 0) {
      clearExpenseStatementPreviewUrl()
      setExpenseStatementStatus('Select at least one expense to preview the statement.')
      return null
    }

    setIsExpenseStatementLoading(true)
    setExpenseStatementStatus('Preparing PDF preview...')

    try {
      const response = await fetchWithSession(buildApiUrl('/expense-statements/pdf'), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(buildExpenseStatementPayload()),
      })

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
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to preview the expense statement PDF.')
      }

      const blob = await response.blob()
      const previewUrl = window.URL.createObjectURL(blob)
      setExpenseStatementPreviewUrl((current) => {
        if (current) {
          window.URL.revokeObjectURL(current)
        }

        return previewUrl
      })
      setExpenseStatementStatus('PDF preview ready.')
      return previewUrl
    } catch (error) {
      clearExpenseStatementPreviewUrl()
      setExpenseStatementStatus(
        error instanceof Error ? error.message : 'Unable to preview the expense statement PDF.'
      )
      return null
    } finally {
      setIsExpenseStatementLoading(false)
    }
  }

  const downloadExpenseStatementPdf = async () => {
    if (expenseStatementGigs.length === 0 || expenseStatementExpenseIds.length === 0) {
      setExpenseStatementStatus('Select at least one expense to download a statement.')
      return
    }

    const clientName =
      clientNamesById.get(expenseStatementGigs[0].clientId) ?? 'Client'
    const fallbackFilename = `Expense-Statement-${clientName}.pdf`
    setIsExpenseStatementLoading(true)
    setExpenseStatementStatus('Preparing expense statement PDF...')

    try {
      const response = await fetchWithSession(buildApiUrl('/expense-statements/pdf'), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(buildExpenseStatementPayload()),
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
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to download the expense statement PDF.')
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
      setExpenseStatementStatus(`Downloaded ${link.download}.`)
      setGigStatus(`Downloaded ${link.download}.`)
    } catch (error) {
      setExpenseStatementStatus(
        error instanceof Error
          ? error.message
          : 'Unable to download the expense statement PDF.'
      )
    } finally {
      setIsExpenseStatementLoading(false)
    }
  }

  return {
    applyGigs,
    closeGigEditor,
    completedGigCount: gigs.filter((gig) => gig.status === 'Completed').length,
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
    gigSearchQuery,
    gigStatus,
    gigs,
    gigsById,
    handleAddGigExpense,
    handleGigSubmit,
    handleToggleGigSelection,
    isGigEditorOpen,
    isExpenseStatementLoading,
    isExpenseStatementOpen,
    isGigLoading,
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
    setGigSearchQuery,
    setGigStatus,
    setIncludeStatementReceiptAppendix: updateIncludeStatementReceiptAppendix,
    setIncludeStatementReceiptAttachments: updateIncludeStatementReceiptAttachments,
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

function formatReimbursementStatus(status: GigExpenseReimbursementStatus) {
  switch (status) {
    case 'Unreimbursed':
      return 'Claimable'
    case 'NotClaimable':
      return 'Not claimable'
    default:
      return status
  }
}

function canCancelInvoice(status: InvoiceStatus) {
  return status === 'Draft' || status === 'Issued' || status === 'Overdue'
}

function hasInvoiceRelevantGigChanges(
  gig: Gig,
  payload: {
    clientId: string
    title: string
    date: string
    venue: string
    fee: string
    notes: string
    wasDriving: boolean
    travelMiles: string
    passengerCount: string
    status: Gig['status']
    expenses: GigExpenseForm[]
  },
  fee: number,
  normalizedExpenses: NormalizedGigExpensePayload[]
) {
  if (
    gig.clientId !== payload.clientId ||
    gig.title !== payload.title ||
    gig.date !== payload.date ||
    gig.venue !== payload.venue ||
    gig.fee !== fee ||
    gig.travelMiles !== (payload.travelMiles ? Number(payload.travelMiles) : 0) ||
    (gig.passengerCount ?? 0) !==
      (payload.passengerCount ? Number(payload.passengerCount) : 0) ||
    (gig.notes ?? '') !== (payload.notes || '') ||
    gig.wasDriving !== payload.wasDriving
  ) {
    return true
  }

  const currentExpenses = gig.expenses
    .slice()
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((expense, index) => ({
      sortOrder: index + 1,
      description: expense.description,
      amount: expense.amount,
    }))

  if (currentExpenses.length !== normalizedExpenses.length) {
    return true
  }

  return currentExpenses.some((expense, index) => {
    const nextExpense = normalizedExpenses[index]
    return (
      expense.sortOrder !== nextExpense.sortOrder ||
      expense.description !== nextExpense.description ||
      expense.amount !== nextExpense.amount
    )
  })
}
