import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  downloadResponseBlob,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
} from '../api'
import { defaultGigStatus, emptyGigExternalResourceForm, emptyGigForm } from '../forms'
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
import {
  getGigReveal,
  getLocalDate,
  getVisibleGigs,
  reconcileSelectedGigId,
} from './gigListState'
import type {
  Client,
  Gig,
  GigExternalResource,
  GigExternalResourceAttachment,
  GigExternalResourceForm,
  GigExpenseForm,
  GigExpenseReimbursementStatus,
  GigForm,
  GigQuickFilter,
  GigSort,
  GigType,
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
  const [gigTypeFilter, setGigTypeFilter] = useState<GigType | 'all'>('all')
  const [showPastGigs, setShowPastGigs] = useState(false)
  const [gigSort, setGigSort] = useState<GigSort>({ key: 'priority', direction: 'asc' })
  const [isGigEditorOpen, setIsGigEditorOpen] = useState(false)
  const [gigMode, setGigMode] = useState<'create' | 'edit'>('create')
  const [gigForm, setGigForm] = useState<GigForm>(emptyGigForm)
  const [gigStatus, setGigStatus] = useState(defaultGigStatus)
  const [isGigLoading, setIsGigLoading] = useState(false)
  const [isMileageEstimating, setIsMileageEstimating] = useState(false)
  const [isExternalResourceEditorOpen, setIsExternalResourceEditorOpen] = useState(false)
  const [externalResourceMode, setExternalResourceMode] = useState<'create' | 'edit'>('create')
  const [editingExternalResourceId, setEditingExternalResourceId] = useState<string>('')
  const [externalResourceForm, setExternalResourceForm] = useState<GigExternalResourceForm>(
    emptyGigExternalResourceForm()
  )
  const gigsById = useMemo(() => new Map(gigs.map((gig) => [gig.id, gig])), [gigs])

  const today = getLocalDate()
  const filteredGigs = useMemo(() => getVisibleGigs(gigs, clientNamesById, {
    searchQuery: gigSearchQuery,
    quickFilter: gigQuickFilter,
    showPastGigs,
    sort: gigSort,
    typeFilter: gigTypeFilter,
  }, today), [clientNamesById, gigQuickFilter, gigSearchQuery, gigSort, gigTypeFilter, gigs, showPastGigs, today])
  const reconciledSelectedGigId = reconcileSelectedGigId(selectedGigId, filteredGigs)
  const selectedGig = isGigEditorOpen
    ? (gigsById.get(selectedGigId) ?? null)
    : (filteredGigs.find((gig) => gig.id === reconciledSelectedGigId) ?? null)

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
      JSON.stringify(gigForm) !== JSON.stringify(baseline)
    )
  }

  useEffect(() => {
    setSelectedGigIds((current) =>
      current.filter((gigId) => gigs.some((gig) => gig.id === gigId))
    )
  }, [gigs])

  useEffect(() => {
    if (isGigEditorOpen) {
      return
    }

    if (reconciledSelectedGigId !== selectedGigId) {
      setSelectedGigId(reconciledSelectedGigId)
    }
  }, [isGigEditorOpen, reconciledSelectedGigId, selectedGigId])

  useEffect(() => {
    if (gigForm.clientId || clients.length === 0) {
      return
    }

    setGigForm((current) => ({
      ...current,
      clientId: clients[0]?.id ?? '',
    }))
  }, [clients, gigForm.clientId])

  useEffect(() => {
    if (isGigEditorOpen || !selectedGig) {
      return
    }

    setGigMode('edit')
    setGigForm(toEditableGigForm(selectedGig))
  }, [isGigEditorOpen, selectedGig])

  const applyGigs = useCallback((nextGigs: Gig[]) => {
    setGigs(nextGigs)
  }, [])

  const resetGigsWorkspace = useCallback(() => {
    setGigs([])
    setSelectedGigId('')
    setSelectedGigIds([])
    setGigSearchQuery('')
    setGigQuickFilter('all')
    setShowPastGigs(false)
    setGigSort({ key: 'priority', direction: 'asc' })
    setIsGigEditorOpen(false)
    setGigMode('create')
    setGigForm(emptyGigForm())
    setGigStatus(defaultGigStatus)
    setIsGigLoading(false)
    setIsExternalResourceEditorOpen(false)
    setExternalResourceMode('create')
    setEditingExternalResourceId('')
    setExternalResourceForm(emptyGigExternalResourceForm())
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

  const replaceSavedGig = useCallback((savedGig: Gig) => {
    setGigs((current) => current.map((gig) => (gig.id === savedGig.id ? savedGig : gig)))
  }, [])

  const startExternalResourceCreate = () => {
    if (!selectedGig) {
      setGigStatus('Select a gig before adding an attachment.')
      return
    }

    setExternalResourceMode('create')
    setEditingExternalResourceId('')
    setExternalResourceForm(emptyGigExternalResourceForm())
    setIsExternalResourceEditorOpen(true)
  }

  const startExternalResourceEdit = (resource: GigExternalResource) => {
    setExternalResourceMode('edit')
    setEditingExternalResourceId(resource.id)
    setExternalResourceForm({
      resourceType: resource.resourceType,
      purpose: resource.purpose,
      title: resource.title,
      url: resource.url ?? '',
      notes: resource.notes ?? '',
      isPrimary: resource.isPrimary,
    })
    setIsExternalResourceEditorOpen(true)
  }

  const cancelExternalResourceEdit = () => {
    setIsExternalResourceEditorOpen(false)
    setExternalResourceMode('create')
    setEditingExternalResourceId('')
    setExternalResourceForm(emptyGigExternalResourceForm())
  }

  const updateExternalResourceField = (
    field: keyof GigExternalResourceForm,
    value: string | boolean
  ) => {
    setExternalResourceForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const submitExternalResource = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    if (!selectedGig) {
      setGigStatus('Select a gig before saving an attachment.')
      return
    }

    const title = externalResourceForm.title.trim()
    const url = externalResourceForm.url.trim()
    if (!title) {
      setGigStatus('Attachment title is required.')
      return
    }

    setIsGigLoading(true)
    setGigStatus('Saving attachment...')

    try {
      const isEdit = externalResourceMode === 'edit' && editingExternalResourceId
      const endpoint = isEdit
        ? buildApiUrl(`/gigs/${selectedGig.id}/external-resources/${editingExternalResourceId}`)
        : buildApiUrl(`/gigs/${selectedGig.id}/external-resources`)
      const response = await fetchWithSession(
        endpoint,
        jsonRequestInit(isEdit ? 'PUT' : 'POST', {
          resourceType: externalResourceForm.resourceType,
          purpose: externalResourceForm.purpose,
          title,
          url,
          notes: externalResourceForm.notes.trim() || null,
          isPrimary: externalResourceForm.isPrimary,
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
          await getResponseErrorMessage(response, 'Unable to save attachment.')
        )
      }

      const savedGig = (await response.json()) as Gig
      replaceSavedGig(savedGig)
      setExternalResourceForm(emptyGigExternalResourceForm())
      setEditingExternalResourceId('')
      setExternalResourceMode('create')
      setIsExternalResourceEditorOpen(false)
      setGigStatus(isEdit ? 'Attachment updated.' : 'Attachment added.')
    } catch (error) {
      setGigStatus(
        error instanceof Error
          ? error.message
          : 'Unable to save attachment right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  const deleteExternalResource = async (resource: GigExternalResource) => {
    if (!selectedGig) {
      return
    }

    if (!window.confirm(`Delete attachment ${resource.title}?`)) {
      return
    }

    setIsGigLoading(true)
    setGigStatus('Deleting attachment...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/external-resources/${resource.id}`),
        { method: 'DELETE' }
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
          await getResponseErrorMessage(response, 'Unable to delete attachment.')
        )
      }

      const savedGig = (await response.json()) as Gig
      replaceSavedGig(savedGig)
      if (editingExternalResourceId === resource.id) {
        cancelExternalResourceEdit()
      }
      setGigStatus('Attachment deleted.')
    } catch (error) {
      setGigStatus(
        error instanceof Error
          ? error.message
          : 'Unable to delete attachment right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  const uploadExternalResourceAttachment = async (
    resource: GigExternalResource,
    file: File
  ) => {
    if (!selectedGig) {
      return
    }

    const formData = new FormData()
    formData.append('file', file)
    setIsGigLoading(true)
    setGigStatus('Uploading attachment file...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/external-resources/${resource.id}/attachments`),
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
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to upload attachment file.')
        )
      }

      const savedGig = (await response.json()) as Gig
      replaceSavedGig(savedGig)
      setGigStatus('Attachment file uploaded.')
    } catch (error) {
      setGigStatus(
        error instanceof Error
          ? error.message
          : 'Unable to upload attachment file right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  const downloadExternalResourceAttachment = async (
    resource: GigExternalResource,
    attachment: GigExternalResourceAttachment
  ) => {
    if (!selectedGig) {
      return
    }

    setIsGigLoading(true)
    setGigStatus('Downloading attachment file...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/external-resources/${resource.id}/attachments/${attachment.id}`)
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
          await getResponseErrorMessage(response, 'Unable to download attachment file.')
        )
      }

      const fileName = await downloadResponseBlob(response, attachment.fileName)
      setGigStatus(`Downloaded ${fileName}.`)
    } catch (error) {
      setGigStatus(
        error instanceof Error
          ? error.message
          : 'Unable to download attachment file right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  const deleteExternalResourceAttachment = async (
    resource: GigExternalResource,
    attachment: GigExternalResourceAttachment
  ) => {
    if (!selectedGig) {
      return
    }

    if (!window.confirm(`Delete attachment file ${attachment.fileName}?`)) {
      return
    }

    setIsGigLoading(true)
    setGigStatus('Deleting attachment file...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/external-resources/${resource.id}/attachments/${attachment.id}`),
        { method: 'DELETE' }
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
          await getResponseErrorMessage(response, 'Unable to delete attachment file.')
        )
      }

      const savedGig = (await response.json()) as Gig
      replaceSavedGig(savedGig)
      setGigStatus('Attachment file deleted.')
    } catch (error) {
      setGigStatus(
        error instanceof Error
          ? error.message
          : 'Unable to delete attachment file right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

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
          type: selectedGig.type,
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
      const nextGigs = [
        savedGig,
        ...gigs.filter((gig) => gig.id !== savedGig.id),
      ]
      setGigs(nextGigs)
      setSelectedGigIds([])
      setGigMode('edit')
      setGigForm(toEditableGigForm(savedGig))
      setGigStatus('Gig cloned. Update any details before saving.')
      revealGig(savedGig, nextGigs)
      setIsGigEditorOpen(true)
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to clone gig.')
    } finally {
      setIsGigLoading(false)
    }
  }

  const revealGig = (nextGig: Gig, candidateGigs = gigs) => {
    const visibleGigs = getVisibleGigs(candidateGigs, clientNamesById, {
      searchQuery: gigSearchQuery,
      quickFilter: gigQuickFilter,
      showPastGigs,
      sort: gigSort,
      typeFilter: gigTypeFilter,
    }, today)
    const reveal = getGigReveal(nextGig, visibleGigs, today)

    if (reveal.clearFilters) {
      setGigSearchQuery('')
      setGigQuickFilter('all')
      setGigTypeFilter('all')
      if (reveal.showPastGigs) {
        setShowPastGigs(true)
      }
      setGigStatus(
        reveal.showPastGigs
          ? `Cleared filters and showed past gigs to open ${nextGig.title}.`
          : `Cleared filters to open ${nextGig.title}.`
      )
    }

    setSelectedGigId(nextGig.id)
  }

  const selectGig = (gigId: string) => {
    if (gigId === selectedGig?.id) {
      return true
    }

    const nextGig = gigsById.get(gigId)
    if (!nextGig) {
      return false
    }

    if (isGigEditorOpen) {
      if (
        hasUnsavedGigEditorChanges() &&
        !window.confirm('Discard unsaved gig changes and edit the selected gig?')
      ) {
        return false
      }

      setGigMode('edit')
      setGigForm(toEditableGigForm(nextGig))
      setGigStatus('Editing the selected gig.')
    }

    cancelExternalResourceEdit()

    revealGig(nextGig)
    return true
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
      setSelectedGigIds((current) => current.filter((gigId) => gigId !== selectedGig.id))
      setIsGigEditorOpen(false)
      setGigMode('create')
      setGigForm(toCreateGigForm(clients))
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
    revealGig(savedGig, [savedGig, ...gigs.filter((gig) => gig.id !== savedGig.id)])
    onOpenSection('gigs')
    setGigMode('edit')
    setGigForm(toEditableGigForm(savedGig))
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

  const saveGigForm = async (
    closeAfterSave: boolean,
    expensesOverride?: GigExpenseForm[],
    successMessage?: string
  ) => {
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
      type: gigForm.type,
      status: gigForm.status,
      expenses: expensesOverride ?? gigForm.expenses,
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
          type: payload.type,
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

      const nextGigs = isEdit
        ? gigs.map((gig) => (gig.id === savedGig.id ? savedGig : gig))
        : [savedGig, ...gigs.filter((gig) => gig.id !== savedGig.id)]
      setGigs(nextGigs)
      setGigMode('edit')
      setGigForm(toEditableGigForm(savedGig))
      setGigStatus(successMessage ?? (isEdit ? 'Gig updated.' : 'Gig created.'))
      setIsGigEditorOpen(!closeAfterSave)
      revealGig(savedGig, nextGigs)
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

  const handleGigSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await saveGigForm(shouldCloseAfterSave(event))
  }

  const saveExpenseDraft = async (
    expenseIndex: number | null,
    draft: { description: string; amount: string }
  ) => {
    if (!selectedGig) {
      setGigStatus('Select a gig before saving expenses.')
      return false
    }

    const description = draft.description.trim()
    const amount = draft.amount.trim()
    const numericAmount = Number(amount)

    if (!description) {
      setGigStatus('Expense description is required.')
      return false
    }

    if (!Number.isFinite(numericAmount) || numericAmount < 0) {
      setGigStatus('Expense amount must be a valid non-negative number.')
      return false
    }

    const nextExpenses = [...gigForm.expenses]
    if (expenseIndex === null) {
      nextExpenses.push({
        id: '',
        sortOrder: nextExpenses.length + 1,
        description,
        amount,
        reimbursementStatus: 'Unreimbursed',
        reimbursedAt: null,
        reimbursementUpdatedAt: null,
        reimbursementMethod: null,
        reimbursementNote: null,
        attachments: [],
      })
    } else {
      const existing = nextExpenses[expenseIndex]
      if (!existing) {
        setGigStatus('Expense no longer exists.')
        return false
      }

      nextExpenses[expenseIndex] = {
        ...existing,
        description,
        amount,
      }
    }

    await saveGigForm(
      false,
      nextExpenses,
      expenseIndex === null ? 'Expense added.' : 'Expense updated.'
    )
    return true
  }

  const deleteExpenseDraft = async (expenseIndex: number) => {
    if (!selectedGig) {
      setGigStatus('Select a gig before deleting expenses.')
      return false
    }

    const expense = gigForm.expenses[expenseIndex]
    if (!expense) {
      setGigStatus('Expense no longer exists.')
      return false
    }

    if (!window.confirm(`Remove expense ${expense.description || expenseIndex + 1}?`)) {
      return false
    }

    const nextExpenses = gigForm.expenses
      .filter((_, index) => index !== expenseIndex)
      .map((expense, index) => ({
        ...expense,
        sortOrder: index + 1,
      }))

    await saveGigForm(false, nextExpenses, 'Expense removed.')
    return true
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
    cancelExternalResourceEdit,
    completedGigCount: gigs.filter((gig) => gig.status === 'Completed').length,
    deleteGig,
    deleteExternalResource,
    deleteExternalResourceAttachment,
    deleteExpenseDraft,
    deleteExpenseAttachment,
    downloadExpenseAttachment,
    downloadExternalResourceAttachment,
    filteredGigs,
    closeExpenseStatement,
    downloadExpenseStatementPdf,
    expenseStatementExpenseIds,
    expenseStatementGigs,
    expenseStatementPreviewUrl,
    expenseStatementReceiptCount,
    expenseStatementStatus,
    expenseStatementTotal,
    externalResourceForm,
    externalResourceMode,
    gigForm,
    gigMode,
    gigQuickFilter,
    gigTypeFilter,
    gigSearchQuery,
    gigSort,
    gigStatus,
    gigs,
    gigsById,
    estimateGigMileage,
    handleGigSubmit,
    handleToggleGigSelection,
    isGigEditorOpen,
    isExternalResourceEditorOpen,
    isExpenseStatementLoading,
    isExpenseStatementOpen,
    isGigLoading,
    isMileageEstimating,
    mergeSavedGig,
    openGigReceiptDraft,
    openExpenseStatement,
    plannedGigCount: gigs.filter((gig) => gig.status === 'Confirmed').length,
    previewExpenseStatement,
    resetGigsWorkspace,
    selectedGig,
    selectedGigIds,
    selectedGigs,
    showPastGigs,
    selectGig,
    setGigs,
    setGigQuickFilter,
    setGigTypeFilter,
    setGigSearchQuery,
    setGigSort,
    setGigStatus,
    setShowPastGigs,
    setIncludeStatementReceiptAppendix,
    setIncludeStatementReceiptAttachments,
    setSelectedGigIds,
    saveExpenseDraft,
    startGigCreate,
    startGigEdit,
    startExternalResourceCreate,
    startExternalResourceEdit,
    submitExternalResource,
    uninvoicedGigCount: gigs.filter((gig) => !gig.isInvoiced && gig.status !== 'Cancelled').length,
    upcomingGigCount: gigs.filter((gig) => gig.date >= today).length,
    updateExternalResourceField,
    updateGigField,
    updateExpenseReimbursement,
    uploadExpenseAttachment,
    uploadExternalResourceAttachment,
    includeStatementReceiptAppendix,
    includeStatementReceiptAttachments,
    toggleExpenseStatementExpense,
  }
}
