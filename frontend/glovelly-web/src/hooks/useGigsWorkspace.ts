import { useCallback, useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  defaultGigStatus,
  emptyGigForm,
  fetchWithSession,
  parseProblemDetails,
  toGigExpenseForm,
} from '../appShared'
import type { Client, Gig, GigExpenseForm, GigForm } from '../appShared'

type UseGigsWorkspaceOptions = {
  clientNamesById: ReadonlyMap<string, string>
  clients: Client[]
  onOpenSection: (section: 'gigs') => void
  onSessionExpired: (message: string) => void
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
    notes: gig.notes ?? '',
    wasDriving: gig.wasDriving,
    status: gig.status,
    expenses: gig.expenses
      .slice()
      .sort((left, right) => left.sortOrder - right.sortOrder)
      .map(toGigExpenseForm),
  }
}

export function useGigsWorkspace({
  clientNamesById,
  clients,
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
  const deferredGigSearchQuery = useDeferredValue(gigSearchQuery)

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
    setIsGigEditorOpen(false)
    setGigMode('create')
    setGigForm(emptyGigForm())
    setGigStatus(defaultGigStatus)
    setIsGigLoading(false)
    setGigExpenseAmount('')
    setGigExpenseDescription('')
  }, [])

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
    setGigMode('create')
    setGigForm({
      ...emptyGigForm(),
      clientId: clients[0]?.id ?? '',
    })
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

  const closeGigEditor = () => {
    setIsGigEditorOpen(false)
    setGigMode('create')
    setGigForm({
      ...emptyGigForm(),
      clientId: clients[0]?.id ?? '',
    })
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

    if (response.status === 401) {
      onSessionExpired('Your session expired. Sign in again to keep managing gigs.')
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

      if (response.status === 401) {
        onSessionExpired('Your session expired. Sign in again to keep managing gigs.')
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

      if (response.status === 401) {
        onSessionExpired('Your session expired. Sign in again to keep managing gigs.')
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

    const normalizedExpenses = []
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
          travelMiles: 0,
          passengerCount: null,
          notes: payload.notes || null,
          wasDriving: payload.wasDriving,
          status: payload.status,
          invoiceId: null,
          expenses: normalizedExpenses,
          invoicedAt: null,
        }),
      })

      if (response.status === 401) {
        onSessionExpired('Your session expired. Sign in again to keep managing gigs.')
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
    } catch (error) {
      setGigStatus(
        error instanceof Error ? error.message : 'Unable to save this gig right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  const handleToggleGigSelection = (gigId: string) => {
    setSelectedGigIds((current) =>
      current.includes(gigId)
        ? current.filter((value) => value !== gigId)
        : [...current, gigId]
    )
  }

  return {
    applyGigs,
    closeGigEditor,
    completedGigCount: gigs.filter((gig) => gig.status === 'Completed').length,
    deleteExpenseAttachment,
    downloadExpenseAttachment,
    filteredGigs,
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
    isGigLoading,
    mergeSavedGig,
    openGigReceiptDraft,
    plannedGigCount: gigs.filter((gig) => gig.status === 'Confirmed').length,
    removeGigExpense,
    resetGigsWorkspace,
    selectedGig,
    selectedGigIds,
    selectedGigs,
    setGigExpenseAmount,
    setGigExpenseDescription,
    setGigs,
    setGigSearchQuery,
    setGigStatus,
    setSelectedGigId,
    setSelectedGigIds,
    startGigCreate,
    startGigEdit,
    uninvoicedGigCount: gigs.filter((gig) => !gig.isInvoiced && gig.status !== 'Cancelled').length,
    upcomingGigCount: gigs.filter((gig) => gig.date >= new Date().toISOString().slice(0, 10)).length,
    updateGigExpenseField,
    updateGigField,
    uploadExpenseAttachment,
  }
}
