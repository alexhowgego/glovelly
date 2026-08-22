import { useCallback, useState } from 'react'
import type { Dispatch, FormEvent, SetStateAction } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
} from '../api'
import { emptyUserSettingsForm } from '../forms'
import type { AuthUser, GoogleCalendarStatus, UserSettingsForm } from '../types'

type SavedUserSettings = {
  displayName: string
  mileageRate: number | null
  passengerMileageRate: number | null
  travelOriginPostcode: string | null
  defaultPaymentWindowDays: number | null
  invoiceFilenamePattern: string | null
  invoiceEmailSubjectPattern: string | null
  invoiceEmailBodyTemplate: string | null
  invoiceReplyToEmail: string | null
  invoiceUploadFolderId: string | null
}

type UseUserSettingsOptions = {
  authUser: AuthUser | null
  onCloseProfileMenu: () => void
  onDisplayNameSaved: (userId: string, displayName: string) => void
  onSessionExpired: (message: string) => void
  setAuthUser: Dispatch<SetStateAction<AuthUser | null>>
}

const defaultUserSettingsStatus =
  'Set the mileage defaults used when a client has no custom rates.'

function toUserSettingsForm(settings: SavedUserSettings): UserSettingsForm {
  return {
    displayName: settings.displayName,
    mileageRate: settings.mileageRate === null ? '' : String(settings.mileageRate),
    passengerMileageRate:
      settings.passengerMileageRate === null
        ? ''
        : String(settings.passengerMileageRate),
    travelOriginPostcode: settings.travelOriginPostcode ?? '',
    defaultPaymentWindowDays:
      settings.defaultPaymentWindowDays === null
        ? ''
        : String(settings.defaultPaymentWindowDays),
    invoiceFilenamePattern: settings.invoiceFilenamePattern ?? '',
    invoiceEmailSubjectPattern: settings.invoiceEmailSubjectPattern ?? '',
    invoiceEmailBodyTemplate: settings.invoiceEmailBodyTemplate ?? '',
    invoiceReplyToEmail: settings.invoiceReplyToEmail ?? '',
    invoiceUploadFolderId: settings.invoiceUploadFolderId ?? '',
  }
}

function parseOptionalDecimal(value: string) {
  const trimmed = value.trim()
  if (!trimmed) {
    return null
  }

  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : Number.NaN
}

