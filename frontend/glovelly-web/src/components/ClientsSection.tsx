import type { FormEvent } from 'react'
import type { Client, ClientForm } from '../appShared'

type ClientsSectionProps = {
  filteredClients: Client[]
  form: ClientForm
  isApiConnected: boolean
  isEditorOpen: boolean
  isMonthlyInvoiceReady: boolean
  isInvoiceLoading: boolean
  isLoading: boolean
  monthlyInvoiceHelperText: string
  monthlyInvoiceMonth: string
  onCloseEditor: () => void
  mode: 'create' | 'edit'
  onDelete: () => void
  onGenerateMonthlyInvoice: () => void
  onMonthlyInvoiceMonthChange: (value: string) => void
  onOpenClientSettings: () => void
  onResetForm: () => void
  onSearchQueryChange: (value: string) => void
  onSelectClient: (clientId: string) => void
  onStartEditing: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateAddressField: (field: keyof Client['billingAddress'], value: string) => void
  onUpdateField: (field: keyof ClientForm, value: string | Client['billingAddress']) => void
  searchQuery: string
  selectedClient: Client | null
  status: string
}

export function ClientsSection({
  filteredClients,
  form,
  isApiConnected,
  isEditorOpen,
  isMonthlyInvoiceReady,
  isInvoiceLoading,
  isLoading,
  monthlyInvoiceHelperText,
  monthlyInvoiceMonth,
  onCloseEditor,
  mode,
  onDelete,
  onGenerateMonthlyInvoice,
  onMonthlyInvoiceMonthChange,
  onOpenClientSettings,
  onResetForm,
  onSearchQueryChange,
  onSelectClient,
  onStartEditing,
  onSubmit,
  onUpdateAddressField,
  onUpdateField,
  searchQuery,
  selectedClient,
  status,
}: ClientsSectionProps) {
  return (
    <section className="section-layout">
      <div className="workspace">
        <div className="clients-panel panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Directory</p>
              <h2>Clients</h2>
            </div>
            <button className="ghost-button" onClick={onResetForm} type="button">
              New client
            </button>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              type="search"
              placeholder="Name, email, city..."
              value={searchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
            />
          </label>

          <div className="client-list">
            {filteredClients.map((client) => (
              <button
                key={client.id}
                className={`client-card ${selectedClient?.id === client.id ? 'selected' : ''}`}
                onClick={() => onSelectClient(client.id)}
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
                <strong>
                  {isApiConnected
                    ? 'No clients match that search.'
                    : 'No client data available.'}
                </strong>
                <p>
                  {isApiConnected
                    ? 'Try a different term or add a fresh client profile.'
                    : 'We could not load client details right now. Refresh and try again.'}
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
                onClick={onOpenClientSettings}
                type="button"
                disabled={!selectedClient}
              >
                Settings
              </button>
              <button
                className="ghost-button"
                onClick={onStartEditing}
                type="button"
                disabled={!selectedClient}
              >
                Edit
              </button>
              <button
                className="danger-button"
                onClick={onDelete}
                type="button"
                disabled={!selectedClient || isLoading}
              >
                Delete
              </button>
            </div>
          </div>

          {selectedClient ? (
            <>
              <div className="gig-timeline-note">
                <p className="detail-label">Monthly invoice run</p>
                <div className="invoice-adjustment-form">
                  <label>
                    Month
                    <input
                      type="month"
                      value={monthlyInvoiceMonth}
                      onChange={(event) =>
                        onMonthlyInvoiceMonthChange(event.target.value)
                      }
                      disabled={isInvoiceLoading}
                    />
                  </label>
                  <button
                    className="primary-button"
                    onClick={onGenerateMonthlyInvoice}
                    type="button"
                    disabled={isInvoiceLoading || !isMonthlyInvoiceReady}
                  >
                    Generate monthly invoice
                  </button>
                </div>
                <span>{monthlyInvoiceHelperText}</span>
              </div>

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
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a client to see billing details.</strong>
              <p>Billing details and contact information will appear here.</p>
            </div>
          )}
        </div>

        <div className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
          <form
            aria-hidden={!isEditorOpen}
            className="editor-panel panel"
            onSubmit={onSubmit}
          >
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
                  onChange={(event) => onUpdateField('name', event.target.value)}
                  placeholder="Fox & Finch Events"
                />
              </label>

              <label>
                <span>Email</span>
                <input
                  required
                  type="email"
                  value={form.email}
                  onChange={(event) => onUpdateField('email', event.target.value)}
                  placeholder="accounts@example.com"
                />
              </label>

              <label className="full-width">
                <span>Address line 1</span>
                <input
                  required
                  value={form.billingAddress.line1}
                  onChange={(event) => onUpdateAddressField('line1', event.target.value)}
                  placeholder="12 Chapel Street"
                />
              </label>

              <label className="full-width">
                <span>Address line 2</span>
                <input
                  value={form.billingAddress.line2 ?? ''}
                  onChange={(event) => onUpdateAddressField('line2', event.target.value)}
                  placeholder="Optional"
                />
              </label>

              <label>
                <span>City</span>
                <input
                  required
                  value={form.billingAddress.city}
                  onChange={(event) => onUpdateAddressField('city', event.target.value)}
                  placeholder="Manchester"
                />
              </label>

              <label>
                <span>County / state</span>
                <input
                  value={form.billingAddress.stateOrCounty ?? ''}
                  onChange={(event) =>
                    onUpdateAddressField('stateOrCounty', event.target.value)
                  }
                  placeholder="Greater Manchester"
                />
              </label>

              <label>
                <span>Postal code</span>
                <input
                  value={form.billingAddress.postalCode ?? ''}
                  onChange={(event) => onUpdateAddressField('postalCode', event.target.value)}
                  placeholder="M3 5JZ"
                />
              </label>

              <label>
                <span>Country</span>
                <input
                  value={form.billingAddress.country ?? ''}
                  onChange={(event) => onUpdateAddressField('country', event.target.value)}
                  placeholder="United Kingdom"
                />
              </label>
            </div>

            <div className="form-actions">
              <button
                className="primary-button"
                data-close-after-save="true"
                type="submit"
                disabled={isLoading}
              >
                Save and close
              </button>
              <button
                className="ghost-button"
                data-close-after-save="false"
                type="submit"
                disabled={isLoading}
              >
                Save
              </button>
              <button className="ghost-button" onClick={onCloseEditor} type="button">
                Discard changes
              </button>
            </div>
          </form>
        </div>
      </div>
    </section>
  )
}
