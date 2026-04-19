import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

type Address = {
  line1: string
  line2: string
  city: string
  stateOrCounty: string
  postalCode: string
  country: string
}

type Client = {
  id: string
  name: string
  email: string
  billingAddress: Address
  mileageRate: number | null
  passengerMileageRate: number | null
}

type ClientForm = {
  name: string
  email: string
  billingAddress: Address
}

type AuthUser = {
  userId: string
  role: string
  name: string
  email: string
  profileImageUrl: string
  mileageRate: number | null
  passengerMileageRate: number | null
}

type AdminUser = {
  id: string
  email: string
  displayName: string | null
  googleSubject: string | null
  isEnrolled: boolean
  role: string
  isActive: boolean
  createdUtc: string
  lastLoginUtc: string | null
}

type AdminUserForm = {
  email: string
  displayName: string
  googleSubject: string
  role: 'Admin' | 'User'
  isActive: boolean
}

type UserSettingsForm = {
  mileageRate: string
  passengerMileageRate: string
}

type ClientSettingsForm = {
  mileageRate: string
  passengerMileageRate: string
}

type GigStatus = 'Draft' | 'Confirmed' | 'Completed' | 'Cancelled'

type Gig = {
  id: string
  clientId: string
  invoiceId: string | null
  title: string
  date: string
  venue: string
  fee: number
  travelMiles: number
  passengerCount: number | null
  notes: string | null
  wasDriving: boolean
  status: GigStatus
  invoicedAt: string | null
  isInvoiced: boolean
}

type GigForm = {
  clientId: string
  title: string
  date: string
  venue: string
  fee: string
  notes: string
  wasDriving: boolean
  status: GigStatus
}

type AppSection = 'clients' | 'admin' | 'gigs'
type ThemePreference = 'system' | 'light' | 'dark'

const themeStorageKey = 'glovelly.theme-preference'

const emptyForm = (): ClientForm => ({
  name: '',
  email: '',
  billingAddress: {
    line1: '',
    line2: '',
    city: '',
    stateOrCounty: '',
    postalCode: '',
    country: 'United Kingdom',
  },
})

const emptyAdminForm = (): AdminUserForm => ({
  email: '',
  displayName: '',
  googleSubject: '',
  role: 'User',
  isActive: true,
})

const emptyUserSettingsForm = (): UserSettingsForm => ({
  mileageRate: '',
  passengerMileageRate: '',
})

const emptyClientSettingsForm = (): ClientSettingsForm => ({
  mileageRate: '',
  passengerMileageRate: '',
})

const emptyGigForm = (): GigForm => ({
  clientId: '',
  title: '',
  date: '',
  venue: '',
  fee: '',
  notes: '',
  wasDriving: true,
  status: 'Confirmed',
})

const defaultAdminStatus = 'User enrolment tools ready.'
const defaultGigStatus = 'Create a gig and it will show up in your gigs workspace.'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

function buildApiUrl(path: string) {
  return `${apiBaseUrl}${path}`
}

function buildReturnUrl() {
  return window.location.href
}

async function fetchWithSession(input: string, init?: RequestInit) {
  return fetch(input, {
    ...init,
    credentials: 'include',
    cache: 'no-store',
  })
}

async function parseProblemDetails(response: Response) {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) {
    return null
  }

  try {
    return (await response.json()) as {
      title?: string
      detail?: string
      errors?: Record<string, string[]>
    }
  } catch {
    return null
  }
}

function formatDateTime(value: string | null) {
  if (!value) {
    return 'Never'
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return 'Unknown'
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date)
}

function formatRate(value: number | null) {
  if (value === null) {
    return 'Using default'
  }

  return `${value.toFixed(2)} per mile`
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'GBP',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value)
}

function formatDate(value: string) {
  if (!value) {
    return 'No date'
  }

  const date = new Date(`${value}T00:00:00`)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
  }).format(date)
}

function formatGigStatus(status: GigStatus) {
  return status === 'Confirmed' ? 'Planned' : status
}

function getStoredThemePreference(): ThemePreference {
  const rawPreference = window.localStorage.getItem(themeStorageKey)
  if (
    rawPreference === 'system' ||
    rawPreference === 'light' ||
    rawPreference === 'dark'
  ) {
    return rawPreference
  }

  return 'system'
}

