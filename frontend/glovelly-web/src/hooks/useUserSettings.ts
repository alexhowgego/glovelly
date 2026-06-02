import { useCallback, useState } from 'react'
import type { Dispatch, FormEvent, SetStateAction } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  handleSessionExpired,
  parseProblemDetails,
} from '../api'
import { emptyUserSettingsForm } from '../forms'
import type { AuthUser, GoogleCalendarStatus, UserSettingsForm } from '../types'

type SavedUserSettings = {
  mileageRate: number | null
  passengerMileageRate: number | null
  travelOriginPostcode: string | null
  defaultPaymentWindowDays: number | null
  invoiceFilenamePattern: string | null
  invoiceEmailSubjectPattern: string | null
  invoiceReplyToEmail: string | null
  invoiceUploadFolderId: string | null
}

type UseUserSettingsOptions = {
  authUser: AuthUser | null
  onCloseProfileMenu: () => void
  onSessionExpired: (message: string) => void
  setAuthUser: Dispatch<SetStateAction<AuthUser | null>>
}

const defaultUserSettingsStatus =
  'Set the mileage defaults used when a client has no custom rates.'

function toUserSettingsForm(settings: SavedUserSettings): UserSettingsForm {
  return {
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
  onSessionExpired,
  setAuthUser,
}: UseUserSettingsOptions) {
  const [isUserSettingsOpen, setIsUserSettingsOpen] = useState(false)
  const [userSettingsForm, setUserSettingsForm] =
    useState<UserSettingsForm>(emptyUserSettingsForm)
  const [userSettingsStatus, setUserSettingsStatus] =
    useState(defaultUserSettingsStatus)
  const [isUserSettingsSaving, setIsUserSettingsSaving] = useState(false)
  const [googleCalendarStatus, setGoogleCalendarStatus] =
    useState<GoogleCalendarStatus | null>(null)
  const [isGoogleCalendarBusy, setIsGoogleCalendarBusy] = useState(false)

  const resetUserSettings = useCallback(() => {
    setIsUserSettingsOpen(false)
    setUserSettingsForm(emptyUserSettingsForm())
    setUserSettingsStatus(defaultUserSettingsStatus)
    setIsUserSettingsSaving(false)
    setGoogleCalendarStatus(null)
    setIsGoogleCalendarBusy(false)
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
        mileageRate: authUser?.mileageRate ?? null,
        passengerMileageRate: authUser?.passengerMileageRate ?? null,
        travelOriginPostcode: authUser?.travelOriginPostcode ?? null,
        defaultPaymentWindowDays: authUser?.defaultPaymentWindowDays ?? null,
        invoiceFilenamePattern: authUser?.invoiceFilenamePattern ?? null,
        invoiceEmailSubjectPattern: authUser?.invoiceEmailSubjectPattern ?? null,
        invoiceReplyToEmail: authUser?.invoiceReplyToEmail ?? null,
        invoiceUploadFolderId: authUser?.invoiceUploadFolderId ?? null,
      })
    )
    setUserSettingsStatus(
      'Set the defaults used when a client does not provide its own overrides.'
    )
    onCloseProfileMenu()
    setIsUserSettingsOpen(true)
    void loadGoogleCalendarStatus()
  }

  const closeUserSettings = () => {
    setIsUserSettingsOpen(false)
  }

  const connectGoogleDrive = () => {
    window.location.assign(buildApiUrl('/integrations/google-drive/connect'))
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
        return
      }
      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        throw new Error(problem?.detail ?? problem?.title ?? 'Unable to disconnect Calendar.')
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
    const passengerMileageRate = parseOptionalDecimal(
      userSettingsForm.passengerMileageRate
    )
    const travelOriginPostcode = userSettingsForm.travelOriginPostcode.trim()
    const defaultPaymentWindowDaysText = userSettingsForm.defaultPaymentWindowDays.trim()
    const invoiceFilenamePattern = userSettingsForm.invoiceFilenamePattern.trim()
    const invoiceEmailSubjectPattern =
      userSettingsForm.invoiceEmailSubjectPattern.trim()
    const invoiceReplyToEmail = userSettingsForm.invoiceReplyToEmail.trim()
    const invoiceUploadFolderId = userSettingsForm.invoiceUploadFolderId.trim()

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
      const response = await fetchWithSession(buildApiUrl('/auth/me/settings'), {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          mileageRate,
          passengerMileageRate,
          travelOriginPostcode: travelOriginPostcode || null,
          defaultPaymentWindowDays,
          invoiceFilenamePattern: invoiceFilenamePattern || null,
          invoiceEmailSubjectPattern: invoiceEmailSubjectPattern || null,
          invoiceReplyToEmail: invoiceReplyToEmail || null,
          invoiceUploadFolderId: invoiceUploadFolderId || null,
        }),
      })

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to update your settings.'
        )
      ) {
        setIsUserSettingsOpen(false)
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to save your settings.')
      }

      const savedSettings = (await response.json()) as SavedUserSettings
      setAuthUser((current) =>
        current
          ? {
              ...current,
              mileageRate: savedSettings.mileageRate,
              passengerMileageRate: savedSettings.passengerMileageRate,
              travelOriginPostcode: savedSettings.travelOriginPostcode,
              defaultPaymentWindowDays: savedSettings.defaultPaymentWindowDays,
              invoiceFilenamePattern: savedSettings.invoiceFilenamePattern,
              invoiceEmailSubjectPattern: savedSettings.invoiceEmailSubjectPattern,
              invoiceReplyToEmail: savedSettings.invoiceReplyToEmail,
              invoiceUploadFolderId: savedSettings.invoiceUploadFolderId,
            }
          : current
      )
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
    connectGoogleCalendar,
    connectGoogleDrive,
    disconnectGoogleCalendar,
    googleCalendarStatus,
    handleUserSettingsSubmit,
    isGoogleCalendarBusy,
    isUserSettingsOpen,
    isUserSettingsSaving,
    openUserSettings,
    resetUserSettings,
    updateUserSettingsField,
    userSettingsForm,
    userSettingsStatus,
  }
}
