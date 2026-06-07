import { useCallback, useDeferredValue, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
} from '../api'
import { emptyClientSettingsForm, emptyForm } from '../forms'
import type { Address, Client, ClientForm, ClientSettingsForm, ClientSort } from '../types'

type UseClientsWorkspaceOptions = {
  isApiConnected: boolean
  onSessionExpired: (message: string) => void
  setIsLoading: (isLoading: boolean) => void
  setStatus: (status: string) => void
}

function shouldCloseAfterSave(event: FormEvent<HTMLFormElement>) {
  const submitter = (event.nativeEvent as SubmitEvent).submitter as
    | HTMLButtonElement
    | null

  return submitter?.dataset.closeAfterSave !== 'false'
}

function parseOptionalDecimal(value: string) {
  const trimmed = value.trim()
  if (!trimmed) {
    return null
  }

  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : Number.NaN
}

function toEditableClientForm(client: Client): ClientForm {
  return {
    name: client.name,
    email: client.email,
    billingAddress: {
      line1: client.billingAddress.line1 ?? '',
      line2: client.billingAddress.line2 ?? '',
      city: client.billingAddress.city ?? '',
      stateOrCounty: client.billingAddress.stateOrCounty ?? '',
      postalCode: client.billingAddress.postalCode ?? '',
      country: client.billingAddress.country ?? '',
    },
  }
}

