import { useDeferredValue, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import {
  AdminSection,
  AppShell,
  ClientSettingsModal,
  ClientsSection,
  GigsSection,
  InvoicesSection,
  QuickReceiptModal,
  SellerProfileModal,
  SessionCheckingScreen,
  SignInScreen,
  UserSettingsModal,
} from './AppSections'
import type { AppNavigationItem, HeaderMetric } from './AppSections'
import {
  buildApiUrl,
  buildInvoiceEmailSubjectPreview,
  buildInvoiceFilenamePreview,
  buildReturnUrl,
  defaultAdminStatus,
  emptyAdminForm,
  invoiceFilenameTokens,
  fetchWithSession,
  getStoredThemePreference,
  parseProblemDetails,
  themeStorageKey,
} from './appShared'
import { useClientsWorkspace } from './hooks/useClientsWorkspace'
import { useGigsWorkspace } from './hooks/useGigsWorkspace'
import { useInvoicesWorkspace } from './hooks/useInvoicesWorkspace'
import { useQuickReceipt } from './hooks/useQuickReceipt'
import { useSellerProfile } from './hooks/useSellerProfile'
import { useUserSettings } from './hooks/useUserSettings'
import type {
  AdminUser,
  AdminUserForm,
  AppMetadata,
  AppSection,
  AuthUser,
  Client,
  Gig,
  Invoice,
  SellerProfile,
  ThemePreference,
} from './appShared'
import './App.css'

function getCurrentMonthValue() {
  return new Date().toISOString().slice(0, 7)
}

function buildMonthlyInvoiceNumber(month: string, sequence: number) {
  return `GLV-${month.replace('-', '')}-${String(sequence).padStart(3, '0')}`
}


type AppProps = {
  appMetadata: AppMetadata
}

function App({ appMetadata }: AppProps) {
  const [activeSection, setActiveSection] = useState<AppSection>('gigs')
  const [status, setStatus] = useState('Checking your session...')
  const [isLoading, setIsLoading] = useState(false)
  const [isApiConnected, setIsApiConnected] = useState(false)
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [authUser, setAuthUser] = useState<AuthUser | null>(null)
  const [isCheckingSession, setIsCheckingSession] = useState(true)
  const [shouldCloseBrowserNotice, setShouldCloseBrowserNotice] = useState(false)
  const [isProfileMenuOpen, setIsProfileMenuOpen] = useState(false)
  const profileMenuRef = useRef<HTMLDivElement | null>(null)
  const [themePreference, setThemePreference] =
    useState<ThemePreference>(getStoredThemePreference)

  const [adminUsers, setAdminUsers] = useState<AdminUser[]>([])
  const [selectedAdminUserId, setSelectedAdminUserId] = useState<string>('')
  const [adminSearchQuery, setAdminSearchQuery] = useState('')
  const [isAdminEditorOpen, setIsAdminEditorOpen] = useState(false)
  const [adminMode, setAdminMode] = useState<'create' | 'edit'>('create')
  const [adminForm, setAdminForm] = useState<AdminUserForm>(emptyAdminForm)
  const [adminStatus, setAdminStatus] = useState(defaultAdminStatus)
  const [isAdminLoading, setIsAdminLoading] = useState(false)
  const [monthlyInvoiceMonth, setMonthlyInvoiceMonth] = useState(getCurrentMonthValue)
  const [monthlyInvoiceStatus, setMonthlyInvoiceStatus] = useState('')
  const deferredAdminSearchQuery = useDeferredValue(adminSearchQuery)

  const isAdmin = authUser?.role === 'Admin'
  const {
    applyClients,
    clientNamesById,
    clientSettingsForm,
    clientSettingsStatus,
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
    setSelectedClientId,
    setSearchQuery,
    startCreating,
    startEditing,
    updateAddressField,
    updateClientSettingsField,
    updateField,
  } = useClientsWorkspace({
    isApiConnected,
    onSessionExpired: (message) => {
      setIsAuthenticated(false)
      setAuthUser(null)
      setIsApiConnected(false)
      setStatus(message)
    },
    setIsLoading,
    setStatus,
  })
  const {
    applyGigs,
    closeGigEditor,
    completedGigCount,
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
    plannedGigCount,
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
    uninvoicedGigCount,
    upcomingGigCount,
    updateGigExpenseField,
    updateGigField,
    uploadExpenseAttachment,
  } = useGigsWorkspace({
    clientNamesById,
    clients,
    onOpenSection: (section) => setActiveSection(section),
    onSessionExpired: (message) => {
      setIsAuthenticated(false)
      setAuthUser(null)
      setIsApiConnected(false)
      setStatus(message)
    },
  })
  const {
    adjustmentAmount,
    adjustmentReason,
    applyInvoices,
    closeInvoiceEditor,
    draftInvoiceCount,
    filteredInvoices,
    googleDrivePublishLink,
    handleAddInvoiceAdjustment,
    handleDeleteInvoice,
    handleDownloadInvoicePdf,
    handleInvoiceReissue,
    handleInvoiceStatusChange,
    handlePublishInvoiceGoogleDrive,
    handleSendInvoiceEmail,
    invoices,
    invoiceSearchQuery,
    invoiceStatus,
    isInvoiceEditorOpen,
    issuedInvoiceCount,
    isInvoiceLoading,
    overdueInvoiceCount,
    resetInvoicesWorkspace,
    selectedInvoice,
    setAdjustmentAmount,
    setAdjustmentReason,
    setInvoices,
    setInvoiceStatus,
    setIsInvoiceLoading,
    setSelectedInvoiceId,
    setInvoiceSearchQuery,
    startInvoiceEdit,
  } = useInvoicesWorkspace({
    clientNamesById,
    onInvoiceDeleted: (invoice) => {
      setGigs((current) =>
        current.map((gig) =>
          gig.invoiceId === invoice.id
            ? {
                ...gig,
                invoiceId: null,
                invoicedAt: null,
                isInvoiced: false,
              }
            : gig
        )
      )
    },
  })
  const {
    clearQuickReceiptDialog,
    closeQuickReceiptPrompt,
    goToQuickReceiptGig,
    handleQuickReceiptFile,
    isQuickReceiptSaving,
    pendingReceiptFile,
    quickReceiptAmount,
    quickReceiptCandidates,
    quickReceiptDescription,
    quickReceiptDraft,
    quickReceiptSelectedGigId,
    quickReceiptStatus,
    savePendingReceiptToSelectedGig,
    saveQuickReceiptDetails,
    setQuickReceiptAmount,
    setQuickReceiptDescription,
    setQuickReceiptSelectedGigId,
  } = useQuickReceipt({
    getGigById: (gigId) => gigsById.get(gigId),
    onMergeSavedGig: (gig) => mergeSavedGig(gig),
    onOpenReceiptDraft: (gig) => openGigReceiptDraft(gig),
    onSelectGig: setSelectedGigId,
    onSessionExpired: (message) => {
      setIsAuthenticated(false)
      setAuthUser(null)
      setIsApiConnected(false)
      setStatus(message)
    },
    setGigStatus,
  })
  const {
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
  } = useUserSettings({
    authUser,
    onCloseProfileMenu: () => setIsProfileMenuOpen(false),
    onSessionExpired: (message) => {
      setIsAuthenticated(false)
      setAuthUser(null)
      setIsApiConnected(false)
      setStatus(message)
    },
    setAuthUser,
  })
  const {
    applySellerProfile,
    closeSellerProfile,
    handleSellerProfileSubmit,
    isSellerProfileOpen,
    isSellerProfileSaving,
    openSellerProfile: openSellerProfileModal,
    resetSellerProfile,
    sellerProfile,
    sellerProfileForm,
    sellerProfileStatus,
    updateSellerProfileField,
  } = useSellerProfile({
    onCloseProfileMenu: () => setIsProfileMenuOpen(false),
    onSessionExpired: (message) => {
      setIsAuthenticated(false)
      setAuthUser(null)
      setIsApiConnected(false)
      setStatus(message)
    },
  })

  useEffect(() => {
    if (!isAdmin && activeSection === 'admin') {
      setActiveSection('gigs')
    }
  }, [activeSection, isAdmin])

  useEffect(() => {
    if (!isProfileMenuOpen) {
      return
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (!profileMenuRef.current?.contains(event.target as Node)) {
        setIsProfileMenuOpen(false)
      }
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsProfileMenuOpen(false)
      }
    }

    document.addEventListener('mousedown', handlePointerDown)
    document.addEventListener('keydown', handleEscape)

    return () => {
      document.removeEventListener('mousedown', handlePointerDown)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [isProfileMenuOpen])

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')

    const applyTheme = () => {
      const nextResolvedTheme =
        themePreference === 'system'
          ? mediaQuery.matches
            ? 'dark'
            : 'light'
          : themePreference

      document.documentElement.setAttribute('data-theme', nextResolvedTheme)
    }

    applyTheme()
    window.localStorage.setItem(themeStorageKey, themePreference)

    if (themePreference !== 'system') {
      return
    }

    const handleSystemThemeChange = () => {
      applyTheme()
    }

    mediaQuery.addEventListener('change', handleSystemThemeChange)
    return () => {
      mediaQuery.removeEventListener('change', handleSystemThemeChange)
    }
  }, [themePreference])

  const resetAdminWorkspace = () => {
    setAdminUsers([])
    setSelectedAdminUserId('')
    setAdminSearchQuery('')
    setIsAdminEditorOpen(false)
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
    setAdminStatus(defaultAdminStatus)
  }

  useEffect(() => {
    let ignore = false

    const resetSignedInState = () => {
      resetClientsWorkspace()
      resetGigsWorkspace()
      setMonthlyInvoiceMonth(getCurrentMonthValue())
      setMonthlyInvoiceStatus('')
      resetInvoicesWorkspace()
      clearQuickReceiptDialog()
      resetUserSettings()
      resetSellerProfile()
      resetAdminWorkspace()
    }

    const loadAdminUsers = async () => {
      const response = await fetchWithSession(buildApiUrl('/admin/users'))
      if (response.status === 401) {
        throw new Error('SESSION_EXPIRED')
      }

      if (response.status === 403) {
        return
      }

      if (!response.ok) {
        throw new Error('Unable to load user enrolments.')
      }

      const users = (await response.json()) as AdminUser[]
      if (ignore) {
        return
      }

      setAdminUsers(users)
      setSelectedAdminUserId((current) => current || users[0]?.id || '')
      setAdminStatus(
        users.length > 0
          ? 'Manage access, roles and account status.'
          : 'No users added yet. Add the first one below.'
      )
    }

    const loadApp = async () => {
      setIsCheckingSession(true)

      try {
        const sessionResponse = await fetchWithSession(buildApiUrl('/auth/me'))
        if (sessionResponse.status === 401) {
          if (ignore) {
            return
          }

          setIsAuthenticated(false)
          setAuthUser(null)
          setIsApiConnected(false)
          resetSignedInState()
          setShouldCloseBrowserNotice(false)
          setStatus('Sign in to access Glovelly.')
          return
        }

        if (!sessionResponse.ok) {
          throw new Error('Unable to verify your session.')
        }

        const user = (await sessionResponse.json()) as AuthUser
        if (ignore) {
          return
        }

        setIsAuthenticated(true)
        setAuthUser(user)
        setIsLoading(true)

        const [clientsResponse, gigsResponse, invoicesResponse, sellerProfileResponse] = await Promise.all([
          fetchWithSession(buildApiUrl('/clients')),
          fetchWithSession(buildApiUrl('/gigs')),
          fetchWithSession(buildApiUrl('/invoices')),
          fetchWithSession(buildApiUrl('/seller-profile')),
        ])

        if (
          clientsResponse.status === 401 ||
          gigsResponse.status === 401 ||
          invoicesResponse.status === 401 ||
          sellerProfileResponse.status === 401
        ) {
          setIsAuthenticated(false)
          setAuthUser(null)
          setIsApiConnected(false)
          resetSignedInState()
          setStatus('Your session expired. Sign in again to keep working.')
          return
        }

        if (!clientsResponse.ok) {
          throw new Error('Unable to load clients.')
        }

        if (!gigsResponse.ok) {
          throw new Error('Unable to load gigs.')
        }

        if (!invoicesResponse.ok) {
          throw new Error('Unable to load invoices.')
        }

        if (!sellerProfileResponse.ok) {
          throw new Error('Unable to load seller profile.')
        }

        const data = (await clientsResponse.json()) as Client[]
        const gigData = (await gigsResponse.json()) as Gig[]
        const invoiceData = (await invoicesResponse.json()) as Invoice[]
        const sellerProfileData = (await sellerProfileResponse.json()) as SellerProfile
        if (ignore) {
          return
        }

        applyClients(data)
        applyGigs(gigData)
        applyInvoices(invoiceData)
        applySellerProfile(sellerProfileData)
        setIsApiConnected(true)
        setShouldCloseBrowserNotice(false)
        setStatus(
          data.length > 0
            ? `Signed in as ${user.email}.`
            : `Signed in as ${user.email}. No clients yet.`
        )

        if (user.role === 'Admin') {
          try {
            await loadAdminUsers()
          } catch {
            if (!ignore) {
              setAdminUsers([])
              setSelectedAdminUserId('')
              setAdminStatus('The admin area could not be loaded right now.')
            }
          }
        }
      } catch (error) {
        if (!ignore) {
          if (error instanceof Error && error.message === 'SESSION_EXPIRED') {
            setIsAuthenticated(false)
            setAuthUser(null)
            setIsApiConnected(false)
            resetSignedInState()
            setStatus('Your session expired. Sign in again to keep working.')
          } else {
            setIsApiConnected(false)
            resetClientsWorkspace()
            setAdminUsers([])
            setSelectedAdminUserId('')
            setShouldCloseBrowserNotice(false)
            setStatus('We could not load your workspace right now. Please try again.')
          }
        }
      } finally {
        if (!ignore) {
          setIsLoading(false)
          setIsCheckingSession(false)
        }
      }
    }

    void loadApp()

    return () => {
      ignore = true
    }
  }, [
    applyClients,
    applyGigs,
    applyInvoices,
    applySellerProfile,
    clearQuickReceiptDialog,
    resetClientsWorkspace,
    resetGigsWorkspace,
    resetInvoicesWorkspace,
    resetSellerProfile,
    resetUserSettings,
  ])

  const adminUsersById = useMemo(
    () => new Map(adminUsers.map((user) => [user.id, user])),
    [adminUsers]
  )
  const filteredAdminUsers = useMemo(() => {
    const query = deferredAdminSearchQuery.trim().toLowerCase()
    if (!query) {
      return adminUsers
    }

    return adminUsers.filter((user) =>
      [user.email, user.displayName ?? '', user.role, user.googleSubject ?? '']
        .join(' ')
        .toLowerCase()
        .includes(query)
    )
  }, [adminUsers, deferredAdminSearchQuery])

  const selectedAdminUser =
    adminUsersById.get(selectedAdminUserId) ?? filteredAdminUsers[0] ?? null

  const monthlyInvoiceEligibleGigs = useMemo(() => {
    if (!selectedClient || !monthlyInvoiceMonth) {
      return []
    }

    return gigs
      .filter(
        (gig) =>
          gig.clientId === selectedClient.id &&
          !gig.isInvoiced &&
          gig.date.startsWith(`${monthlyInvoiceMonth}-`) &&
          gig.status !== 'Cancelled'
      )
      .sort((left, right) => left.date.localeCompare(right.date))
  }, [gigs, monthlyInvoiceMonth, selectedClient])

  const isMonthlyInvoiceReady =
    Boolean(selectedClient) &&
    Boolean(monthlyInvoiceMonth) &&
    monthlyInvoiceEligibleGigs.length > 0

  const monthlyInvoiceHelperText = monthlyInvoiceStatus || (() => {
    if (!selectedClient) {
      return 'Select a client to review monthly invoice eligibility.'
    }

    if (!monthlyInvoiceMonth) {
      return 'Choose a month to check which gigs are ready to invoice.'
    }

    if (monthlyInvoiceEligibleGigs.length === 0) {
      return `No eligible gigs found for ${selectedClient.name} in ${monthlyInvoiceMonth}.`
    }

    return `${monthlyInvoiceEligibleGigs.length} eligible gig(s) ready to invoice for ${selectedClient.name} in ${monthlyInvoiceMonth}.`
  })()

  const openSelectedGigInvoice = () => {
    if (!selectedGig?.invoiceId) {
      return
    }

    setSelectedInvoiceId(selectedGig.invoiceId)
    setActiveSection('invoices')
  }

  useEffect(() => {
    if (selectedAdminUser) {
      setSelectedAdminUserId(selectedAdminUser.id)
    }
  }, [selectedAdminUser])

  useEffect(() => {
    setMonthlyInvoiceStatus('')
  }, [selectedClient?.id, monthlyInvoiceMonth])

  const activeUsersCount = adminUsers.filter((user) => user.isActive).length
  const totalAdmins = adminUsers.filter((user) => user.role === 'Admin').length
  const outstandingInvoiceCount = issuedInvoiceCount + overdueInvoiceCount
  const sellerProfileMissingLabels = useMemo(() => {
    const labels: Record<string, string> = {
      sellerName: 'seller name',
      addressLine1: 'address line 1',
      city: 'city',
      country: 'country',
      accountName: 'account name',
      sortCode: 'sort code',
      accountNumber: 'account number',
    }

    return sellerProfile.missingFields.map((field) => labels[field] ?? field)
  }, [sellerProfile.missingFields])
  const sellerProfileNotice = sellerProfile.isInvoiceReady
    ? ' Seller profile is invoice-ready, so PDFs will include your sender and payment details.'
    : sellerProfile.isConfigured
      ? ` Seller profile is incomplete. Missing: ${sellerProfileMissingLabels.join(', ')}. You can still generate invoices, but some sender details may be omitted.`
      : ' Seller profile is not set up yet. You can still generate invoices, but sender and payment details will be missing until you configure them.'

  const navigationItems: AppNavigationItem[] = [
    {
      id: 'clients',
      label: 'Clients',
      eyebrow: 'Live',
      description: 'Booking contacts, billing details and client records.',
    },
    {
      id: 'gigs',
      label: 'Gigs',
      eyebrow: 'Live',
      description: 'Bookings, delivery status and the first invoicing-ready gig records.',
    },
    {
      id: 'invoices',
      label: 'Invoices',
      eyebrow: 'Generated',
      description: 'One-off invoices, line items and downloadable PDFs.',
    },
    ...(isAdmin
      ? [
          {
            id: 'admin' as const,
            label: 'Admin',
            eyebrow: 'Restricted',
            description: 'Manage access, roles and account status.',
          },
        ]
      : []),
  ]

  const currentSection = navigationItems.find((item) => item.id === activeSection)
  const headerMetrics: HeaderMetric[] = [
    {
      value: upcomingGigCount,
      label: 'upcoming gigs',
    },
    {
      value: uninvoicedGigCount,
      label: 'uninvoiced gigs',
    },
    {
      value: outstandingInvoiceCount,
      label: 'outstanding invoices',
    },
    {
      value: draftInvoiceCount,
      label: 'draft invoices',
    },
  ]

  const startAdminCreate = () => {
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
    setIsAdminEditorOpen(true)
  }

  const startAdminEdit = () => {
    if (!selectedAdminUser) {
      return
    }

    setAdminMode('edit')
    setAdminForm({
      email: selectedAdminUser.email,
      displayName: selectedAdminUser.displayName ?? '',
      googleSubject: selectedAdminUser.googleSubject ?? '',
      role: selectedAdminUser.role === 'Admin' ? 'Admin' : 'User',
      isActive: selectedAdminUser.isActive,
    })
    setIsAdminEditorOpen(true)
  }

  const closeAdminEditor = () => {
    setIsAdminEditorOpen(false)
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
  }

  const updateAdminField = (
    field: keyof AdminUserForm,
    value: string | boolean
  ) => {
    setAdminForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const signIn = () => {
    const loginUrl = buildApiUrl(
      `/auth/login?returnUrl=${encodeURIComponent(buildReturnUrl())}`
    )
    window.location.assign(loginUrl)
  }

  const signOut = async () => {
    setIsLoading(true)
    setIsProfileMenuOpen(false)

    try {
      const response = await fetchWithSession(buildApiUrl('/auth/logout'), {
        method: 'POST',
      })

      if (!response.ok) {
        throw new Error('Unable to sign out.')
      }

      setIsAuthenticated(false)
      setAuthUser(null)
      setIsApiConnected(false)
      resetClientsWorkspace()
      resetGigsWorkspace()
      resetInvoicesWorkspace()
      resetSellerProfile()
      resetAdminWorkspace()
      setShouldCloseBrowserNotice(true)
      setStatus('Signed out successfully.')
    } catch {
      setStatus('Unable to sign out right now.')
    } finally {
      setIsLoading(false)
    }
  }

  const handleThemePreferenceChange = (nextPreference: ThemePreference) => {
    setThemePreference(nextPreference)
  }

  const openSellerProfile = () => {
    openSellerProfileModal(sellerProfileNotice)
  }

  const handleAdminSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const payload: AdminUserForm = {
      email: adminForm.email.trim(),
      displayName: adminForm.displayName.trim(),
      googleSubject: adminForm.googleSubject.trim(),
      role: adminForm.role,
      isActive: adminForm.isActive,
    }

    if (!payload.email) {
      setAdminStatus('Email is required.')
      return
    }

    setIsAdminLoading(true)

    try {
      const isEdit = adminMode === 'edit' && selectedAdminUser
      const endpoint = isEdit
        ? buildApiUrl(`/admin/users/${selectedAdminUser.id}`)
        : buildApiUrl('/admin/users')

      const response = await fetchWithSession(endpoint, {
        method: isEdit ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      })

      if (response.status === 401) {
        setIsAuthenticated(false)
        setAuthUser(null)
        setIsApiConnected(false)
        setStatus('Your session expired. Sign in again to keep managing access.')
        return
      }

      if (response.status === 403) {
        setAdminStatus('Administrator access is required to manage users.')
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to save enrolment.')
      }

      const savedUser = (await response.json()) as AdminUser

      setAdminUsers((current) => {
        if (isEdit) {
          return current.map((user) => (user.id === savedUser.id ? savedUser : user))
        }

        return [savedUser, ...current]
      })

      setSelectedAdminUserId(savedUser.id)
      setAdminMode('edit')
      setAdminStatus(isEdit ? 'User updated.' : 'User added.')
      setIsAdminEditorOpen(false)
    } catch (error) {
      setAdminStatus(
        error instanceof Error ? error.message : 'Unable to save right now.'
      )
    } finally {
      setIsAdminLoading(false)
    }
  }

  const handleGenerateInvoice = async () => {
    if (selectedGigs.length > 0) {
      const distinctClientIds = new Set(selectedGigs.map((gig) => gig.clientId))
      if (distinctClientIds.size > 1) {
        setGigStatus('Selected gigs must all belong to the same client.')
        return
      }

      const alreadyInvoicedGig = selectedGigs.find((gig) => gig.isInvoiced)
      if (alreadyInvoicedGig) {
        setGigStatus(`"${alreadyInvoicedGig.title}" is already linked to an invoice.`)
        return
      }

      setIsInvoiceLoading(true)
      setGigStatus(`Generating invoice for ${selectedGigs.length} selected gig(s)...`)

      try {
        const response = await fetchWithSession(buildApiUrl('/gigs/generate-invoice'), {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            gigIds: selectedGigs.map((gig) => gig.id),
          }),
        })

        if (response.status === 409) {
          const conflict = (await response.json()) as { message?: string }
          throw new Error(
            conflict.message ?? 'Selected gigs must all be uninvoiced before generating.'
          )
        }

        if (!response.ok) {
          const problem = await parseProblemDetails(response)
          const validationMessages = problem?.errors
            ? Object.values(problem.errors).flat().join(' ')
            : problem?.detail ?? problem?.title
          throw new Error(validationMessages || 'Unable to generate invoice.')
        }

        const generatedInvoice = (await response.json()) as Invoice
        const nowIso = new Date().toISOString()
        const selectedIds = new Set(selectedGigs.map((gig) => gig.id))

        setInvoices((current) => [
          generatedInvoice,
          ...current.filter((invoice) => invoice.id !== generatedInvoice.id),
        ])
        setSelectedInvoiceId(generatedInvoice.id)
        setGigs((current) =>
          current.map((gig) =>
            selectedIds.has(gig.id)
              ? {
                  ...gig,
                  invoiceId: generatedInvoice.id,
                  invoicedAt: gig.invoicedAt ?? nowIso,
                  isInvoiced: true,
                }
              : gig
          )
        )
        setSelectedGigIds([])
        setGigStatus(
          `Invoice ${generatedInvoice.invoiceNumber} generated from ${selectedGigs.length} selected gig(s).`
        )
        setInvoiceStatus(
          sellerProfile.isInvoiceReady
            ? `Invoice ${generatedInvoice.invoiceNumber} is ready for review.`
            : `Invoice ${generatedInvoice.invoiceNumber} is ready for review. ${sellerProfileNotice}`
        )
        setActiveSection('invoices')
      } catch (error) {
        setGigStatus(error instanceof Error ? error.message : 'Unable to generate invoice.')
      } finally {
        setIsInvoiceLoading(false)
      }

      return
    }

    if (!selectedGig) {
      setGigStatus('Select one or more gigs first.')
      return
    }

    setIsInvoiceLoading(true)
    setGigStatus('Generating invoice and PDF...')

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/gigs/${selectedGig.id}/generate-invoice`),
        {
          method: 'POST',
        }
      )

      if (response.status === 409) {
        const conflict = (await response.json()) as {
          message?: string
          invoiceId?: string
        }

        const existingInvoiceId = conflict.invoiceId ?? selectedGig.invoiceId
        if (existingInvoiceId) {
          setSelectedInvoiceId(existingInvoiceId)
          setActiveSection('invoices')
        }

        setGigStatus(conflict.message ?? 'This gig has already been invoiced.')
        setInvoiceStatus('This gig already has an invoice. Reviewing the existing record.')
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        throw new Error(problem?.detail || problem?.title || 'Unable to generate invoice.')
      }

      const generatedInvoice = (await response.json()) as Invoice

      setInvoices((current) => [generatedInvoice, ...current.filter((invoice) => invoice.id !== generatedInvoice.id)])
      setSelectedInvoiceId(generatedInvoice.id)
      setGigs((current) =>
        current.map((gig) =>
          gig.id === selectedGig.id
            ? {
                ...gig,
                invoiceId: generatedInvoice.id,
                invoicedAt: new Date().toISOString(),
                isInvoiced: true,
              }
            : gig
        )
      )
      setGigStatus('Invoice generated and linked to this gig.')
      setInvoiceStatus(
        sellerProfile.isInvoiceReady
          ? 'New invoice generated from the selected gig.'
          : `New invoice generated from the selected gig. ${sellerProfileNotice}`
      )
      setActiveSection('invoices')
    } catch (error) {
      setGigStatus(error instanceof Error ? error.message : 'Unable to generate invoice.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleGenerateMonthlyInvoice = async () => {
    if (!selectedClient) {
      setMonthlyInvoiceStatus('Choose a client before running a monthly invoice.')
      return
    }

    if (!monthlyInvoiceMonth) {
      setMonthlyInvoiceStatus('Choose a month for the monthly invoice run.')
      return
    }

    const gigsToInvoice = monthlyInvoiceEligibleGigs

    if (gigsToInvoice.length === 0) {
      setMonthlyInvoiceStatus(
        `No eligible gigs found for ${selectedClient.name} in ${monthlyInvoiceMonth}.`
      )
      return
    }

    setIsInvoiceLoading(true)
    setMonthlyInvoiceStatus(
      `Creating monthly invoice for ${gigsToInvoice.length} gig(s)...`
    )

    try {
      const createInvoiceResponse = await fetchWithSession(buildApiUrl('/invoices'), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          invoiceNumber: buildMonthlyInvoiceNumber(monthlyInvoiceMonth, invoices.length + 1),
          clientId: selectedClient.id,
          status: 'Draft',
          description: `Monthly invoice for ${monthlyInvoiceMonth}.`,
        }),
      })

      if (!createInvoiceResponse.ok) {
        const problem = await parseProblemDetails(createInvoiceResponse)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title
        throw new Error(validationMessages || 'Unable to create monthly invoice.')
      }

      const createdInvoice = (await createInvoiceResponse.json()) as Invoice

      for (const gig of gigsToInvoice) {
        const linkGigResponse = await fetchWithSession(buildApiUrl(`/gigs/${gig.id}`), {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            clientId: gig.clientId,
            title: gig.title,
            date: gig.date,
            venue: gig.venue,
            fee: gig.fee,
            travelMiles: gig.travelMiles,
            passengerCount: gig.passengerCount,
            notes: gig.notes,
            wasDriving: gig.wasDriving,
            status: gig.status,
            invoiceId: createdInvoice.id,
            expenses: gig.expenses
              .slice()
              .sort((left, right) => left.sortOrder - right.sortOrder)
              .map((expense, index) => ({
                sortOrder: index + 1,
                description: expense.description,
                amount: expense.amount,
              })),
            invoicedAt: gig.invoicedAt,
          }),
        })

        if (!linkGigResponse.ok) {
          const problem = await parseProblemDetails(linkGigResponse)
          const validationMessages = problem?.errors
            ? Object.values(problem.errors).flat().join(' ')
            : problem?.detail ?? problem?.title
          throw new Error(
            validationMessages || `Unable to link ${gig.title} to the monthly invoice.`
          )
        }
      }

      const redraftInvoiceResponse = await fetchWithSession(
        buildApiUrl(`/invoices/${createdInvoice.id}/redraft`),
        {
          method: 'POST',
        }
      )

      if (!redraftInvoiceResponse.ok) {
        const problem = await parseProblemDetails(redraftInvoiceResponse)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title
        throw new Error(validationMessages || 'Unable to prepare the monthly invoice PDF.')
      }

      const redraftedInvoice = (await redraftInvoiceResponse.json()) as Invoice
      const hydratedInvoiceResponse = await fetchWithSession(
        buildApiUrl(`/invoices/${createdInvoice.id}`)
      )

      const updatedInvoice = hydratedInvoiceResponse.ok
        ? ((await hydratedInvoiceResponse.json()) as Invoice)
        : redraftedInvoice

      setInvoices((current) => [
        updatedInvoice,
        ...current.filter((invoice) => invoice.id !== updatedInvoice.id),
      ])
      setSelectedInvoiceId(updatedInvoice.id)
      setGigs((current) =>
        current.map((gig) =>
          gigsToInvoice.some((value) => value.id === gig.id)
            ? {
                ...gig,
                invoiceId: updatedInvoice.id,
                isInvoiced: true,
                invoicedAt: gig.invoicedAt ?? new Date().toISOString(),
              }
            : gig
        )
      )
      setGigStatus(
        `Monthly invoice ${updatedInvoice.invoiceNumber} created for ${gigsToInvoice.length} gig(s).`
      )
      setMonthlyInvoiceStatus(
        `Monthly invoice ${updatedInvoice.invoiceNumber} created for ${gigsToInvoice.length} gig(s).`
      )
      setInvoiceStatus(`Monthly invoice ${updatedInvoice.invoiceNumber} is ready for review.`)
      setActiveSection('invoices')
    } catch (error) {
      setMonthlyInvoiceStatus(
        error instanceof Error ? error.message : 'Unable to generate monthly invoice.'
      )
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  if (isCheckingSession) {
    return <SessionCheckingScreen appMetadata={appMetadata} status={status} />
  }

  if (!isAuthenticated) {
    return (
      <SignInScreen
        appMetadata={appMetadata}
        onSignIn={signIn}
        shouldCloseBrowserNotice={shouldCloseBrowserNotice}
        status={status}
      />
    )
  }
  const currentSectionContent =
    activeSection === 'clients' ? (
      <ClientsSection
        filteredClients={filteredClients}
        form={form}
        isApiConnected={isApiConnected}
        isEditorOpen={isClientEditorOpen}
        isMonthlyInvoiceReady={isMonthlyInvoiceReady}
        isInvoiceLoading={isInvoiceLoading}
        isLoading={isLoading}
        monthlyInvoiceHelperText={monthlyInvoiceHelperText}
        monthlyInvoiceMonth={monthlyInvoiceMonth}
        mode={mode}
        onCloseEditor={closeClientEditor}
        onDelete={handleDelete}
        onGenerateMonthlyInvoice={handleGenerateMonthlyInvoice}
        onMonthlyInvoiceMonthChange={setMonthlyInvoiceMonth}
        onOpenClientSettings={openClientSettings}
        onResetForm={startCreating}
        onSearchQueryChange={setSearchQuery}
        onSelectClient={setSelectedClientId}
        onStartEditing={startEditing}
        onSubmit={handleSubmit}
        onUpdateAddressField={updateAddressField}
        onUpdateField={updateField}
        searchQuery={searchQuery}
        selectedClient={selectedClient}
        status={status}
      />
    ) : activeSection === 'admin' && isAdmin ? (
      <AdminSection
        adminForm={adminForm}
        isEditorOpen={isAdminEditorOpen}
        adminMode={adminMode}
        adminSearchQuery={adminSearchQuery}
        adminStatus={adminStatus}
        adminUsers={adminUsers}
        activeUsersCount={activeUsersCount}
        filteredAdminUsers={filteredAdminUsers}
        isAdminLoading={isAdminLoading}
        onCloseEditor={closeAdminEditor}
        onResetForm={startAdminCreate}
        onSearchQueryChange={setAdminSearchQuery}
        onSelectUser={setSelectedAdminUserId}
        onStartEditing={startAdminEdit}
        onSubmit={handleAdminSubmit}
        onUpdateField={updateAdminField}
        selectedAdminUser={selectedAdminUser}
        totalAdmins={totalAdmins}
      />
    ) : activeSection === 'gigs' ? (
      <GigsSection
        clientNamesById={clientNamesById}
        clients={clients}
        completedGigCount={completedGigCount}
        filteredGigs={filteredGigs}
        gigExpenseAmount={gigExpenseAmount}
        gigExpenseDescription={gigExpenseDescription}
        gigForm={gigForm}
        isEditorOpen={isGigEditorOpen}
        gigMode={gigMode}
        gigSearchQuery={gigSearchQuery}
        gigStatus={gigStatus}
        gigs={gigs}
        isGigLoading={isGigLoading}
        isInvoiceLoading={isInvoiceLoading}
        onAddGigExpense={handleAddGigExpense}
        onCloseEditor={closeGigEditor}
        onExpenseAmountChange={setGigExpenseAmount}
        onExpenseDescriptionChange={setGigExpenseDescription}
        onGenerateInvoice={handleGenerateInvoice}
        onDownloadExpenseAttachment={downloadExpenseAttachment}
        onOpenLinkedInvoice={openSelectedGigInvoice}
        onOpenSellerProfile={openSellerProfile}
        onUploadExpenseAttachment={uploadExpenseAttachment}
        onDeleteExpenseAttachment={deleteExpenseAttachment}
        onRemoveGigExpense={removeGigExpense}
        onResetForm={startGigCreate}
        onSearchQueryChange={setGigSearchQuery}
        onSelectGig={setSelectedGigId}
        onToggleGigSelection={handleToggleGigSelection}
        onStartEditing={startGigEdit}
        onSubmit={handleGigSubmit}
        onUpdateGigExpenseField={updateGigExpenseField}
        onUpdateGigField={updateGigField}
        plannedGigCount={plannedGigCount}
        sellerProfile={sellerProfile}
        sellerProfileNotice={sellerProfileNotice}
        selectedGig={selectedGig}
        selectedGigIds={selectedGigIds}
        selectedGigs={selectedGigs}
      />
    ) : (
      <InvoicesSection
        adjustmentAmount={adjustmentAmount}
        adjustmentReason={adjustmentReason}
        clientNamesById={clientNamesById}
        draftInvoiceCount={draftInvoiceCount}
        filteredInvoices={filteredInvoices}
        isEditorOpen={isInvoiceEditorOpen}
        invoiceSearchQuery={invoiceSearchQuery}
        invoiceStatus={invoiceStatus}
        googleDrivePublishLink={
          invoiceStatus.startsWith('Uploaded ') ? googleDrivePublishLink : null
        }
        invoices={invoices}
        issuedInvoiceCount={issuedInvoiceCount}
        isGoogleDriveConnected={authUser?.isGoogleDriveConnected ?? false}
        isInvoiceLoading={isInvoiceLoading}
        isSellerProfileConfigured={sellerProfile.isConfigured}
        onAdjustmentAmountChange={setAdjustmentAmount}
        onAdjustmentReasonChange={setAdjustmentReason}
        onAddAdjustment={handleAddInvoiceAdjustment}
        onCloseEditor={closeInvoiceEditor}
        onDeleteInvoice={handleDeleteInvoice}
        onDownloadPdf={handleDownloadInvoicePdf}
        onInvoiceStatusChange={handleInvoiceStatusChange}
        onOpenSellerProfile={openSellerProfile}
        onPublishGoogleDrive={handlePublishInvoiceGoogleDrive}
        onReissue={handleInvoiceReissue}
        onSendEmail={handleSendInvoiceEmail}
        onSearchQueryChange={setInvoiceSearchQuery}
        onSelectInvoice={setSelectedInvoiceId}
        onStartEditing={startInvoiceEdit}
        sellerProfileNotice={sellerProfileNotice}
        selectedInvoice={selectedInvoice}
      />
    )

  return (
    <AppShell
      activeSection={activeSection}
      appMetadata={appMetadata}
      authUser={authUser}
      currentSection={currentSection}
      currentSectionContent={currentSectionContent}
      headerMetrics={headerMetrics}
      isAdmin={isAdmin}
      isAdminLoading={isAdminLoading}
      isGigLoading={isGigLoading}
      isLoading={isLoading}
      isProfileMenuOpen={isProfileMenuOpen}
      isQuickReceiptSaving={isQuickReceiptSaving}
      isSellerProfileSaving={isSellerProfileSaving}
      isUserSettingsSaving={isUserSettingsSaving}
      navigationItems={navigationItems}
      onOpenSellerProfile={openSellerProfile}
      onOpenUserSettings={openUserSettings}
      onProfileMenuToggle={() => setIsProfileMenuOpen((current) => !current)}
      onQuickReceiptFile={handleQuickReceiptFile}
      onSectionChange={setActiveSection}
      onSignOut={signOut}
      onThemePreferenceChange={handleThemePreferenceChange}
      profileMenuRef={profileMenuRef}
      sellerProfile={sellerProfile}
      themePreference={themePreference}
    >
      <UserSettingsModal
        form={userSettingsForm}
        invoiceEmailSubjectPreview={buildInvoiceEmailSubjectPreview(
          userSettingsForm.invoiceEmailSubjectPattern,
          null
        )}
        invoiceFilenamePreview={buildInvoiceFilenamePreview(
          userSettingsForm.invoiceFilenamePattern,
          null
        )}
        invoiceFilenameTokens={invoiceFilenameTokens}
        isGoogleDriveConnected={authUser?.isGoogleDriveConnected ?? false}
        isOpen={isUserSettingsOpen}
        isSaving={isUserSettingsSaving}
        isGoogleDriveConnectDisabled={isLoading || isAdminLoading}
        onClose={closeUserSettings}
        onConnectGoogleDrive={connectGoogleDrive}
        onSubmit={handleUserSettingsSubmit}
        onUpdateField={updateUserSettingsField}
        status={userSettingsStatus}
      />

      <SellerProfileModal
        form={sellerProfileForm}
        isOpen={isSellerProfileOpen}
        isSaving={isSellerProfileSaving}
        onClose={closeSellerProfile}
        onSubmit={handleSellerProfileSubmit}
        onUpdateField={updateSellerProfileField}
        profile={sellerProfile}
        status={sellerProfileStatus}
      />

      <QuickReceiptModal
        amount={quickReceiptAmount}
        candidates={quickReceiptCandidates}
        clientNamesById={clientNamesById}
        description={quickReceiptDescription}
        draft={quickReceiptDraft}
        isSaving={isQuickReceiptSaving}
        onAmountChange={setQuickReceiptAmount}
        onClose={closeQuickReceiptPrompt}
        onDescriptionChange={setQuickReceiptDescription}
        onGoToGig={goToQuickReceiptGig}
        onSaveDetails={saveQuickReceiptDetails}
        onSaveDraft={savePendingReceiptToSelectedGig}
        onSelectedGigChange={setQuickReceiptSelectedGigId}
        pendingFile={pendingReceiptFile}
        selectedGigId={quickReceiptSelectedGigId}
        status={quickReceiptStatus}
      />

      <ClientSettingsModal
        authUser={authUser}
        form={clientSettingsForm}
        invoiceEmailSubjectPreview={buildInvoiceEmailSubjectPreview(
          clientSettingsForm.invoiceEmailSubjectPattern ||
            authUser?.invoiceEmailSubjectPattern,
          selectedClient?.name
        )}
        invoiceFilenamePreview={buildInvoiceFilenamePreview(
          clientSettingsForm.invoiceFilenamePattern || authUser?.invoiceFilenamePattern,
          selectedClient?.name
        )}
        invoiceFilenameTokens={invoiceFilenameTokens}
        isOpen={isClientSettingsOpen}
        isSaving={isClientSettingsSaving}
        onClose={closeClientSettings}
        onSubmit={handleClientSettingsSubmit}
        onUpdateField={updateClientSettingsField}
        selectedClient={selectedClient}
        status={clientSettingsStatus}
      />
    </AppShell>
  )
}

export default App