export function useUserSettings({
  authUser,
  onCloseProfileMenu,
  onDisplayNameSaved,
  onSessionExpired,
  setAuthUser,
}: UseUserSettingsOptions) {
  const [isUserSettingsOpen, setIsUserSettingsOpen] = useState(false)
  const [isConnectedServicesOpen, setIsConnectedServicesOpen] = useState(false)
  const [userSettingsForm, setUserSettingsForm] =
    useState<UserSettingsForm>(emptyUserSettingsForm)
  const [userSettingsStatus, setUserSettingsStatus] =
    useState(defaultUserSettingsStatus)
  const [isUserSettingsSaving, setIsUserSettingsSaving] = useState(false)
  const [googleCalendarStatus, setGoogleCalendarStatus] =
    useState<GoogleCalendarStatus | null>(null)
  const [isGoogleCalendarBusy, setIsGoogleCalendarBusy] = useState(false)
  const [isGoogleDriveBusy, setIsGoogleDriveBusy] = useState(false)
  const [isGoogleSheetsBusy, setIsGoogleSheetsBusy] = useState(false)

  const resetUserSettings = useCallback(() => {
    setIsUserSettingsOpen(false)
    setIsConnectedServicesOpen(false)
    setUserSettingsForm(emptyUserSettingsForm())
    setUserSettingsStatus(defaultUserSettingsStatus)
    setIsUserSettingsSaving(false)
    setGoogleCalendarStatus(null)
    setIsGoogleCalendarBusy(false)
    setIsGoogleDriveBusy(false)
    setIsGoogleSheetsBusy(false)
  }, [])

  const loadGoogleCalendarStatus = useCallback(async () => {
    try {
      const response = await fetchWithSession(
        buildApiUrl('/integrations/google-calendar/status')
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to manage Google Calendar.'
        )
      ) {
        setIsUserSettingsOpen(false)
        return
      }

      if (!response.ok) {
        return
      }

      setGoogleCalendarStatus((await response.json()) as GoogleCalendarStatus)
    } catch {
      setGoogleCalendarStatus(null)
    }
  }, [onSessionExpired])

  const openUserSettings = () => {
    setUserSettingsForm(
      toUserSettingsForm({
        displayName: authUser?.name ?? '',
        mileageRate: authUser?.mileageRate ?? null,
        passengerMileageRate: authUser?.passengerMileageRate ?? null,
        travelOriginPostcode: authUser?.travelOriginPostcode ?? null,
        defaultPaymentWindowDays: authUser?.defaultPaymentWindowDays ?? null,
        invoiceFilenamePattern: authUser?.invoiceFilenamePattern ?? null,
        invoiceEmailSubjectPattern: authUser?.invoiceEmailSubjectPattern ?? null,
        invoiceEmailBodyTemplate: authUser?.invoiceEmailBodyTemplate ?? null,
        invoiceReplyToEmail: authUser?.invoiceReplyToEmail ?? null,
        invoiceUploadFolderId: authUser?.invoiceUploadFolderId ?? null,
      })
    )
    setUserSettingsStatus(
      'Set the defaults used when a client does not provide its own overrides.'
    )
    onCloseProfileMenu()
    setIsUserSettingsOpen(true)
  }

  const openConnectedServices = () => {
    setUserSettingsStatus('Manage connected services and Google authorizations.')
    onCloseProfileMenu()
    setIsConnectedServicesOpen(true)
    void loadGoogleCalendarStatus()
  }

  const closeConnectedServices = () => {
    setIsConnectedServicesOpen(false)
  }

  const openSettingsFromServices = () => {
    setIsConnectedServicesOpen(false)
    openUserSettings()
  }

  const closeUserSettings = () => {
    setIsUserSettingsOpen(false)
  }

  const connectGoogleDrive = () => {
    window.location.assign(buildApiUrl('/integrations/google-drive/connect'))
  }

  const connectGoogleSheets = () => {
    window.location.assign(buildApiUrl('/integrations/google-sheets/connect'))
  }

  const disconnectGoogleDrive = async () => {
    setIsGoogleDriveBusy(true)
    try {
      const response = await fetchWithSession(
        buildApiUrl('/integrations/google-drive/disconnect'),
        { method: 'POST' }
      )
      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to disconnect Google Drive.'
        )
      ) {
        setIsUserSettingsOpen(false)
        setIsConnectedServicesOpen(false)
        return
      }
      if (!response.ok) {
        throw new Error(
          (await getResponseErrorMessage(response, 'Unable to disconnect Drive.')) ??
            'Unable to disconnect Drive.'
        )
      }

      setAuthUser((current) => current
        ? {
            ...current,
            isGoogleDriveConnected: false,
            invoiceUploadFolderId: null,
          }
        : current)
      setUserSettingsForm((current) => ({ ...current, invoiceUploadFolderId: '' }))
      setUserSettingsStatus('Google Drive disconnected.')
    } catch (error) {
      setUserSettingsStatus(
        error instanceof Error ? error.message : 'Unable to disconnect Drive.'
      )
    } finally {
      setIsGoogleDriveBusy(false)
    }
  }

  const disconnectGoogleSheets = async () => {
    setIsGoogleSheetsBusy(true)
    try {
      const response = await fetchWithSession(
        buildApiUrl('/integrations/google-sheets/disconnect'),
        { method: 'POST' }
      )
      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to disconnect Google Sheets.'
        )
      ) {
        setIsUserSettingsOpen(false)
        setIsConnectedServicesOpen(false)
        return
      }
      if (!response.ok) {
        throw new Error(
          (await getResponseErrorMessage(response, 'Unable to disconnect Sheets.')) ??
            'Unable to disconnect Sheets.'
        )
      }

      setAuthUser((current) => current
        ? {
            ...current,
            isGoogleSheetsConnected: false,
          }
        : current)
      setUserSettingsStatus('Google Sheets disconnected.')
    } catch (error) {
      setUserSettingsStatus(
        error instanceof Error ? error.message : 'Unable to disconnect Sheets.'
      )
    } finally {
      setIsGoogleSheetsBusy(false)
    }
  }

  const connectGoogleCalendar = () => {
    window.location.assign(buildApiUrl('/integrations/google-calendar/connect'))
  }

  const disconnectGoogleCalendar = async () => {
    setIsGoogleCalendarBusy(true)
    try {
      const response = await fetchWithSession(
        buildApiUrl('/integrations/google-calendar/disconnect'),
        { method: 'POST' }
      )
      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to disconnect Google Calendar.'
        )
      ) {
        setIsUserSettingsOpen(false)
        setIsConnectedServicesOpen(false)
        return
      }
      if (!response.ok) {
        throw new Error(
          (await getResponseErrorMessage(response, 'Unable to disconnect Calendar.')) ??
            'Unable to disconnect Calendar.'
        )
      }
      setUserSettingsStatus('Google Calendar disconnected.')
      await loadGoogleCalendarStatus()
    } catch (error) {
      setUserSettingsStatus(
        error instanceof Error ? error.message : 'Unable to disconnect Calendar.'
      )
    } finally {
      setIsGoogleCalendarBusy(false)
    }
  }

  const updateUserSettingsField = (
    field: keyof UserSettingsForm,
    value: string
  ) => {
    setUserSettingsForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const handleUserSettingsSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const mileageRate = parseOptionalDecimal(userSettingsForm.mileageRate)
    const displayName = userSettingsForm.displayName.trim()
    const passengerMileageRate = parseOptionalDecimal(
      userSettingsForm.passengerMileageRate
    )
    const travelOriginPostcode = userSettingsForm.travelOriginPostcode.trim()
    const defaultPaymentWindowDaysText = userSettingsForm.defaultPaymentWindowDays.trim()
    const invoiceFilenamePattern = userSettingsForm.invoiceFilenamePattern.trim()
    const invoiceEmailSubjectPattern =
      userSettingsForm.invoiceEmailSubjectPattern.trim()
    const invoiceEmailBodyTemplate = userSettingsForm.invoiceEmailBodyTemplate.trim()
    const invoiceReplyToEmail = userSettingsForm.invoiceReplyToEmail.trim()
    const invoiceUploadFolderId = userSettingsForm.invoiceUploadFolderId.trim()

    if (!displayName) {
      setUserSettingsStatus('Display name cannot be empty.')
      return
    }

    if (Number.isNaN(mileageRate) || Number.isNaN(passengerMileageRate)) {
      setUserSettingsStatus('Rates must be valid numbers, for example 0.45.')
      return
    }

    const defaultPaymentWindowDays = defaultPaymentWindowDaysText
      ? Number(defaultPaymentWindowDaysText)
      : null
    if (
      defaultPaymentWindowDays !== null &&
      (!Number.isInteger(defaultPaymentWindowDays) || defaultPaymentWindowDays < 0)
    ) {
      setUserSettingsStatus('Payment window must be a whole number of days.')
      return
    }

    setIsUserSettingsSaving(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl('/auth/me/settings'),
        jsonRequestInit('PUT', {
          displayName,
          mileageRate,
          passengerMileageRate,
          travelOriginPostcode: travelOriginPostcode || null,
          defaultPaymentWindowDays,
          invoiceFilenamePattern: invoiceFilenamePattern || null,
          invoiceEmailSubjectPattern: invoiceEmailSubjectPattern || null,
          invoiceEmailBodyTemplate: invoiceEmailBodyTemplate || null,
          invoiceReplyToEmail: invoiceReplyToEmail || null,
          invoiceUploadFolderId: invoiceUploadFolderId || null,
        })
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to update your settings.'
        )
      ) {
        setIsUserSettingsOpen(false)
        setIsConnectedServicesOpen(false)
        return
      }

      if (!response.ok) {
        throw new Error(
          (await getResponseErrorMessage(response, 'Unable to save your settings.')) ??
            'Unable to save your settings.'
        )
      }

      const savedSettings = (await response.json()) as SavedUserSettings
      setAuthUser((current) =>
        current
          ? {
            ...current,
            name: savedSettings.displayName,
            mileageRate: savedSettings.mileageRate,
              passengerMileageRate: savedSettings.passengerMileageRate,
              travelOriginPostcode: savedSettings.travelOriginPostcode,
              defaultPaymentWindowDays: savedSettings.defaultPaymentWindowDays,
              invoiceFilenamePattern: savedSettings.invoiceFilenamePattern,
              invoiceEmailSubjectPattern: savedSettings.invoiceEmailSubjectPattern,
              invoiceEmailBodyTemplate: savedSettings.invoiceEmailBodyTemplate,
              invoiceReplyToEmail: savedSettings.invoiceReplyToEmail,
              invoiceUploadFolderId: savedSettings.invoiceUploadFolderId,
              isGoogleDriveConnected: current.isGoogleDriveConnected,
              isGoogleSheetsConnected: current.isGoogleSheetsConnected,
            }
          : current
      )
      if (authUser) {
        onDisplayNameSaved(authUser.userId, savedSettings.displayName)
      }
      setUserSettingsForm(toUserSettingsForm(savedSettings))
      setUserSettingsStatus('Settings updated.')
    } catch (error) {
      setUserSettingsStatus(
        error instanceof Error
          ? error.message
          : 'Unable to save your settings right now.'
      )
    } finally {
      setIsUserSettingsSaving(false)
    }
  }

  return {
    closeUserSettings,
    closeConnectedServices,
    connectGoogleCalendar,
    connectGoogleDrive,
    connectGoogleSheets,
    disconnectGoogleCalendar,
    disconnectGoogleDrive,
    disconnectGoogleSheets,
    googleCalendarStatus,
    handleUserSettingsSubmit,
    isGoogleCalendarBusy,
    isGoogleDriveBusy,
    isGoogleSheetsBusy,
    isConnectedServicesOpen,
    isUserSettingsOpen,
    isUserSettingsSaving,
    openConnectedServices,
    openSettingsFromServices,
    openUserSettings,
    resetUserSettings,
    updateUserSettingsField,
    userSettingsForm,
    userSettingsStatus,
  }
}