function App() {
  const [activeSection, setActiveSection] = useState<AppSection>('clients')
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [searchQuery, setSearchQuery] = useState('')
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

  const [adminUsers, setAdminUsers] = useState<AdminUser[]>([])
  const [selectedAdminUserId, setSelectedAdminUserId] = useState<string>('')
  const [adminSearchQuery, setAdminSearchQuery] = useState('')
  const [adminMode, setAdminMode] = useState<'create' | 'edit'>('create')
  const [adminForm, setAdminForm] = useState<AdminUserForm>(emptyAdminForm)
  const [adminStatus, setAdminStatus] = useState(defaultAdminStatus)
  const [isAdminLoading, setIsAdminLoading] = useState(false)
  const [gigs, setGigs] = useState<Gig[]>([])
  const [selectedGigId, setSelectedGigId] = useState<string>('')
  const [gigSearchQuery, setGigSearchQuery] = useState('')
  const [gigMode, setGigMode] = useState<'create' | 'edit'>('create')
  const [gigForm, setGigForm] = useState<GigForm>(emptyGigForm)
  const [gigStatus, setGigStatus] = useState(defaultGigStatus)
  const [isGigLoading, setIsGigLoading] = useState(false)

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
      setActiveSection('clients')
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
      setMode('create')
      setForm(emptyForm())
      setGigs([])
      setSelectedGigId('')
      setGigSearchQuery('')
      setGigMode('create')
      setGigForm(emptyGigForm())
      setGigStatus(defaultGigStatus)
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
          ? 'Manage who can sign in to Glovelly.'
          : 'No users enrolled yet. Add the first account below.'
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
          setStatus('Sign in with Google to access Glovelly.')
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

        const [clientsResponse, gigsResponse] = await Promise.all([
          fetchWithSession(buildApiUrl('/clients')),
          fetchWithSession(buildApiUrl('/gigs')),
        ])

        if (clientsResponse.status === 401 || gigsResponse.status === 401) {
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

        const data = (await clientsResponse.json()) as Client[]
        const gigData = (await gigsResponse.json()) as Gig[]
        if (ignore) {
          return
        }

        setClients(data)
        setSelectedClientId(data[0]?.id ?? '')
        setGigs(gigData)
        setSelectedGigId(gigData[0]?.id ?? '')
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
            setStatus('API unavailable. Start the backend to finish sign-in.')
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

  const filteredClients = useMemo(() => {
    const query = searchQuery.trim().toLowerCase()
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
  }, [clients, searchQuery])

  const filteredAdminUsers = useMemo(() => {
    const query = adminSearchQuery.trim().toLowerCase()
    if (!query) {
      return adminUsers
    }

    return adminUsers.filter((user) =>
      [user.email, user.displayName ?? '', user.role, user.googleSubject ?? '']
        .join(' ')
        .toLowerCase()
        .includes(query)
    )
  }, [adminSearchQuery, adminUsers])

  const filteredGigs = useMemo(() => {
    const query = gigSearchQuery.trim().toLowerCase()
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
      const clientName =
        clients.find((client) => client.id === gig.clientId)?.name ?? ''

      return [gig.title, gig.venue, gig.date, gig.status, clientName]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
  }, [clients, gigSearchQuery, gigs])

  const selectedClient =
    filteredClients.find((client) => client.id === selectedClientId) ??
    clients.find((client) => client.id === selectedClientId) ??
    filteredClients[0] ??
    null

  const selectedAdminUser =
    filteredAdminUsers.find((user) => user.id === selectedAdminUserId) ??
    adminUsers.find((user) => user.id === selectedAdminUserId) ??
    filteredAdminUsers[0] ??
    null

  const selectedGig =
    filteredGigs.find((gig) => gig.id === selectedGigId) ??
    gigs.find((gig) => gig.id === selectedGigId) ??
    filteredGigs[0] ??
    null

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
    if (gigForm.clientId || clients.length === 0) {
      return
    }

    setGigForm((current) => ({
      ...current,
      clientId: clients[0]?.id ?? '',
    }))
  }, [clients, gigForm.clientId])

  const activeCities = new Set(clients.map((client) => client.billingAddress.city)).size
  const activeUsersCount = adminUsers.filter((user) => user.isActive).length
  const totalAdmins = adminUsers.filter((user) => user.role === 'Admin').length
  const plannedGigCount = gigs.filter((gig) => gig.status === 'Confirmed').length
  const upcomingGigCount = gigs.filter((gig) => gig.date >= new Date().toISOString().slice(0, 10)).length

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
    ...(isAdmin
      ? [
          {
            id: 'admin' as const,
            label: 'Admin',
            eyebrow: 'Restricted',
            description: 'User enrolment, roles and sign-in access.',
          },
        ]
      : []),
    {
      id: 'gigs',
      label: 'Gigs',
      eyebrow: 'Live',
      description: 'Bookings, delivery status and the first invoicing-ready gig records.',
    },
  ]

  const currentSection = navigationItems.find((item) => item.id === activeSection)
  const secondaryMetricValue =
    activeSection === 'admin'
      ? activeUsersCount
      : activeSection === 'gigs'
        ? upcomingGigCount
        : activeCities
  const secondaryMetricLabel =
    activeSection === 'admin'
      ? 'active accounts'
      : activeSection === 'gigs'
        ? 'upcoming gigs'
        : 'active cities'

  const startCreating = () => {
    setMode('create')
    setForm(emptyForm())
  }

  const startEditing = () => {
    if (!selectedClient) {
      return
    }

    setMode('edit')
    setForm({
      name: selectedClient.name,
      email: selectedClient.email,
      billingAddress: { ...selectedClient.billingAddress },
    })
  }

  const startAdminCreate = () => {
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
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
    })
    setGigStatus('Editing the selected gig.')
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
    value: string | boolean
  ) => {
    setGigForm((current) => ({
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
      setClients([])
      setSelectedClientId('')
      setSearchQuery('')
      setMode('create')
      setForm(emptyForm())
      setGigs([])
      setSelectedGigId('')
      setGigSearchQuery('')
      setGigMode('create')
      setGigForm(emptyGigForm())
      setGigStatus(defaultGigStatus)
      resetAdminWorkspace()
      setShouldCloseBrowserNotice(true)
      setStatus('Signed out. Close your browser to fully end the Google session.')
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
    })
    setUserSettingsStatus(
      'Set the mileage defaults used when a client has no custom rates.'
    )
    setIsProfileMenuOpen(false)
    setIsUserSettingsOpen(true)
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
      }

      setAuthUser((current) =>
        current
          ? {
              ...current,
              mileageRate: savedSettings.mileageRate,
              passengerMileageRate: savedSettings.passengerMileageRate,
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
    } catch {
      setStatus('Unable to save right now. Check that the API is running.')
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
      setAdminStatus('Email is required for enrolment.')
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
        setStatus('Your session expired. Sign in again to keep managing enrolments.')
        return
      }

      if (response.status === 403) {
        setAdminStatus('Administrator access is required to manage enrolments.')
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
      setAdminStatus(isEdit ? 'User enrolment updated.' : 'User enrolled.')
    } catch (error) {
      setAdminStatus(
        error instanceof Error ? error.message : 'Unable to save enrolment right now.'
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
          expenses: [],
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
      })
      setGigStatus(isEdit ? 'Gig updated.' : 'Gig created.')
    } catch (error) {
      setGigStatus(
        error instanceof Error ? error.message : 'Unable to save this gig right now.'
      )
    } finally {
      setIsGigLoading(false)
    }
  }

  if (isCheckingSession) {
    return (
      <main className="app-shell auth-shell">
        <section className="hero-panel auth-panel">
          <div className="hero-copy">
            <p className="eyebrow">Security</p>
            <h1>Checking your Google session.</h1>
            <p className="hero-text">
              Glovelly now protects client and invoice data behind OpenID Connect.
            </p>
          </div>
          <span className="status-pill">{status}</span>
        </section>
      </main>
    )
  }

  if (!isAuthenticated) {
    return (
      <main className="app-shell auth-shell">
        <section className="hero-panel auth-panel">
          <div className="hero-copy">
            <p className="eyebrow">Secure Sign-In</p>
            <h1>Glovelly now uses Google to verify who’s allowed in.</h1>
            <p className="hero-text">
              Sign in with the Google account attached to your deployment so the API
              can issue a secure app session before any business data is loaded.
            </p>
          </div>

          <div className="auth-actions">
            <span className="status-pill">{status}</span>
            {shouldCloseBrowserNotice && (
              <p className="auth-note">
                The Glovelly session cookie has been cleared. Close your browser if you
                want to fully end the Google sign-in session too.
              </p>
            )}
            <button className="primary-button" onClick={signIn} type="button">
              Continue with Google
            </button>
          </div>
        </section>
      </main>
    )
  }

  const renderClientsSection = () => (
    <section className="section-layout">
      <div className="workspace">
        <div className="clients-panel panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Directory</p>
              <h2>Clients</h2>
            </div>
            <button className="ghost-button" onClick={startCreating} type="button">
              New client
            </button>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              type="search"
              placeholder="Name, email, city..."
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
            />
          </label>

          <div className="client-list">
            {filteredClients.map((client) => (
              <button
                key={client.id}
                className={`client-card ${selectedClient?.id === client.id ? 'selected' : ''}`}
                onClick={() => setSelectedClientId(client.id)}
                type="button"
              >
                <div>
                  <strong>{client.name}</strong>
                  <span>{client.email}</span>
                </div>
                <small>
                  {client.billingAddress.city}, {client.billingAddress.country}
                </small>
              </button>
            ))}

            {filteredClients.length === 0 && (
              <div className="empty-state">
                <strong>{isApiConnected ? 'No clients match that search.' : 'No client data available.'}</strong>
                <p>
                  {isApiConnected
                    ? 'Try a different term or add a fresh client profile.'
                    : 'Start the backend and refresh this page to complete sign-in.'}
                </p>
              </div>
            )}
          </div>
        </div>

        <div className="detail-panel panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Overview</p>
              <h2>{selectedClient?.name ?? 'No client selected'}</h2>
            </div>
            <div className="actions">
              <button
                className="ghost-button"
                onClick={openClientSettings}
                type="button"
                disabled={!selectedClient}
              >
                Settings
              </button>
              <button
                className="ghost-button"
                onClick={startEditing}
                type="button"
                disabled={!selectedClient}
              >
                Edit
              </button>
              <button
                className="danger-button"
                onClick={handleDelete}
                type="button"
                disabled={!selectedClient || isLoading}
              >
                Delete
              </button>
            </div>
          </div>

          {selectedClient ? (
            <div className="detail-grid">
              <article>
                <p className="detail-label">Primary email</p>
                <strong>{selectedClient.email}</strong>
              </article>
              <article>
                <p className="detail-label">Billing city</p>
                <strong>{selectedClient.billingAddress.city}</strong>
              </article>
              <article className="full-width">
                <p className="detail-label">Billing address</p>
                <strong>{selectedClient.billingAddress.line1}</strong>
                {selectedClient.billingAddress.line2 && (
                  <span>{selectedClient.billingAddress.line2}</span>
                )}
                <span>
                  {selectedClient.billingAddress.city}
                  {selectedClient.billingAddress.stateOrCounty
                    ? `, ${selectedClient.billingAddress.stateOrCounty}`
                    : ''}
                </span>
                <span>
                  {selectedClient.billingAddress.postalCode},{' '}
                  {selectedClient.billingAddress.country}
                </span>
              </article>
            </div>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a client to see billing details.</strong>
              <p>The right-hand panel is set up for a fuller CRM-style summary later on.</p>
            </div>
          )}
        </div>

        <form className="editor-panel panel" onSubmit={handleSubmit}>
          <div className="panel-heading">
            <div>
              <p className="section-label">Editor</p>
              <h2>{mode === 'create' ? 'Add client' : 'Edit client'}</h2>
            </div>
            <span className="status-pill">{status}</span>
          </div>

          <div className="form-grid">
            <label>
              <span>Client name</span>
              <input
                required
                value={form.name}
                onChange={(event) => updateField('name', event.target.value)}
                placeholder="Fox & Finch Events"
              />
            </label>

            <label>
              <span>Email</span>
              <input
                required
                type="email"
                value={form.email}
                onChange={(event) => updateField('email', event.target.value)}
                placeholder="accounts@example.com"
              />
            </label>

            <label className="full-width">
              <span>Address line 1</span>
              <input
                required
                value={form.billingAddress.line1}
                onChange={(event) => updateAddressField('line1', event.target.value)}
                placeholder="12 Chapel Street"
              />
            </label>

            <label className="full-width">
              <span>Address line 2</span>
              <input
                value={form.billingAddress.line2}
                onChange={(event) => updateAddressField('line2', event.target.value)}
                placeholder="Optional"
              />
            </label>

            <label>
              <span>City</span>
              <input
                required
                value={form.billingAddress.city}
                onChange={(event) => updateAddressField('city', event.target.value)}
                placeholder="Manchester"
              />
            </label>

            <label>
              <span>County / state</span>
              <input
                value={form.billingAddress.stateOrCounty}
                onChange={(event) => updateAddressField('stateOrCounty', event.target.value)}
                placeholder="Greater Manchester"
              />
            </label>

            <label>
              <span>Postal code</span>
              <input
                value={form.billingAddress.postalCode}
                onChange={(event) => updateAddressField('postalCode', event.target.value)}
                placeholder="M3 5JZ"
              />
            </label>

            <label>
              <span>Country</span>
              <input
                value={form.billingAddress.country}
                onChange={(event) => updateAddressField('country', event.target.value)}
                placeholder="United Kingdom"
              />
            </label>
          </div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isLoading}>
              {mode === 'create' ? 'Save client' : 'Update client'}
            </button>
            <button className="ghost-button" onClick={startCreating} type="button">
              Reset form
            </button>
          </div>
        </form>
      </div>
    </section>
  )

  const renderAdminSection = () => (
    <section className="section-layout admin-zone">
      <div className="admin-banner panel">
        <div>
          <p className="section-label">Administrator Area</p>
          <h2>User enrolment</h2>
          <p className="hero-text">
            Control which Google accounts can access Glovelly, whether they are
            active, and whether they should see this administrator workspace.
          </p>
        </div>

        <div className="hero-metrics admin-metrics">
          <article>
            <span>{adminUsers.length}</span>
            <p>enrolled users</p>
          </article>
          <article>
            <span>{activeUsersCount}</span>
            <p>active accounts</p>
          </article>
          <article>
            <span>{totalAdmins}</span>
            <p>administrators</p>
          </article>
        </div>
      </div>

      <div className="admin-workspace">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Access Directory</p>
              <h2>Enrolled users</h2>
            </div>
            <button className="ghost-button" onClick={startAdminCreate} type="button">
              New enrolment
            </button>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              type="search"
              placeholder="Name, email, role..."
              value={adminSearchQuery}
              onChange={(event) => setAdminSearchQuery(event.target.value)}
            />
          </label>

          <div className="client-list">
            {filteredAdminUsers.map((user) => (
              <button
                key={user.id}
                className={`client-card ${selectedAdminUser?.id === user.id ? 'selected' : ''}`}
                onClick={() => setSelectedAdminUserId(user.id)}
                type="button"
              >
                <div>
                  <strong>{user.displayName || user.email}</strong>
                  <span>{user.email}</span>
                </div>
                <small>
                  {user.role} · {user.isActive ? 'Active' : 'Inactive'} ·{' '}
                  {user.isEnrolled ? 'Bound' : 'Invited'}
                </small>
              </button>
            ))}

            {filteredAdminUsers.length === 0 && (
              <div className="empty-state">
                <strong>No enrolled users match that search.</strong>
                <p>Try another term or start a fresh enrolment.</p>
              </div>
            )}
          </div>
        </div>

        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Enrolment Overview</p>
              <h2>{selectedAdminUser?.displayName || selectedAdminUser?.email || 'No user selected'}</h2>
            </div>
            <div className="actions">
              <button
                className="ghost-button"
                onClick={startAdminEdit}
                type="button"
                disabled={!selectedAdminUser}
              >
                Edit enrolment
              </button>
            </div>
          </div>

          {selectedAdminUser ? (
            <div className="detail-grid">
              <article>
                <p className="detail-label">Role</p>
                <strong>{selectedAdminUser.role}</strong>
              </article>
              <article>
                <p className="detail-label">Access</p>
                <strong>{selectedAdminUser.isActive ? 'Active' : 'Inactive'}</strong>
              </article>
              <article>
                <p className="detail-label">Enrolment</p>
                <strong>{selectedAdminUser.isEnrolled ? 'Bound to Google subject' : 'Invited by email'}</strong>
              </article>
              <article className="full-width">
                <p className="detail-label">Google subject</p>
                <strong>{selectedAdminUser.googleSubject ?? 'Pending first Google sign-in'}</strong>
              </article>
              <article>
                <p className="detail-label">Created</p>
                <strong>{formatDateTime(selectedAdminUser.createdUtc)}</strong>
              </article>
              <article>
                <p className="detail-label">Last login</p>
                <strong>{formatDateTime(selectedAdminUser.lastLoginUtc)}</strong>
              </article>
            </div>
          ) : (
            <div className="empty-state roomy">
              <strong>Select an enrolled user to review their access.</strong>
              <p>The admin area stays hidden from standard users and only appears for admins.</p>
            </div>
          )}
        </div>

        <form className="panel" onSubmit={handleAdminSubmit}>
          <div className="panel-heading">
            <div>
              <p className="section-label">Management Pane</p>
              <h2>{adminMode === 'create' ? 'Create enrolment' : 'Update enrolment'}</h2>
            </div>
            <span className="status-pill">{adminStatus}</span>
          </div>

          <div className="form-grid">
            <label>
              <span>Email</span>
              <input
                required
                type="email"
                value={adminForm.email}
                onChange={(event) => updateAdminField('email', event.target.value)}
                placeholder="performer@example.com"
              />
            </label>

            <label>
              <span>Display name</span>
              <input
                value={adminForm.displayName}
                onChange={(event) => updateAdminField('displayName', event.target.value)}
                placeholder="Optional"
              />
            </label>

            <label className="full-width">
              <span>Google subject</span>
              <input
                value={adminForm.googleSubject}
                onChange={(event) => updateAdminField('googleSubject', event.target.value)}
                placeholder="Optional until first Google sign-in"
                disabled={adminMode === 'edit' && selectedAdminUser?.isEnrolled === true}
              />
            </label>

            <label>
              <span>Role</span>
              <select
                value={adminForm.role}
                onChange={(event) =>
                  updateAdminField('role', event.target.value as AdminUserForm['role'])
                }
              >
                <option value="User">User</option>
                <option value="Admin">Admin</option>
              </select>
            </label>

            <label className="checkbox-field">
              <input
                type="checkbox"
                checked={adminForm.isActive}
                onChange={(event) => updateAdminField('isActive', event.target.checked)}
              />
              <span>Account is active and allowed to sign in</span>
            </label>
          </div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isAdminLoading}>
              {adminMode === 'create' ? 'Enrol user' : 'Save enrolment'}
            </button>
            <button className="ghost-button" onClick={startAdminCreate} type="button">
              Reset enrolment form
            </button>
          </div>
          <p className="auth-note">
            Admins can pre-provision by email only. If Google subject is blank, Glovelly
            will bind it on the user’s first successful Google sign-in.
          </p>
        </form>
      </div>
    </section>
  )

  const renderGigsSection = () => {
    const selectedGigClient =
      clients.find((client) => client.id === selectedGig?.clientId) ?? null

    return (
      <section className="section-layout">
        <div className="gig-workspace">
          <div className="panel">
            <div className="panel-heading">
              <div>
                <p className="section-label">Bookings</p>
                <h2>Gigs</h2>
              </div>
              <button className="ghost-button" onClick={startGigCreate} type="button">
                New gig
              </button>
            </div>

            <label className="search-field">
              <span>Search</span>
              <input
                type="search"
                placeholder="Client, title, venue..."
                value={gigSearchQuery}
                onChange={(event) => setGigSearchQuery(event.target.value)}
              />
            </label>

            <div className="gig-summary-grid">
              <article>
                <span>{gigs.length}</span>
                <p>saved gigs</p>
              </article>
              <article>
                <span>{plannedGigCount}</span>
                <p>planned</p>
              </article>
              <article>
                <span>{gigs.filter((gig) => gig.status === 'Completed').length}</span>
                <p>completed</p>
              </article>
            </div>

            <div className="client-list">
              {filteredGigs.map((gig) => {
                const clientName =
                  clients.find((client) => client.id === gig.clientId)?.name ??
                  'Unknown client'

                return (
                  <button
                    key={gig.id}
                    className={`client-card ${selectedGig?.id === gig.id ? 'selected' : ''}`}
                    onClick={() => setSelectedGigId(gig.id)}
                    type="button"
                  >
                    <div>
                      <strong>{gig.title}</strong>
                      <span>{clientName}</span>
                    </div>
                    <small className="gig-card-meta">
                      {formatDate(gig.date)} · {gig.venue}
                    </small>
                    <small className="gig-card-meta">
                      {formatCurrency(gig.fee)} · {formatGigStatus(gig.status)}
                    </small>
                  </button>
                )
              })}

              {filteredGigs.length === 0 && (
                <div className="empty-state">
                  <strong>No gigs match that search.</strong>
                  <p>Create the first gig or try a different term.</p>
                </div>
              )}
            </div>
          </div>

          <div className="panel">
            <div className="panel-heading">
              <div>
                <p className="section-label">Gig Overview</p>
                <h2>{selectedGig?.title ?? 'No gig selected'}</h2>
              </div>
              <div className="actions">
                <button
                  className="ghost-button"
                  onClick={startGigEdit}
                  type="button"
                  disabled={!selectedGig}
                >
                  Edit gig
                </button>
              </div>
            </div>

            {selectedGig ? (
              <>
                <div className="detail-grid">
                  <article>
                    <p className="detail-label">Client</p>
                    <strong>{selectedGigClient?.name ?? 'Unknown client'}</strong>
                  </article>
                  <article>
                    <p className="detail-label">Status</p>
                    <strong>{formatGigStatus(selectedGig.status)}</strong>
                  </article>
                  <article>
                    <p className="detail-label">Date</p>
                    <strong>{formatDate(selectedGig.date)}</strong>
                  </article>
                  <article>
                    <p className="detail-label">Fee</p>
                    <strong>{formatCurrency(selectedGig.fee)}</strong>
                  </article>
                  <article className="full-width">
                    <p className="detail-label">Location</p>
                    <strong>{selectedGig.venue}</strong>
                  </article>
                  <article>
                    <p className="detail-label">Driving</p>
                    <strong>{selectedGig.wasDriving ? 'Yes' : 'No'}</strong>
                  </article>
                  <article>
                    <p className="detail-label">Invoice link</p>
                    <strong>{selectedGig.isInvoiced ? 'Linked' : 'Not invoiced yet'}</strong>
                  </article>
                  <article className="full-width">
                    <p className="detail-label">Notes</p>
                    <span>{selectedGig.notes?.trim() || 'No notes yet.'}</span>
                  </article>
                </div>

                <div className="gig-timeline-note">
                  <p className="detail-label">Ready for future invoicing</p>
                  <span>
                    This record now carries the client, date, venue, fee and delivery
                    status that invoice workflows can build on next.
                  </span>
                </div>
              </>
            ) : (
              <div className="empty-state roomy">
                <strong>Select a gig to review its details.</strong>
                <p>The browse pane shows the commercial snapshot that matters later.</p>
              </div>
            )}
          </div>

          <form className="panel" onSubmit={handleGigSubmit}>
            <div className="panel-heading">
              <div>
                <p className="section-label">Management Pane</p>
                <h2>{gigMode === 'create' ? 'Create gig' : 'Update gig'}</h2>
              </div>
              <span className="status-pill">{gigStatus}</span>
            </div>

            <div className="form-grid">
              <label>
                <span>Client</span>
                <select
                  required
                  value={gigForm.clientId}
                  onChange={(event) => updateGigField('clientId', event.target.value)}
                >
                  <option value="">Select a client</option>
                  {clients.map((client) => (
                    <option key={client.id} value={client.id}>
                      {client.name}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                <span>Date</span>
                <input
                  required
                  type="date"
                  value={gigForm.date}
                  onChange={(event) => updateGigField('date', event.target.value)}
                />
              </label>

              <label className="full-width">
                <span>Title / description</span>
                <input
                  required
                  value={gigForm.title}
                  onChange={(event) => updateGigField('title', event.target.value)}
                  placeholder="Spring product launch"
                />
              </label>

              <label className="full-width">
                <span>Location / venue</span>
                <input
                  required
                  value={gigForm.venue}
                  onChange={(event) => updateGigField('venue', event.target.value)}
                  placeholder="Albert Hall, Manchester"
                />
              </label>

              <label>
                <span>Fee</span>
                <input
                  required
                  inputMode="decimal"
                  value={gigForm.fee}
                  onChange={(event) => updateGigField('fee', event.target.value)}
                  placeholder="650"
                />
              </label>

              <label>
                <span>Status</span>
                <select
                  value={gigForm.status}
                  onChange={(event) =>
                    updateGigField('status', event.target.value as GigStatus)
                  }
                >
                  <option value="Confirmed">Planned</option>
                  <option value="Completed">Completed</option>
                  <option value="Cancelled">Cancelled</option>
                  <option value="Draft">Draft</option>
                </select>
              </label>

              <label className="checkbox-field full-width">
                <input
                  type="checkbox"
                  checked={gigForm.wasDriving}
                  onChange={(event) => updateGigField('wasDriving', event.target.checked)}
                />
                <span>I was driving for this gig</span>
              </label>

              <label className="full-width">
                <span>Notes</span>
                <textarea
                  rows={5}
                  value={gigForm.notes}
                  onChange={(event) => updateGigField('notes', event.target.value)}
                  placeholder="Optional commercial or logistics notes"
                />
              </label>
            </div>

            <div className="form-actions">
              <button
                className="primary-button"
                type="submit"
                disabled={isGigLoading || clients.length === 0}
              >
                {gigMode === 'create' ? 'Save gig' : 'Update gig'}
              </button>
              <button className="ghost-button" onClick={startGigCreate} type="button">
                Reset form
              </button>
            </div>

            {clients.length === 0 && (
              <p className="auth-note">
                Add a client first. Every gig is intentionally tied to a client record.
              </p>
            )}
          </form>
        </div>
      </section>
    )
  }

  return (
    <main className="app-shell">
      <section className="hero-panel app-frame">
        <aside className="nav-panel">
          <div className="nav-intro">
            <p className="eyebrow">Workspace</p>
            <h1>Glovelly keeps each area in its own lane.</h1>
            <p className="hero-text">
              Move between client records, admin access and the next planned gigs
              workspace without stacking everything into one screen.
            </p>
          </div>

          <nav className="nav-menu" aria-label="Primary">
            {navigationItems.map((item) => (
              <button
                key={item.id}
                className={`nav-item ${activeSection === item.id ? 'selected' : ''}`}
                onClick={() => setActiveSection(item.id)}
                type="button"
                disabled={item.disabled}
              >
                <span className="nav-meta">{item.eyebrow}</span>
                <strong>{item.label}</strong>
                <span>{item.description}</span>
              </button>
            ))}
          </nav>

          <div className="hero-mascot">
            <img src="/gordon-512.png" alt="Gordon the Glovelly mascot" />
            <div>
              <p className="section-label">Meet Gordon</p>
              <strong>Mozart wig. Rubber chicken. Unreasonably good taste in admin.</strong>
            </div>
          </div>
        </aside>

        <div className="content-shell">
          <div className="content-header panel">
            <div className="content-header-top">
              <div className="content-header-copy">
                <p className="eyebrow">{currentSection?.eyebrow ?? 'Workspace'}</p>
                <h2>{currentSection?.label ?? 'Glovelly'}</h2>
                <p className="hero-text">{currentSection?.description}</p>
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

            <div className="content-header-aside">
              <div className="hero-metrics">
                <article>
                  <span>{clients.length}</span>
                  <p>clients on file</p>
                </article>
                <article>
                  <span>{secondaryMetricValue}</span>
                  <p>{secondaryMetricLabel}</p>
                </article>
                <article>
                  <span>{isApiConnected ? 'Live' : 'Offline'}</span>
                  <p>{activeSection === 'gigs' ? 'workspace status' : 'data source'}</p>
                </article>
              </div>
            </div>
          </div>

          {activeSection === 'clients' && renderClientsSection()}
          {activeSection === 'admin' && isAdmin && renderAdminSection()}
          {activeSection === 'gigs' && renderGigsSection()}
        </div>
      </section>

      {isUserSettingsOpen && (
        <div
          className="settings-overlay"
          onClick={closeUserSettings}
          role="presentation"
        >
          <section
            aria-labelledby="user-settings-title"
            aria-modal="true"
            className="settings-modal panel"
            onClick={(event) => event.stopPropagation()}
            role="dialog"
          >
            <div className="panel-heading">
              <div>
                <p className="section-label">User settings</p>
                <h2 id="user-settings-title">Mileage defaults</h2>
              </div>
              <button
                className="ghost-button"
                onClick={closeUserSettings}
                type="button"
              >
                Close
              </button>
            </div>

            <p className="hero-text settings-intro">
              These rates are used as your personal fallback when a client does not
              have custom mileage pricing configured.
            </p>

            <form className="settings-form" onSubmit={handleUserSettingsSubmit}>
              <div className="form-grid">
                <label>
                  <span>Mileage rate</span>
                  <input
                    inputMode="decimal"
                    placeholder="0.45"
                    type="text"
                    value={userSettingsForm.mileageRate}
                    onChange={(event) =>
                      updateUserSettingsField('mileageRate', event.target.value)
                    }
                  />
                </label>

                <label>
                  <span>Passenger mileage rate</span>
                  <input
                    inputMode="decimal"
                    placeholder="0.10"
                    type="text"
                    value={userSettingsForm.passengerMileageRate}
                    onChange={(event) =>
                      updateUserSettingsField(
                        'passengerMileageRate',
                        event.target.value
                      )
                    }
                  />
                </label>
              </div>

              <div className="settings-note">
                Leave a field blank if you do not want a default for that rate.
              </div>

              <div className="form-actions">
                <button
                  className="primary-button"
                  type="submit"
                  disabled={isUserSettingsSaving}
                >
                  {isUserSettingsSaving ? 'Saving…' : 'Save settings'}
                </button>
                <span className="status-pill">{userSettingsStatus}</span>
              </div>
            </form>
          </section>
        </div>
      )}

      {isClientSettingsOpen && selectedClient && (
        <div
          className="settings-overlay"
          onClick={closeClientSettings}
          role="presentation"
        >
          <section
            aria-labelledby="client-settings-title"
            aria-modal="true"
            className="settings-modal panel"
            onClick={(event) => event.stopPropagation()}
            role="dialog"
          >
            <div className="panel-heading">
              <div>
                <p className="section-label">Client settings</p>
                <h2 id="client-settings-title">{selectedClient.name}</h2>
              </div>
              <button
                className="ghost-button"
                onClick={closeClientSettings}
                type="button"
              >
                Close
              </button>
            </div>

            <p className="hero-text settings-intro">
              Leave a field blank to inherit the default from your own user settings.
              Add a value here only when this client needs a special rate.
            </p>

            <div className="detail-grid client-settings-preview">
              <article className={selectedClient.mileageRate === null ? 'setting-card inherited' : 'setting-card override'}>
                <p className="detail-label">Current mileage rule</p>
                <strong>{formatRate(selectedClient.mileageRate ?? authUser?.mileageRate ?? null)}</strong>
                <span>
                  {selectedClient.mileageRate === null
                    ? 'Inherited from your user settings'
                    : 'Overriding your default'}
                </span>
              </article>
              <article className={selectedClient.passengerMileageRate === null ? 'setting-card inherited' : 'setting-card override'}>
                <p className="detail-label">Current passenger rule</p>
                <strong>
                  {formatRate(
                    selectedClient.passengerMileageRate ??
                      authUser?.passengerMileageRate ??
                      null
                  )}
                </strong>
                <span>
                  {selectedClient.passengerMileageRate === null
                    ? 'Inherited from your user settings'
                    : 'Overriding your default'}
                </span>
              </article>
            </div>

            <form className="settings-form" onSubmit={handleClientSettingsSubmit}>
              <div className="form-grid">
                <label>
                  <span>Mileage rate override</span>
                  <input
                    inputMode="decimal"
                    placeholder={
                      authUser?.mileageRate === null || authUser?.mileageRate === undefined
                        ? 'Use default'
                        : `Default ${authUser.mileageRate}`
                    }
                    type="text"
                    value={clientSettingsForm.mileageRate}
                    onChange={(event) =>
                      updateClientSettingsField('mileageRate', event.target.value)
                    }
                  />
                </label>

                <label>
                  <span>Passenger rate override</span>
                  <input
                    inputMode="decimal"
                    placeholder={
                      authUser?.passengerMileageRate === null ||
                      authUser?.passengerMileageRate === undefined
                        ? 'Use default'
                        : `Default ${authUser.passengerMileageRate}`
                    }
                    type="text"
                    value={clientSettingsForm.passengerMileageRate}
                    onChange={(event) =>
                      updateClientSettingsField(
                        'passengerMileageRate',
                        event.target.value
                      )
                    }
                  />
                </label>
              </div>

              <div className="settings-note">
                Blank means inherited. A filled value becomes a client-specific override.
              </div>

              <div className="form-actions">
                <button
                  className="primary-button"
                  type="submit"
                  disabled={isClientSettingsSaving}
                >
                  {isClientSettingsSaving ? 'Saving…' : 'Save client settings'}
                </button>
                <span className="status-pill">{clientSettingsStatus}</span>
              </div>
            </form>
          </section>
        </div>
      )}
    </main>
  )
}

export default App