export function useClientsWorkspace({
  isApiConnected,
  onSessionExpired,
  setIsLoading,
  setStatus,
}: UseClientsWorkspaceOptions) {
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [searchQuery, setSearchQuery] = useState('')
  const [clientSort, setClientSort] = useState<ClientSort>({ key: 'name', direction: 'asc' })
  const [isClientEditorOpen, setIsClientEditorOpen] = useState(false)
  const [mode, setMode] = useState<'create' | 'edit'>('create')
  const [form, setForm] = useState<ClientForm>(emptyForm)
  const [isClientSettingsOpen, setIsClientSettingsOpen] = useState(false)
  const [clientSettingsForm, setClientSettingsForm] =
    useState<ClientSettingsForm>(emptyClientSettingsForm)
  const [clientSettingsStatus, setClientSettingsStatus] =
    useState('Client-specific rates override your personal defaults when set.')
  const [isClientSettingsSaving, setIsClientSettingsSaving] = useState(false)
  const deferredSearchQuery = useDeferredValue(searchQuery)

  const clientsById = useMemo(
    () => new Map(clients.map((client) => [client.id, client])),
    [clients]
  )
  const clientNamesById = useMemo(
    () => new Map(clients.map((client) => [client.id, client.name])),
    [clients]
  )

  const filteredClients = useMemo(() => {
    const query = deferredSearchQuery.trim().toLowerCase()
    const sortDirection = clientSort.direction === 'asc' ? 1 : -1
    const compareText = (left: string, right: string) => left.localeCompare(right)
    const compareByKey = (left: Client, right: Client) => {
      switch (clientSort.key) {
        case 'city':
          return compareText(left.billingAddress.city, right.billingAddress.city)
        case 'country':
          return compareText(left.billingAddress.country, right.billingAddress.country)
        case 'email':
          return compareText(left.email, right.email)
        case 'name':
        default:
          return compareText(left.name, right.name)
      }
    }
    const sortedClients = [...clients].sort((left, right) => {
      const primaryComparison = compareByKey(left, right)
      if (primaryComparison !== 0) {
        return primaryComparison * sortDirection
      }

      const nameComparison = left.name.localeCompare(right.name)
      if (nameComparison !== 0) {
        return nameComparison
      }

      return left.id.localeCompare(right.id)
    })

    if (!query) {
      return sortedClients
    }

    return sortedClients.filter((client) =>
      [
        client.name,
        client.email,
        client.billingAddress.city,
        client.billingAddress.country,
      ]
        .join(' ')
        .toLowerCase()
        .includes(query)
    )
  }, [clientSort, clients, deferredSearchQuery])

  const selectedClient = clientsById.get(selectedClientId) ?? filteredClients[0] ?? null

  const hasUnsavedClientEditorChanges = () => {
    if (!isClientEditorOpen) {
      return false
    }

    const baseline =
      mode === 'edit' && selectedClient
        ? toEditableClientForm(selectedClient)
        : emptyForm()

    return JSON.stringify(form) !== JSON.stringify(baseline)
  }

  const applyClients = useCallback((nextClients: Client[]) => {
    setClients(nextClients)
    setSelectedClientId(nextClients[0]?.id ?? '')
  }, [])

  const resetClientsWorkspace = useCallback(() => {
    setClients([])
    setSelectedClientId('')
    setSearchQuery('')
    setClientSort({ key: 'name', direction: 'asc' })
    setIsClientEditorOpen(false)
    setMode('create')
    setForm(emptyForm())
    setIsClientSettingsOpen(false)
    setClientSettingsForm(emptyClientSettingsForm())
    setClientSettingsStatus(
      'Client-specific rates override your personal defaults when set.'
    )
    setIsClientSettingsSaving(false)
  }, [])

  const startCreating = () => {
    if (
      hasUnsavedClientEditorChanges() &&
      !window.confirm('Discard unsaved client changes and add a new client?')
    ) {
      return
    }

    setMode('create')
    setForm(emptyForm())
    setIsClientEditorOpen(true)
  }

  const startEditing = () => {
    if (!selectedClient) {
      return
    }

    setMode('edit')
    setForm(toEditableClientForm(selectedClient))
    setIsClientEditorOpen(true)
  }

  const selectClient = (clientId: string) => {
    if (clientId === selectedClient?.id) {
      return true
    }

    const nextClient = clientsById.get(clientId)
    if (!nextClient) {
      return false
    }

    if (isClientEditorOpen) {
      if (
        hasUnsavedClientEditorChanges() &&
        !window.confirm('Discard unsaved client changes and edit the selected client?')
      ) {
        return false
      }

      setMode('edit')
      setForm(toEditableClientForm(nextClient))
    }

    setSelectedClientId(clientId)
    return true
  }

  const closeClientEditor = () => {
    if (
      hasUnsavedClientEditorChanges() &&
      !window.confirm('Discard unsaved client changes and close the editor?')
    ) {
      return
    }

    setIsClientEditorOpen(false)
    setMode('create')
    setForm(emptyForm())
  }

  const updateField = (field: keyof ClientForm, value: string | Address) => {
    setForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const updateAddressField = (field: keyof Address, value: string) => {
    setForm((current) => ({
      ...current,
      billingAddress: {
        ...current.billingAddress,
        [field]: value,
      },
    }))
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const closeAfterSave = shouldCloseAfterSave(event)

    const payload: ClientForm = {
      name: form.name.trim(),
      email: form.email.trim(),
      billingAddress: {
        line1: form.billingAddress.line1.trim(),
        line2: form.billingAddress.line2.trim(),
        city: form.billingAddress.city.trim(),
        stateOrCounty: form.billingAddress.stateOrCounty.trim(),
        postalCode: form.billingAddress.postalCode.trim(),
        country: form.billingAddress.country.trim(),
      },
    }

    const preservedClientSettings =
      mode === 'edit' && selectedClient
        ? {
            mileageRate: selectedClient.mileageRate,
            passengerMileageRate: selectedClient.passengerMileageRate,
            invoiceFilenamePattern: selectedClient.invoiceFilenamePattern,
            invoiceEmailSubjectPattern: selectedClient.invoiceEmailSubjectPattern,
          }
        : {
            mileageRate: null,
            passengerMileageRate: null,
            invoiceFilenamePattern: null,
            invoiceEmailSubjectPattern: null,
          }

    if (
      !payload.name ||
      !payload.email ||
      !payload.billingAddress.line1 ||
      !payload.billingAddress.city
    ) {
      setStatus('Name, email, address line 1 and city are required.')
      return
    }

    setIsLoading(true)

    try {
      if (!isApiConnected) {
        throw new Error('API unavailable.')
      }

      const isEdit = mode === 'edit' && selectedClient
      const endpoint = isEdit
        ? buildApiUrl(`/clients/${selectedClient.id}`)
        : buildApiUrl('/clients')
      const requestBody = {
        ...payload,
        ...preservedClientSettings,
      }

      const response = await fetchWithSession(
        endpoint,
        jsonRequestInit(isEdit ? 'PUT' : 'POST', requestBody)
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to save changes.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error('Save failed.')
      }

      const savedClient = (await response.json()) as Client

      setClients((current) => {
        if (isEdit) {
          return current.map((client) =>
            client.id === savedClient.id ? savedClient : client
          )
        }

        return [savedClient, ...current]
      })

      setSelectedClientId(savedClient.id)
      setMode('edit')
      setStatus(isEdit ? 'Client updated.' : 'Client created.')
      setIsClientEditorOpen(!closeAfterSave)
    } catch {
      setStatus('Unable to save right now. Please try again.')
    } finally {
      setIsLoading(false)
    }
  }

  const handleDelete = async () => {
    if (!selectedClient) {
      return
    }

    setIsLoading(true)

    try {
      if (!isApiConnected) {
        throw new Error('API unavailable.')
      }

      const response = await fetchWithSession(buildApiUrl(`/clients/${selectedClient.id}`), {
        method: 'DELETE',
      })

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to delete clients.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Delete failed.'))
      }

      let nextSelectedClientId = ''

      setClients((current) => {
        const remaining = current.filter((client) => client.id !== selectedClient.id)
        nextSelectedClientId = remaining[0]?.id ?? ''
        return remaining
      })

      setSelectedClientId(nextSelectedClientId)
      setMode('create')
      setForm(emptyForm())
      setStatus('Client deleted.')
      setIsClientEditorOpen(false)
    } catch (error) {
      setStatus(
        error instanceof Error ? error.message : 'Unable to delete right now.'
      )
    } finally {
      setIsLoading(false)
    }
  }

  const openClientSettings = () => {
    if (!selectedClient) {
      return
    }

    setClientSettingsForm({
      mileageRate:
        selectedClient.mileageRate === null ? '' : String(selectedClient.mileageRate),
      passengerMileageRate:
        selectedClient.passengerMileageRate === null
          ? ''
          : String(selectedClient.passengerMileageRate),
      invoiceFilenamePattern: selectedClient.invoiceFilenamePattern ?? '',
      invoiceEmailSubjectPattern: selectedClient.invoiceEmailSubjectPattern ?? '',
    })
    setClientSettingsStatus(
      'Client-specific rates override your personal defaults when set.'
    )
    setIsClientSettingsOpen(true)
  }

  const closeClientSettings = () => {
    setIsClientSettingsOpen(false)
  }

  const updateClientSettingsField = (
    field: keyof ClientSettingsForm,
    value: string
  ) => {
    setClientSettingsForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const handleClientSettingsSubmit = async (
    event: FormEvent<HTMLFormElement>
  ) => {
    event.preventDefault()

    if (!selectedClient) {
      return
    }

    const mileageRate = parseOptionalDecimal(clientSettingsForm.mileageRate)
    const passengerMileageRate = parseOptionalDecimal(
      clientSettingsForm.passengerMileageRate
    )
    const invoiceFilenamePattern = clientSettingsForm.invoiceFilenamePattern.trim()
    const invoiceEmailSubjectPattern =
      clientSettingsForm.invoiceEmailSubjectPattern.trim()

    if (Number.isNaN(mileageRate) || Number.isNaN(passengerMileageRate)) {
      setClientSettingsStatus('Rates must be valid numbers, for example 0.45.')
      return
    }

    setIsClientSettingsSaving(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/clients/${selectedClient.id}`),
        jsonRequestInit('PUT', {
            id: selectedClient.id,
            name: selectedClient.name,
            email: selectedClient.email,
            billingAddress: selectedClient.billingAddress,
            mileageRate,
            passengerMileageRate,
            invoiceFilenamePattern: invoiceFilenamePattern || null,
            invoiceEmailSubjectPattern: invoiceEmailSubjectPattern || null,
          })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to update client settings.'
        )
      ) {
        setIsClientSettingsOpen(false)
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to save client settings.')
        )
      }

      const savedClient = (await response.json()) as Client

      setClients((current) =>
        current.map((client) => (client.id === savedClient.id ? savedClient : client))
      )
      setClientSettingsForm({
        mileageRate: savedClient.mileageRate === null ? '' : String(savedClient.mileageRate),
        passengerMileageRate:
          savedClient.passengerMileageRate === null
            ? ''
            : String(savedClient.passengerMileageRate),
        invoiceFilenamePattern: savedClient.invoiceFilenamePattern ?? '',
        invoiceEmailSubjectPattern: savedClient.invoiceEmailSubjectPattern ?? '',
      })
      setClientSettingsStatus('Client settings updated.')
    } catch (error) {
      setClientSettingsStatus(
        error instanceof Error
          ? error.message
          : 'Unable to save client settings right now.'
      )
    } finally {
      setIsClientSettingsSaving(false)
    }
  }

  return {
    applyClients,
    clientNamesById,
    clientSettingsForm,
    clientSettingsStatus,
    clientSort,
    clients,
    closeClientEditor,
    closeClientSettings,
    filteredClients,
    form,
    handleClientSettingsSubmit,
    handleDelete,
    handleSubmit,
    isClientEditorOpen,
    isClientSettingsOpen,
    isClientSettingsSaving,
    mode,
    openClientSettings,
    resetClientsWorkspace,
    searchQuery,
    selectedClient,
    selectClient,
    setSelectedClientId,
    setClientSort,
    setSearchQuery,
    startCreating,
    startEditing,
    updateAddressField,
    updateClientSettingsField,
    updateField,
  }
}
