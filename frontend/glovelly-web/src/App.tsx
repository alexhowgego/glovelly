import { useEffect, useMemo, useState } from 'react'
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

type AppSection = 'clients' | 'admin' | 'gigs'

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

const defaultAdminStatus = 'User enrolment tools ready.'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

function buildApiUrl(path: string) {
  return `${apiBaseUrl}${path}`
}

function buildReturnUrl() {
  return window.location.href
}

async function fetchWithSession(input: string, init?: RequestInit) {
  return fetch(input, {
    credentials: 'include',
    ...init,
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

function App() {
  const [activeSection, setActiveSection] = useState<AppSection>('clients')
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [searchQuery, setSearchQuery] = useState('')
  const [mode, setMode] = useState<'create' | 'edit'>('create')
  const [form, setForm] = useState<ClientForm>(emptyForm)
  const [status, setStatus] = useState('Checking your session...')
  const [isLoading, setIsLoading] = useState(false)
  const [isApiConnected, setIsApiConnected] = useState(false)
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [authUser, setAuthUser] = useState<AuthUser | null>(null)
  const [isCheckingSession, setIsCheckingSession] = useState(true)
  const [shouldCloseBrowserNotice, setShouldCloseBrowserNotice] = useState(false)

  const [adminUsers, setAdminUsers] = useState<AdminUser[]>([])
  const [selectedAdminUserId, setSelectedAdminUserId] = useState<string>('')
  const [adminSearchQuery, setAdminSearchQuery] = useState('')
  const [adminMode, setAdminMode] = useState<'create' | 'edit'>('create')
  const [adminForm, setAdminForm] = useState<AdminUserForm>(emptyAdminForm)
  const [adminStatus, setAdminStatus] = useState(defaultAdminStatus)
  const [isAdminLoading, setIsAdminLoading] = useState(false)

  const isAdmin = authUser?.role === 'Admin'

  useEffect(() => {
    if (!isAdmin && activeSection === 'admin') {
      setActiveSection('clients')
    }
  }, [activeSection, isAdmin])

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

        const clientsResponse = await fetchWithSession(buildApiUrl('/clients'))
        if (clientsResponse.status === 401) {
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

        const data = (await clientsResponse.json()) as Client[]
        if (ignore) {
          return
        }

        setClients(data)
        setSelectedClientId(data[0]?.id ?? '')
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

  const activeCities = new Set(clients.map((client) => client.billingAddress.city)).size
  const activeUsersCount = adminUsers.filter((user) => user.isActive).length
  const totalAdmins = adminUsers.filter((user) => user.role === 'Admin').length

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
      eyebrow: 'Next',
      description: 'Draft home for scheduling, bookings and delivery status.',
    },
  ]

  const currentSection = navigationItems.find((item) => item.id === activeSection)
  const secondaryMetricValue =
    activeSection === 'admin' ? activeUsersCount : activeSection === 'gigs' ? 'Planned' : activeCities
  const secondaryMetricLabel =
    activeSection === 'admin'
      ? 'active accounts'
      : activeSection === 'gigs'
        ? 'workflow stage'
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

  const signIn = () => {
    const loginUrl = buildApiUrl(
      `/auth/login?returnUrl=${encodeURIComponent(buildReturnUrl())}`
    )
    window.location.assign(loginUrl)
  }

  const signOut = async () => {
    setIsLoading(true)

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
      resetAdminWorkspace()
      setShouldCloseBrowserNotice(true)
      setStatus('Signed out. Close your browser to fully end the Google session.')
    } catch {
      setStatus('Unable to sign out right now.')
    } finally {
      setIsLoading(false)
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

  const renderGigsSection = () => (
    <section className="section-layout">
      <div className="gigs-draft panel">
        <div className="panel-heading">
          <div>
            <p className="section-label">Draft Section</p>
            <h2>Gigs</h2>
          </div>
          <span className="status-pill">Coming next</span>
        </div>

        <p className="hero-text">
          This space is reserved for the booking workflow that links clients,
          dates, venues, performers and delivery status into one operational view.
        </p>

        <div className="draft-grid">
          <article>
            <p className="detail-label">Likely modules</p>
            <strong>Schedule, assignment, logistics and invoicing handoff.</strong>
          </article>
          <article>
            <p className="detail-label">Suggested shape</p>
            <strong>List of gigs, timeline summary, and a detailed edit pane.</strong>
          </article>
          <article className="full-width">
            <p className="detail-label">Why keep it here now</p>
            <span>
              The navigation is already ready for the next phase, so we can add gigs
              without rearranging the whole app shell a second time.
            </span>
          </article>
        </div>
      </div>
    </section>
  )

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
            <div className="content-header-copy">
              <p className="eyebrow">{currentSection?.eyebrow ?? 'Workspace'}</p>
              <h2>{currentSection?.label ?? 'Glovelly'}</h2>
              <p className="hero-text">{currentSection?.description}</p>
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
                  <span>{activeSection === 'gigs' ? 'Draft' : isApiConnected ? 'Live' : 'Offline'}</span>
                  <p>{activeSection === 'gigs' ? 'section status' : 'data source'}</p>
                </article>
              </div>

              <div className="session-card">
                <div>
                  <p className="section-label">Signed In</p>
                  <strong>{authUser?.name ?? authUser?.email}</strong>
                  <span>
                    {authUser?.email}
                    {isAdmin ? ' · Administrator' : ''}
                  </span>
                </div>
                <button
                  className="ghost-button"
                  onClick={signOut}
                  type="button"
                  disabled={isLoading || isAdminLoading}
                >
                  Sign out
                </button>
              </div>
            </div>
          </div>

          {activeSection === 'clients' && renderClientsSection()}
          {activeSection === 'admin' && isAdmin && renderAdminSection()}
          {activeSection === 'gigs' && renderGigsSection()}
        </div>
      </section>
    </main>
  )
}

export default App
