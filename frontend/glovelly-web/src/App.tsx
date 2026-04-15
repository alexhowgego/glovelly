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

const seedClients: Client[] = [
  {
    id: 'c1',
    name: 'Fox & Finch Events',
    email: 'bookings@foxandfinch.co.uk',
    billingAddress: {
      line1: '12 Chapel Street',
      line2: '',
      city: 'Manchester',
      stateOrCounty: 'Greater Manchester',
      postalCode: 'M3 5JZ',
      country: 'United Kingdom',
    },
  },
  {
    id: 'c2',
    name: 'Northlight Weddings',
    email: 'accounts@northlightweddings.com',
    billingAddress: {
      line1: '7 Hawthorn Mews',
      line2: '',
      city: 'Leeds',
      stateOrCounty: 'West Yorkshire',
      postalCode: 'LS1 4PR',
      country: 'United Kingdom',
    },
  },
  {
    id: 'c3',
    name: 'Riverside Arts Centre',
    email: 'finance@riversidearts.org',
    billingAddress: {
      line1: '84 Mill Lane',
      line2: 'Studio 3',
      city: 'Bristol',
      stateOrCounty: 'Bristol',
      postalCode: 'BS1 6QX',
      country: 'United Kingdom',
    },
  },
]

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

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '')

function App() {
  const [clients, setClients] = useState<Client[]>(seedClients)
  const [selectedClientId, setSelectedClientId] = useState<string>(seedClients[0]?.id ?? '')
  const [searchQuery, setSearchQuery] = useState('')
  const [mode, setMode] = useState<'create' | 'edit'>('create')
  const [form, setForm] = useState<ClientForm>(emptyForm)
  const [status, setStatus] = useState('Prototype mode using sample clients.')
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    if (!apiBaseUrl) {
      return
    }

    let ignore = false

    const loadClients = async () => {
      setIsLoading(true)

      try {
        const response = await fetch(`${apiBaseUrl}/clients`)
        if (!response.ok) {
          throw new Error('Unable to load clients.')
        }

        const data = (await response.json()) as Client[]
        if (ignore) {
          return
        }

        setClients(data)
        setSelectedClientId(data[0]?.id ?? '')
        setStatus(
          data.length > 0
            ? 'Connected to the API.'
            : 'Connected to the API. No clients yet.'
        )
      } catch {
        if (!ignore) {
          setStatus('API unavailable, showing sample clients.')
        }
      } finally {
        if (!ignore) {
          setIsLoading(false)
        }
      }
    }

    void loadClients()

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

  const selectedClient =
    filteredClients.find((client) => client.id === selectedClientId) ??
    clients.find((client) => client.id === selectedClientId) ??
    filteredClients[0] ??
    null

  useEffect(() => {
    if (selectedClient) {
      setSelectedClientId(selectedClient.id)
    }
  }, [selectedClient])

  const activeCities = new Set(clients.map((client) => client.billingAddress.city)).size

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

  const updateField = (
    field: keyof ClientForm,
    value: string | Address
  ) => {
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

    if (!payload.name || !payload.email || !payload.billingAddress.line1 || !payload.billingAddress.city) {
      setStatus('Name, email, address line 1 and city are required.')
      return
    }

    setIsLoading(true)

    try {
      if (apiBaseUrl) {
        const isEdit = mode === 'edit' && selectedClient
        const endpoint = isEdit
          ? `${apiBaseUrl}/clients/${selectedClient.id}`
          : `${apiBaseUrl}/clients`

        const response = await fetch(endpoint, {
          method: isEdit ? 'PUT' : 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(payload),
        })

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
      } else {
        if (mode === 'edit' && selectedClient) {
          const updatedClient: Client = {
            id: selectedClient.id,
            ...payload,
          }

          setClients((current) =>
            current.map((client) =>
              client.id === updatedClient.id ? updatedClient : client
            )
          )
          setSelectedClientId(updatedClient.id)
          setStatus('Client updated locally.')
        } else {
          const createdClient: Client = {
            id: crypto.randomUUID(),
            ...payload,
          }

          setClients((current) => [createdClient, ...current])
          setSelectedClientId(createdClient.id)
          setMode('edit')
          setStatus('Client created locally.')
        }
      }
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
      if (apiBaseUrl) {
        const response = await fetch(`${apiBaseUrl}/clients/${selectedClient.id}`, {
          method: 'DELETE',
        })

        if (!response.ok) {
          throw new Error('Delete failed.')
        }
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
      setStatus(apiBaseUrl ? 'Client deleted.' : 'Client deleted locally.')
    } catch {
      setStatus('Unable to delete right now.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <main className="app-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <p className="eyebrow">Client Management</p>
          <h1>Keep booking contacts tidy without feeling like you opened an ERP.</h1>
          <p className="hero-text">
            A lightweight workspace for the people behind each gig, with room for
            invoicing details and billing addresses from the start.
          </p>
        </div>

        <div className="hero-brand">
          <div className="hero-mascot">
            <img src="/gordon-512.png" alt="Gordon the Glovelly mascot" />
            <div>
              <p className="section-label">Meet Gordon</p>
              <strong>Mozart wig. Rubber chicken. Unreasonably good taste in admin.</strong>
            </div>
          </div>

          <div className="hero-metrics">
            <article>
              <span>{clients.length}</span>
              <p>clients on file</p>
            </article>
            <article>
              <span>{activeCities}</span>
              <p>active cities</p>
            </article>
            <article>
              <span>{apiBaseUrl ? 'Live' : 'Demo'}</span>
              <p>data source</p>
            </article>
          </div>
        </div>
      </section>

      <section className="workspace">
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
                <strong>No clients match that search.</strong>
                <p>Try a different term or add a fresh client profile.</p>
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
                onChange={(event) =>
                  updateAddressField('stateOrCounty', event.target.value)
                }
                placeholder="Greater Manchester"
              />
            </label>

            <label>
              <span>Postal code</span>
              <input
                value={form.billingAddress.postalCode}
                onChange={(event) =>
                  updateAddressField('postalCode', event.target.value)
                }
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
              {mode === 'create' ? 'Create client' : 'Save changes'}
            </button>
            <button className="ghost-button" type="button" onClick={startCreating}>
              Clear form
            </button>
          </div>
        </form>
      </section>
    </main>
  )
}

export default App
