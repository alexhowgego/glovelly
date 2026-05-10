import { useCallback, useState } from 'react'
import type { Dispatch, FormEvent, SetStateAction } from 'react'
import {
  buildApiUrl,
  emptyUserSettingsForm,
  fetchWithSession,
  parseProblemDetails,
} from '../appShared'
import type { AuthUser, UserSettingsForm } from '../appShared'

type SavedUserSettings = {
  mileageRate: number | null
  passengerMileageRate: number | null
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

  const resetUserSettings = useCallback(() => {
    setIsUserSettingsOpen(false)
    setUserSettingsForm(emptyUserSettingsForm())
    setUserSettingsStatus(defaultUserSettingsStatus)
    setIsUserSettingsSaving(false)
  }, [])

  const openUserSettings = () => {
    setUserSettingsForm(
      toUserSettingsForm({
        mileageRate: authUser?.mileageRate ?? null,
        passengerMileageRate: authUser?.passengerMileageRate ?? null,
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
  }

  const closeUserSettings = () => {
    setIsUserSettingsOpen(false)
  }

  const connectGoogleDrive = () => {
    window.location.assign(buildApiUrl('/integrations/google-drive/connect'))
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
          defaultPaymentWindowDays,
          invoiceFilenamePattern: invoiceFilenamePattern || null,
          invoiceEmailSubjectPattern: invoiceEmailSubjectPattern || null,
          invoiceReplyToEmail: invoiceReplyToEmail || null,
          invoiceUploadFolderId: invoiceUploadFolderId || null,
        }),
      })

      if (response.status === 401) {
        setIsUserSettingsOpen(false)
        onSessionExpired('Your session expired. Sign in again to update your settings.')
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
    connectGoogleDrive,
    handleUserSettingsSubmit,
    isUserSettingsOpen,
    isUserSettingsSaving,
    openUserSettings,
    resetUserSettings,
    updateUserSettingsField,
    userSettingsForm,
    userSettingsStatus,
  }
}
