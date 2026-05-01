import { useDeferredValue, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import {
  AdminSection,
  ClientSettingsModal,
  ClientsSection,
  GigsSection,
  InvoicesSection,
  SellerProfileModal,
  SessionCheckingScreen,
  SignInScreen,
  UserSettingsModal,
} from './AppSections'
import {
  buildApiUrl,
  buildInvoiceFilenamePreview,
  buildReturnUrl,
  defaultAdminStatus,
  defaultGigStatus,
  defaultInvoiceStatus,
  emptyAdminForm,
  emptyClientSettingsForm,
  emptyForm,
  emptyGigForm,
  emptySellerProfileForm,
  emptyUserSettingsForm,
  invoiceFilenameTokens,
  fetchWithSession,
  formatCurrency,
  formatBuildMetadata,
  formatDateTime,
  getStoredThemePreference,
  parseProblemDetails,
  themeStorageKey,
  toGigExpenseForm,
} from './appShared'
import type {
  Address,
  AdminUser,
  AdminUserForm,
  AppMetadata,
  AppSection,
  AuthUser,
  Client,
  ClientForm,
  ClientSettingsForm,
  Gig,
  GigExpenseForm,
  GigForm,
  Invoice,
  InvoiceStatus,
  SellerProfile,
  SellerProfileForm,
  ThemePreference,
  UserSettingsForm,
} from './appShared'
import './App.css'

function getCurrentMonthValue() {
  return new Date().toISOString().slice(0, 7)
}

function buildMonthlyInvoiceNumber(month: string, sequence: number) {
  return `GLV-${month.replace('-', '')}-${String(sequence).padStart(3, '0')}`
}


function toSellerProfileForm(profile: SellerProfile): SellerProfileForm {
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

type AppProps = {
  appMetadata: AppMetadata
}

function App({ appMetadata }: AppProps) {
  const [activeSection, setActiveSection] = useState<AppSection>('gigs')
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [searchQuery, setSearchQuery] = useState('')
  const [isClientEditorOpen, setIsClientEditorOpen] = useState(false)
  const [mode, setMode] = useState<'create' | 'edit'>('create')
  const [form, setForm] = useState<ClientForm>(emptyForm)
  const [isClientSettingsOpen, setIsClientSettingsOpen] = useState(false)
  const [clientSettingsForm, setClientSettingsForm] =
    useState<ClientSettingsForm>(emptyClientSettingsForm)
  const [clientSettingsStatus, setClientSettingsStatus] =
    useState('Client-specific rates override your personal defaults when set.')
  const [isClientSettingsSaving, setIsClientSettingsSaving] = useState(false)
  const [status, setStatus] = useState('Checking your session...')
  const [isLoading, setIsLoading] = useState(false)
  const [isApiConnected, setIsApiConnected] = useState(false)
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [authUser, setAuthUser] = useState<AuthUser | null>(null)
  const [isCheckingSession, setIsCheckingSession] = useState(true)
  const [shouldCloseBrowserNotice, setShouldCloseBrowserNotice] = useState(false)
  const [isProfileMenuOpen, setIsProfileMenuOpen] = useState(false)
  const [isUserSettingsOpen, setIsUserSettingsOpen] = useState(false)
  const profileMenuRef = useRef<HTMLDivElement | null>(null)
  const [themePreference, setThemePreference] =
    useState<ThemePreference>(getStoredThemePreference)
  const [userSettingsForm, setUserSettingsForm] =
    useState<UserSettingsForm>(emptyUserSettingsForm)
  const [userSettingsStatus, setUserSettingsStatus] =
    useState('Set the mileage defaults used when a client has no custom rates.')
  const [isUserSettingsSaving, setIsUserSettingsSaving] = useState(false)
  const [sellerProfile, setSellerProfile] = useState<SellerProfile>(emptySellerProfile)
  const [sellerProfileForm, setSellerProfileForm] =
    useState<SellerProfileForm>(emptySellerProfileForm)
  const [sellerProfileStatus, setSellerProfileStatus] = useState(
    'Add the seller details that should appear on your invoices.'
  )
  const [isSellerProfileOpen, setIsSellerProfileOpen] = useState(false)
  const [isSellerProfileSaving, setIsSellerProfileSaving] = useState(false)

  const [adminUsers, setAdminUsers] = useState<AdminUser[]>([])
  const [selectedAdminUserId, setSelectedAdminUserId] = useState<string>('')
  const [adminSearchQuery, setAdminSearchQuery] = useState('')
  const [isAdminEditorOpen, setIsAdminEditorOpen] = useState(false)
  const [adminMode, setAdminMode] = useState<'create' | 'edit'>('create')
  const [adminForm, setAdminForm] = useState<AdminUserForm>(emptyAdminForm)
  const [adminStatus, setAdminStatus] = useState(defaultAdminStatus)
  const [isAdminLoading, setIsAdminLoading] = useState(false)
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
  const [monthlyInvoiceMonth, setMonthlyInvoiceMonth] = useState(getCurrentMonthValue)
  const [monthlyInvoiceStatus, setMonthlyInvoiceStatus] = useState('')
  const [invoices, setInvoices] = useState<Invoice[]>([])
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<string>('')
  const [isInvoiceEditorOpen, setIsInvoiceEditorOpen] = useState(false)
  const [invoiceSearchQuery, setInvoiceSearchQuery] = useState('')
  const [invoiceStatus, setInvoiceStatus] = useState(defaultInvoiceStatus)
  const [isInvoiceLoading, setIsInvoiceLoading] = useState(false)
  const [adjustmentAmount, setAdjustmentAmount] = useState('')
  const [adjustmentReason, setAdjustmentReason] = useState('')
  const deferredSearchQuery = useDeferredValue(searchQuery)
  const deferredAdminSearchQuery = useDeferredValue(adminSearchQuery)
  const deferredGigSearchQuery = useDeferredValue(gigSearchQuery)
  const deferredInvoiceSearchQuery = useDeferredValue(invoiceSearchQuery)

  const isAdmin = authUser?.role === 'Admin'
  const profileDisplayName = authUser?.name?.trim() || authUser?.email || 'User'
  const profileImageUrl = authUser?.profileImageUrl?.trim() || ''
  const profileInitials = profileDisplayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')

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
      setClients([])
      setSelectedClientId('')
      setSearchQuery('')
      setIsClientEditorOpen(false)
      setMode('create')
      setForm(emptyForm())
      setGigs([])
      setSelectedGigId('')
      setGigSearchQuery('')
      setIsGigEditorOpen(false)
      setGigMode('create')
      setGigForm(emptyGigForm())
      setGigStatus(defaultGigStatus)
      setMonthlyInvoiceMonth(getCurrentMonthValue())
      setMonthlyInvoiceStatus('')
      setInvoices([])
      setSelectedInvoiceId('')
      setIsInvoiceEditorOpen(false)
      setInvoiceSearchQuery('')
      setInvoiceStatus(defaultInvoiceStatus)
      setIsClientSettingsOpen(false)
      setClientSettingsForm(emptyClientSettingsForm())
      setClientSettingsStatus(
        'Client-specific rates override your personal defaults when set.'
      )
      setIsUserSettingsOpen(false)
      setUserSettingsForm(emptyUserSettingsForm())
      setUserSettingsStatus(
        'Set the mileage defaults used when a client has no custom rates.'
      )
      setIsSellerProfileOpen(false)
      setSellerProfile(emptySellerProfile())
      setSellerProfileForm(emptySellerProfileForm())
      setSellerProfileStatus(
        'Add the seller details that should appear on your invoices.'
      )
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

        setClients(data)
        setSelectedClientId(data[0]?.id ?? '')
        setGigs(gigData)
        setSelectedGigId(gigData[0]?.id ?? '')
        setInvoices(invoiceData)
        setSelectedInvoiceId(invoiceData[0]?.id ?? '')
        setSellerProfile(sellerProfileData)
        setSellerProfileForm(toSellerProfileForm(sellerProfileData))
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
            setClients([])
            setSelectedClientId('')
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
  }, [])

  const clientNamesById = useMemo(
    () => new Map(clients.map((client) => [client.id, client.name])),
    [clients]
  )
  const clientsById = useMemo(
    () => new Map(clients.map((client) => [client.id, client])),
    [clients]
  )
  const adminUsersById = useMemo(
    () => new Map(adminUsers.map((user) => [user.id, user])),
    [adminUsers]
  )
  const gigsById = useMemo(() => new Map(gigs.map((gig) => [gig.id, gig])), [gigs])
  const invoicesById = useMemo(
    () => new Map(invoices.map((invoice) => [invoice.id, invoice])),
    [invoices]
  )

  const filteredClients = useMemo(() => {
    const query = deferredSearchQuery.trim().toLowerCase()
    if (!query) {
      return clients
    }

    return clients.filter((client) =>
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
  }, [clients, deferredSearchQuery])

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

  const filteredInvoices = useMemo(() => {
    const query = deferredInvoiceSearchQuery.trim().toLowerCase()
    const sortedInvoices = [...invoices].sort((left, right) => {
      const dateComparison = right.invoiceDate.localeCompare(left.invoiceDate)
      if (dateComparison !== 0) {
        return dateComparison
      }

      return left.invoiceNumber.localeCompare(right.invoiceNumber)
    })

    if (!query) {
      return sortedInvoices
    }

    return sortedInvoices.filter((invoice) => {
      const clientName = clientNamesById.get(invoice.clientId) ?? ''

      return [
        invoice.invoiceNumber,
        invoice.description ?? '',
        invoice.status,
        clientName,
      ]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
  }, [clientNamesById, deferredInvoiceSearchQuery, invoices])

  const selectedClient = clientsById.get(selectedClientId) ?? filteredClients[0] ?? null

  const selectedAdminUser =
    adminUsersById.get(selectedAdminUserId) ?? filteredAdminUsers[0] ?? null

  const selectedGig = gigsById.get(selectedGigId) ?? filteredGigs[0] ?? null

  const selectedGigs = useMemo(
    () => {
      const selectedGigIdSet = new Set(selectedGigIds)

      return gigs
        .filter((gig) => selectedGigIdSet.has(gig.id))
        .sort((left, right) => left.date.localeCompare(right.date))
    },
    [gigs, selectedGigIds]
  )

  const selectedInvoice =
    invoicesById.get(selectedInvoiceId) ?? filteredInvoices[0] ?? null

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
    if (selectedClient) {
      setSelectedClientId(selectedClient.id)
    }
  }, [selectedClient])

  useEffect(() => {
    if (selectedAdminUser) {
      setSelectedAdminUserId(selectedAdminUser.id)
    }
  }, [selectedAdminUser])

  useEffect(() => {
    if (selectedGig) {
      setSelectedGigId(selectedGig.id)
    }
  }, [selectedGig])

  useEffect(() => {
    setSelectedGigIds((current) => current.filter((gigId) => gigs.some((gig) => gig.id === gigId)))
  }, [gigs])

  useEffect(() => {
    if (selectedInvoice) {
      setSelectedInvoiceId(selectedInvoice.id)
    }
  }, [selectedInvoice])

  useEffect(() => {
    setMonthlyInvoiceStatus('')
  }, [selectedClient?.id, monthlyInvoiceMonth])

  useEffect(() => {
    if (gigForm.clientId || clients.length === 0) {
      return
    }

    setGigForm((current) => ({
      ...current,
      clientId: clients[0]?.id ?? '',
    }))
  }, [clients, gigForm.clientId])

  const activeUsersCount = adminUsers.filter((user) => user.isActive).length
  const totalAdmins = adminUsers.filter((user) => user.role === 'Admin').length
  const plannedGigCount = gigs.filter((gig) => gig.status === 'Confirmed').length
  const completedGigCount = gigs.filter((gig) => gig.status === 'Completed').length
  const upcomingGigCount = gigs.filter((gig) => gig.date >= new Date().toISOString().slice(0, 10)).length
  const uninvoicedGigCount = gigs.filter((gig) => !gig.isInvoiced && gig.status !== 'Cancelled').length
  const draftInvoiceCount = invoices.filter((invoice) => invoice.status === 'Draft').length
  const issuedInvoiceCount = invoices.filter((invoice) => invoice.status === 'Issued').length
  const overdueInvoiceCount = invoices.filter((invoice) => invoice.status === 'Overdue').length
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

  const navigationItems: Array<{
    id: AppSection
    label: string
    eyebrow: string
    description: string
    disabled?: boolean
  }> = [
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
  const headerMetrics = [
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

  const startCreating = () => {
    setMode('create')
    setForm(emptyForm())
    setIsClientEditorOpen(true)
  }

  const startEditing = () => {
    if (!selectedClient) {
      return
    }

    setMode('edit')
    setForm({
      name: selectedClient.name,
      email: selectedClient.email,
      billingAddress: {
        line1: selectedClient.billingAddress.line1 ?? '',
        line2: selectedClient.billingAddress.line2 ?? '',
        city: selectedClient.billingAddress.city ?? '',
        stateOrCounty: selectedClient.billingAddress.stateOrCounty ?? '',
        postalCode: selectedClient.billingAddress.postalCode ?? '',
        country: selectedClient.billingAddress.country ?? '',
      },
    })
    setIsClientEditorOpen(true)
  }

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
    setGigForm({
      clientId: selectedGig.clientId,
      title: selectedGig.title,
      date: selectedGig.date,
      venue: selectedGig.venue,
      fee: String(selectedGig.fee),
      notes: selectedGig.notes ?? '',
      wasDriving: selectedGig.wasDriving,
      status: selectedGig.status,
      expenses: selectedGig.expenses
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder)
        .map(toGigExpenseForm),
    })
    setGigStatus('Editing the selected gig.')
    setGigExpenseAmount('')
    setGigExpenseDescription('')
    setIsGigEditorOpen(true)
  }

  const closeClientEditor = () => {
    setIsClientEditorOpen(false)
    setMode('create')
    setForm(emptyForm())
  }

  const closeAdminEditor = () => {
    setIsAdminEditorOpen(false)
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
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

  const startInvoiceEdit = () => {
    if (!selectedInvoice) {
      return
    }

    setIsInvoiceEditorOpen(true)
  }

  const closeInvoiceEditor = () => {
    setIsInvoiceEditorOpen(false)
    setAdjustmentAmount('')
    setAdjustmentReason('')
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

  const updateAdminField = (
    field: keyof AdminUserForm,
    value: string | boolean
  ) => {
    setAdminForm((current) => ({
      ...current,
      [field]: value,
    }))
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
      setClients([])
      setSelectedClientId('')
      setSearchQuery('')
      setIsClientEditorOpen(false)
      setMode('create')
      setForm(emptyForm())
      setGigs([])
      setSelectedGigId('')
      setGigSearchQuery('')
      setIsGigEditorOpen(false)
      setGigMode('create')
      setGigForm(emptyGigForm())
      setGigStatus(defaultGigStatus)
      setInvoices([])
      setSelectedInvoiceId('')
      setIsInvoiceEditorOpen(false)
      setInvoiceSearchQuery('')
      setInvoiceStatus(defaultInvoiceStatus)
      setSellerProfile(emptySellerProfile())
      setSellerProfileForm(emptySellerProfileForm())
      setSellerProfileStatus(
        'Add the seller details that should appear on your invoices.'
      )
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

    const parseOptionalDecimal = (value: string) => {
      const trimmed = value.trim()
      if (!trimmed) {
        return null
      }

      const parsed = Number(trimmed)
      return Number.isFinite(parsed) ? parsed : Number.NaN
    }

    const mileageRate = parseOptionalDecimal(clientSettingsForm.mileageRate)
    const passengerMileageRate = parseOptionalDecimal(
      clientSettingsForm.passengerMileageRate
    )
    const invoiceFilenamePattern = clientSettingsForm.invoiceFilenamePattern.trim()

    if (Number.isNaN(mileageRate) || Number.isNaN(passengerMileageRate)) {
      setClientSettingsStatus('Rates must be valid numbers, for example 0.45.')
      return
    }

    setIsClientSettingsSaving(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/clients/${selectedClient.id}`),
        {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            id: selectedClient.id,
            name: selectedClient.name,
            email: selectedClient.email,
            billingAddress: selectedClient.billingAddress,
            mileageRate,
            passengerMileageRate,
            invoiceFilenamePattern: invoiceFilenamePattern || null,
          }),
        }
      )

      if (response.status === 401) {
        setIsAuthenticated(false)
        setAuthUser(null)
        setIsApiConnected(false)
        setIsClientSettingsOpen(false)
        setStatus('Your session expired. Sign in again to update client settings.')
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to save client settings.')
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

  const openUserSettings = () => {
    setUserSettingsForm({
      mileageRate:
        authUser?.mileageRate === null || authUser?.mileageRate === undefined
          ? ''
          : String(authUser.mileageRate),
      passengerMileageRate:
        authUser?.passengerMileageRate === null ||
        authUser?.passengerMileageRate === undefined
          ? ''
          : String(authUser.passengerMileageRate),
      invoiceFilenamePattern: authUser?.invoiceFilenamePattern ?? '',
      invoiceReplyToEmail: authUser?.invoiceReplyToEmail ?? '',
    })
    setUserSettingsStatus(
      'Set the defaults used when a client does not provide its own overrides.'
    )
    setIsProfileMenuOpen(false)
    setIsUserSettingsOpen(true)
  }

  const openSellerProfile = () => {
    setSellerProfileForm(toSellerProfileForm(sellerProfile))
    setSellerProfileStatus(
      sellerProfile.isConfigured
        ? sellerProfileNotice
        : 'Add the seller details that should appear on your invoices.'
    )
    setIsProfileMenuOpen(false)
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

  const closeUserSettings = () => {
    setIsUserSettingsOpen(false)
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

    const parseOptionalDecimal = (value: string) => {
      const trimmed = value.trim()
      if (!trimmed) {
        return null
      }

      const parsed = Number(trimmed)
      return Number.isFinite(parsed) ? parsed : Number.NaN
    }

    const mileageRate = parseOptionalDecimal(userSettingsForm.mileageRate)
    const passengerMileageRate = parseOptionalDecimal(
      userSettingsForm.passengerMileageRate
    )
    const invoiceFilenamePattern = userSettingsForm.invoiceFilenamePattern.trim()
    const invoiceReplyToEmail = userSettingsForm.invoiceReplyToEmail.trim()

    if (Number.isNaN(mileageRate) || Number.isNaN(passengerMileageRate)) {
      setUserSettingsStatus('Rates must be valid numbers, for example 0.45.')
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
          invoiceFilenamePattern: invoiceFilenamePattern || null,
          invoiceReplyToEmail: invoiceReplyToEmail || null,
        }),
      })

      if (response.status === 401) {
        setIsAuthenticated(false)
        setAuthUser(null)
        setIsApiConnected(false)
        setIsUserSettingsOpen(false)
        setStatus('Your session expired. Sign in again to update your settings.')
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to save your settings.')
      }

      const savedSettings = (await response.json()) as {
        mileageRate: number | null
        passengerMileageRate: number | null
        invoiceFilenamePattern: string | null
        invoiceReplyToEmail: string | null
      }

      setAuthUser((current) =>
        current
          ? {
              ...current,
              mileageRate: savedSettings.mileageRate,
              passengerMileageRate: savedSettings.passengerMileageRate,
              invoiceFilenamePattern: savedSettings.invoiceFilenamePattern,
              invoiceReplyToEmail: savedSettings.invoiceReplyToEmail,
            }
          : current
      )
      setUserSettingsForm({
        mileageRate:
          savedSettings.mileageRate === null ? '' : String(savedSettings.mileageRate),
        passengerMileageRate:
          savedSettings.passengerMileageRate === null
            ? ''
            : String(savedSettings.passengerMileageRate),
        invoiceFilenamePattern: savedSettings.invoiceFilenamePattern ?? '',
        invoiceReplyToEmail: savedSettings.invoiceReplyToEmail ?? '',
      })
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
        setIsAuthenticated(false)
        setAuthUser(null)
        setIsApiConnected(false)
        setIsSellerProfileOpen(false)
        setStatus('Your session expired. Sign in again to update your seller profile.')
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
      setSellerProfile(savedProfile)
      setSellerProfileForm(toSellerProfileForm(savedProfile))
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

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

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

    const clientRates =
      mode === 'edit' && selectedClient
        ? {
            mileageRate: selectedClient.mileageRate,
            passengerMileageRate: selectedClient.passengerMileageRate,
          }
        : {
            mileageRate: null,
            passengerMileageRate: null,
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
      const requestBody = JSON.stringify({
        ...payload,
        ...clientRates,
      })

      const response = await fetchWithSession(endpoint, {
        method: isEdit ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: requestBody,
      })

      if (response.status === 401) {
        setIsAuthenticated(false)
        setAuthUser(null)
        setIsApiConnected(false)
        setStatus('Your session expired. Sign in again to save changes.')
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
      setIsClientEditorOpen(false)
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

      if (response.status === 401) {
        setIsAuthenticated(false)
        setAuthUser(null)
        setIsApiConnected(false)
        setStatus('Your session expired. Sign in again to delete clients.')
        return
      }

      if (!response.ok) {
        throw new Error('Delete failed.')
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
    } catch {
      setStatus('Unable to delete right now.')
    } finally {
      setIsLoading(false)
    }
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

  const handleGigSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

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
        setIsAuthenticated(false)
        setAuthUser(null)
        setIsApiConnected(false)
        setStatus('Your session expired. Sign in again to keep managing gigs.')
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
      setGigForm({
        clientId: savedGig.clientId,
        title: savedGig.title,
        date: savedGig.date,
        venue: savedGig.venue,
        fee: String(savedGig.fee),
        notes: savedGig.notes ?? '',
        wasDriving: savedGig.wasDriving,
        status: savedGig.status,
        expenses: savedGig.expenses
          .slice()
          .sort((left, right) => left.sortOrder - right.sortOrder)
          .map(toGigExpenseForm),
      })
      setGigExpenseAmount('')
      setGigExpenseDescription('')
      setGigStatus(isEdit ? 'Gig updated.' : 'Gig created.')
      setIsGigEditorOpen(false)
    } catch (error) {
      setGigStatus(
        error instanceof Error ? error.message : 'Unable to save this gig right now.'
      )
    } finally {
      setIsGigLoading(false)
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

  const handleToggleGigSelection = (gigId: string) => {
    setSelectedGigIds((current) =>
      current.includes(gigId)
        ? current.filter((value) => value !== gigId)
        : [...current, gigId]
    )
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

  const handleDownloadInvoicePdf = async (invoice: Invoice) => {
    const fallbackFilename = `${invoice.invoiceNumber}.pdf`
    setIsInvoiceLoading(true)
    setInvoiceStatus(`Preparing ${fallbackFilename}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/pdf`))
      if (!response.ok) {
        throw new Error('Unable to download the invoice PDF.')
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
      setInvoiceStatus(`Downloaded ${link.download}.`)
    } catch (error) {
      setInvoiceStatus(
        error instanceof Error ? error.message : 'Unable to download the invoice PDF.'
      )
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const extractDownloadFilename = (contentDisposition: string | null) => {
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

    const quotedMatch = contentDisposition.match(/filename=\"([^\"]+)\"/i)
    if (quotedMatch?.[1]) {
      return quotedMatch[1]
    }

    const plainMatch = contentDisposition.match(/filename=([^;]+)/i)
    return plainMatch?.[1]?.trim() ?? null
  }

  const handleInvoiceStatusChange = async (invoice: Invoice, status: InvoiceStatus) => {
    if (invoice.status === status) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Updating ${invoice.invoiceNumber} to ${status}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/status`), {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ status }),
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const fieldError = problem?.errors?.status?.[0]
        throw new Error(fieldError ?? problem?.detail ?? problem?.title ?? 'Unable to update status.')
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setInvoiceStatus(`Invoice ${updatedInvoice.invoiceNumber} is now ${updatedInvoice.status}.`)
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to update invoice status.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleInvoiceReissue = async (invoice: Invoice) => {
    const isRedraft = invoice.status === 'Draft'
    const actionLabel = isRedraft ? 'Redraft' : 'Re-issue'
    const actionVerb = isRedraft ? 'Redrafting' : 'Re-issuing'
    const shouldProceed = window.confirm(
      isRedraft
        ? `Redraft ${invoice.invoiceNumber}? This will regenerate the draft document without changing reissue history.`
        : `Re-issue ${invoice.invoiceNumber}? This will regenerate the document and log the action.`
    )
    if (!shouldProceed) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`${actionVerb} ${invoice.invoiceNumber}...`)

    try {
      const actionPath = isRedraft ? 'redraft' : 'reissue'
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/${actionPath}`), {
        method: 'POST',
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const fieldError = problem?.errors?.recipient?.[0]
        const statusError = problem?.errors?.status?.[0]
        throw new Error(
          fieldError ??
            statusError ??
            problem?.detail ??
            problem?.title ??
            `Unable to ${actionLabel.toLowerCase()} invoice.`
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )

      if (isRedraft) {
        setInvoiceStatus(`Invoice ${updatedInvoice.invoiceNumber} draft regenerated.`)
      } else {
        const reissuedAt = formatDateTime(updatedInvoice.lastReissuedUtc)
        setInvoiceStatus(`Invoice ${updatedInvoice.invoiceNumber} re-issued at ${reissuedAt}.`)
      }
    } catch (error) {
      setInvoiceStatus(
        error instanceof Error ? error.message : `Unable to ${actionLabel.toLowerCase()} invoice.`
      )
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleSendInvoiceEmail = async (invoice: Invoice) => {
    const message = window.prompt(
      `Add an optional message for ${invoice.invoiceNumber}, or leave blank to send the standard note.`
    )
    if (message === null) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Sending ${invoice.invoiceNumber} to client...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/send-email`), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          message: message.trim() || null,
        }),
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const recipientError = problem?.errors?.recipient?.[0]
        const pdfError = problem?.errors?.pdf?.[0]
        throw new Error(
          recipientError ??
            pdfError ??
            problem?.detail ??
            problem?.title ??
            'Unable to send invoice email.'
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setInvoiceStatus(
        `Invoice ${updatedInvoice.invoiceNumber} sent to ${updatedInvoice.lastDeliveryRecipient}.`
      )
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to send invoice email.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleAddInvoiceAdjustment = async (invoice: Invoice) => {
    const amount = Number.parseFloat(adjustmentAmount)
    if (!Number.isFinite(amount) || amount === 0) {
      setInvoiceStatus('Enter a non-zero adjustment amount.')
      return
    }

    const reason = adjustmentReason.trim()
    if (!reason) {
      setInvoiceStatus('Add a reason before saving an adjustment.')
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Saving adjustment on ${invoice.invoiceNumber}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}/adjustments`), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          amount,
          reason,
        }),
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const reasonError = problem?.errors?.reason?.[0]
        const amountError = problem?.errors?.amount?.[0]
        throw new Error(
          reasonError ??
            amountError ??
            problem?.detail ??
            problem?.title ??
            'Unable to add invoice adjustment.'
        )
      }

      const updatedInvoice = (await response.json()) as Invoice
      setInvoices((current) =>
        current.map((value) => (value.id === updatedInvoice.id ? updatedInvoice : value))
      )
      setAdjustmentAmount('')
      setAdjustmentReason('')
      setInvoiceStatus(`Adjustment saved. ${updatedInvoice.invoiceNumber} now totals ${formatCurrency(updatedInvoice.total)}.`)
      setIsInvoiceEditorOpen(false)
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to add invoice adjustment.')
    } finally {
      setIsInvoiceLoading(false)
    }
  }

  const handleDeleteInvoice = async (invoice: Invoice) => {
    if (invoice.status !== 'Draft') {
      setInvoiceStatus(
        `Only Draft invoices can be deleted. ${invoice.invoiceNumber} is currently ${invoice.status}.`
      )
      return
    }

    const shouldDelete = window.confirm(
      `Delete ${invoice.invoiceNumber}? This cannot be undone and should only be used for draft mistakes.`
    )
    if (!shouldDelete) {
      return
    }

    setIsInvoiceLoading(true)
    setInvoiceStatus(`Deleting ${invoice.invoiceNumber}...`)

    try {
      const response = await fetchWithSession(buildApiUrl(`/invoices/${invoice.id}`), {
        method: 'DELETE',
      })

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const statusError = problem?.errors?.status?.[0]
        throw new Error(
          statusError ?? problem?.detail ?? problem?.title ?? 'Unable to delete invoice.'
        )
      }

      setInvoices((current) => current.filter((value) => value.id !== invoice.id))
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
      setSelectedInvoiceId((current) => (current === invoice.id ? '' : current))
      setInvoiceStatus(`Invoice ${invoice.invoiceNumber} deleted.`)
      setIsInvoiceEditorOpen(false)
    } catch (error) {
      setInvoiceStatus(error instanceof Error ? error.message : 'Unable to delete invoice.')
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
        onOpenLinkedInvoice={openSelectedGigInvoice}
        onOpenSellerProfile={openSellerProfile}
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
        invoices={invoices}
        issuedInvoiceCount={issuedInvoiceCount}
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
    <main className="app-shell">
      <section className="hero-panel app-frame">
        <div className="content-shell">
          <div className="content-header panel">
            <div className="content-header-top">
              <div className="content-header-copy">
                <p className="eyebrow">Workspace</p>
                <h1>Your work, all in one place.</h1>
                <p className="hero-text">
                  Move between clients, gigs, invoices and admin tools with everything easy to find.
                </p>
              </div>

              <div className="profile-menu" ref={profileMenuRef}>
                <button
                  aria-expanded={isProfileMenuOpen}
                  aria-haspopup="menu"
                  aria-label="Open profile menu"
                  className={`profile-trigger ${isProfileMenuOpen ? 'open' : ''}`}
                  onClick={() => setIsProfileMenuOpen((current) => !current)}
                  type="button"
                >
                  <span className="profile-avatar" aria-hidden="true">
                    {profileImageUrl ? (
                      <img
                        className="profile-avatar-image"
                        src={profileImageUrl}
                        alt=""
                        decoding="async"
                        referrerPolicy="no-referrer"
                      />
                    ) : (
                      profileInitials || 'U'
                    )}
                  </span>
                </button>

                {isProfileMenuOpen && (
                  <div className="profile-dropdown" role="menu" aria-label="Profile menu">
                    <div className="profile-summary">
                      <p className="section-label">Signed in</p>
                      <strong>{profileDisplayName}</strong>
                      <span>{authUser?.email}</span>
                    </div>
                    <div className="profile-meta">
                      <span>{isAdmin ? 'Administrator' : 'Standard access'}</span>
                    </div>
                    <div className="profile-meta">
                      <span>
                        {sellerProfile.isInvoiceReady
                          ? 'Seller profile ready'
                          : sellerProfile.isConfigured
                            ? 'Seller profile needs attention'
                            : 'Seller profile not set up'}
                      </span>
                    </div>
                    <label className="theme-field" htmlFor="theme-preference-select">
                      <span>Theme</span>
                      <select
                        id="theme-preference-select"
                        value={themePreference}
                        onChange={(event) =>
                          handleThemePreferenceChange(event.target.value as ThemePreference)
                        }
                      >
                        <option value="system">System</option>
                        <option value="light">Light</option>
                        <option value="dark">Dark</option>
                      </select>
                    </label>
                    <button
                      className="ghost-button profile-settings"
                      onClick={openSellerProfile}
                      role="menuitem"
                      type="button"
                      disabled={isLoading || isAdminLoading || isSellerProfileSaving}
                    >
                      Seller profile
                    </button>
                    <button
                      className="ghost-button profile-settings"
                      onClick={openUserSettings}
                      role="menuitem"
                      type="button"
                      disabled={isLoading || isAdminLoading || isUserSettingsSaving}
                    >
                      Settings
                    </button>
                    <button
                      className="ghost-button profile-signout"
                      onClick={signOut}
                      role="menuitem"
                      type="button"
                      disabled={isLoading || isAdminLoading}
                    >
                      Sign out
                    </button>
                  </div>
                )}
              </div>
            </div>

            <div className="content-header-body">
              <div className="content-header-copy">
                <p className="eyebrow">{currentSection?.eyebrow ?? 'Workspace'}</p>
                <h2>{currentSection?.label ?? 'Glovelly'}</h2>
                <p className="hero-text">{currentSection?.description}</p>
              </div>

              <div className="hero-mascot header-mascot">
                <img
                  src="/gordon-192.png"
                  alt="Gordon the Glovelly mascot"
                  decoding="async"
                  loading="lazy"
                />
                <div>
                  <p className="section-label">Meet Gordon</p>
                  <strong>Mozart wig. Rubber chicken. Unreasonably good taste in admin.</strong>
                </div>
              </div>
            </div>

            <nav className="charm-bar" aria-label="Primary">
              {navigationItems.map((item) => (
                <button
                  key={item.id}
                  className={`charm-item ${activeSection === item.id ? 'selected' : ''}`}
                  onClick={() => setActiveSection(item.id)}
                  type="button"
                  disabled={item.disabled}
                >
                  <span className="charm-meta">{item.eyebrow}</span>
                  <strong>{item.label}</strong>
                  <span>{item.description}</span>
                </button>
              ))}
            </nav>

            <div className="content-header-aside">
              <div className="hero-metrics">
                {headerMetrics.map((metric) => (
                  <article key={metric.label}>
                    <span>{metric.value}</span>
                    <p>{metric.label}</p>
                  </article>
                ))}
              </div>
            </div>
          </div>

          {currentSectionContent}
        </div>
      </section>

      <p className="build-meta">
        {appMetadata.deploymentName ? `${appMetadata.deploymentName} • ` : ''}
        {formatBuildMetadata(appMetadata.commitId, appMetadata.buildTimestamp)}
      </p>

      <UserSettingsModal
        form={userSettingsForm}
        invoiceFilenamePreview={buildInvoiceFilenamePreview(
          userSettingsForm.invoiceFilenamePattern,
          null
        )}
        invoiceFilenameTokens={invoiceFilenameTokens}
        isOpen={isUserSettingsOpen}
        isSaving={isUserSettingsSaving}
        onClose={closeUserSettings}
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

      <ClientSettingsModal
        authUser={authUser}
        form={clientSettingsForm}
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
    </main>
  )
}

export default App
