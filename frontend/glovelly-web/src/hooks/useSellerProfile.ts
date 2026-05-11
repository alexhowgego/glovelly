import { useCallback, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  parseProblemDetails,
} from '../api'
import { emptySellerProfileForm } from '../forms'
import type { SellerProfile, SellerProfileForm } from '../types'

type UseSellerProfileOptions = {
  onCloseProfileMenu: () => void
  onSessionExpired: (message: string) => void
}

const defaultSellerProfileStatus =
  'Add the seller details that should appear on your invoices.'

export function toSellerProfileForm(profile: SellerProfile): SellerProfileForm {
  return {
    sellerName: profile.sellerName ?? '',
    addressLine1: profile.addressLine1 ?? '',
    addressLine2: profile.addressLine2 ?? '',
    city: profile.city ?? '',
    region: profile.region ?? '',
    postcode: profile.postcode ?? '',
    country: profile.country ?? 'United Kingdom',
    email: profile.email ?? '',
    phone: profile.phone ?? '',
    accountName: profile.accountName ?? '',
    sortCode: profile.sortCode ?? '',
    accountNumber: profile.accountNumber ?? '',
    paymentReferenceNote: profile.paymentReferenceNote ?? '',
  }
}

function emptySellerProfile(): SellerProfile {
  return {
    id: null,
    sellerName: null,
    addressLine1: null,
    addressLine2: null,
    city: null,
    region: null,
    postcode: null,
    country: 'United Kingdom',
    email: null,
    phone: null,
    accountName: null,
    sortCode: null,
    accountNumber: null,
    paymentReferenceNote: null,
    isConfigured: false,
    isInvoiceReady: false,
    missingFields: ['sellerName', 'addressLine1', 'city', 'country'],
  }
}

export function useSellerProfile({
  onCloseProfileMenu,
  onSessionExpired,
}: UseSellerProfileOptions) {
  const [sellerProfile, setSellerProfile] = useState<SellerProfile>(emptySellerProfile)
  const [sellerProfileForm, setSellerProfileForm] =
    useState<SellerProfileForm>(emptySellerProfileForm)
  const [sellerProfileStatus, setSellerProfileStatus] = useState(
    defaultSellerProfileStatus
  )
  const [isSellerProfileOpen, setIsSellerProfileOpen] = useState(false)
  const [isSellerProfileSaving, setIsSellerProfileSaving] = useState(false)

  const applySellerProfile = useCallback((profile: SellerProfile) => {
    setSellerProfile(profile)
    setSellerProfileForm(toSellerProfileForm(profile))
  }, [])

  const resetSellerProfile = useCallback(() => {
    setIsSellerProfileOpen(false)
    setSellerProfile(emptySellerProfile())
    setSellerProfileForm(emptySellerProfileForm())
    setSellerProfileStatus(defaultSellerProfileStatus)
    setIsSellerProfileSaving(false)
  }, [])

  const openSellerProfile = (configuredStatus: string) => {
    setSellerProfileForm(toSellerProfileForm(sellerProfile))
    setSellerProfileStatus(
      sellerProfile.isConfigured ? configuredStatus : defaultSellerProfileStatus
    )
    onCloseProfileMenu()
    setIsSellerProfileOpen(true)
  }

  const closeSellerProfile = () => {
    setIsSellerProfileOpen(false)
  }

  const updateSellerProfileField = (
    field: keyof SellerProfileForm,
    value: string
  ) => {
    setSellerProfileForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const handleSellerProfileSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setIsSellerProfileSaving(true)

    try {
      const response = await fetchWithSession(buildApiUrl('/seller-profile'), {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          sellerName: sellerProfileForm.sellerName.trim() || null,
          addressLine1: sellerProfileForm.addressLine1.trim() || null,
          addressLine2: sellerProfileForm.addressLine2.trim() || null,
          city: sellerProfileForm.city.trim() || null,
          region: sellerProfileForm.region.trim() || null,
          postcode: sellerProfileForm.postcode.trim() || null,
          country: sellerProfileForm.country.trim() || null,
          email: sellerProfileForm.email.trim() || null,
          phone: sellerProfileForm.phone.trim() || null,
          accountName: sellerProfileForm.accountName.trim() || null,
          sortCode: sellerProfileForm.sortCode.trim() || null,
          accountNumber: sellerProfileForm.accountNumber.trim() || null,
          paymentReferenceNote: sellerProfileForm.paymentReferenceNote.trim() || null,
        }),
      })

      if (response.status === 401) {
        setIsSellerProfileOpen(false)
        onSessionExpired('Your session expired. Sign in again to update your seller profile.')
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to save seller profile.')
      }

      const savedProfile = (await response.json()) as SellerProfile
      applySellerProfile(savedProfile)
      setSellerProfileStatus(
        savedProfile.isInvoiceReady
          ? 'Seller profile updated and ready for invoice generation.'
          : `Seller profile saved. Missing: ${savedProfile.missingFields.join(', ')}.`
      )
    } catch (error) {
      setSellerProfileStatus(
        error instanceof Error
          ? error.message
          : 'Unable to save your seller profile right now.'
      )
    } finally {
      setIsSellerProfileSaving(false)
    }
  }

  return {
    applySellerProfile,
    closeSellerProfile,
    handleSellerProfileSubmit,
    isSellerProfileOpen,
    isSellerProfileSaving,
    openSellerProfile,
    resetSellerProfile,
    sellerProfile,
    sellerProfileForm,
    sellerProfileStatus,
    updateSellerProfileField,
  }
}
