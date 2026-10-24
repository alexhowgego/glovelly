import { useEffect, useRef } from 'react'
import type { FormEvent } from 'react'
import type { Client, ClientForm, ClientSort, ClientSortKey } from '../types'
import { TrashIcon } from './TrashIcon'
import { clientLocationQuery } from '../recordLocationSource'
import { RecordVisualOverview } from './RecordVisualOverview'
import { GoogleMapsLocationLink, RecordMapPreview } from './RecordMapPreview'
import { RecordActionDock } from './RecordActionDock'

type ClientsSectionProps = {
  onSessionExpired?: (message: string) => void
  filteredClients: Client[]
  form: ClientForm
  clientSort: ClientSort
  canDeleteSelectedClient: boolean
  clientDeleteHelperText: string
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
  onSortChange: (sort: ClientSort) => void
  onStartEditing: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateAddressField: (field: keyof Client['billingAddress'], value: string) => void
  onUpdateField: (field: keyof ClientForm, value: string | Client['billingAddress']) => void
  searchQuery: string
  selectedClient: Client | null
  status: string
}

export function ClientsSection({
  onSessionExpired,
  filteredClients,
  form,
  clientSort,
  canDeleteSelectedClient,
  clientDeleteHelperText,
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
  onSortChange,
  onStartEditing,
  onSubmit,
  onUpdateAddressField,
  onUpdateField,
  searchQuery,
  selectedClient,
  status,
}: ClientsSectionProps) {
  const editorSlotRef = useRef<HTMLDivElement | null>(null)
  const clientSortOptions: { value: ClientSortKey; label: string }[] = [
    { value: 'name', label: 'Client' },
    { value: 'email', label: 'Email' },
    { value: 'city', label: 'City' },
    { value: 'country', label: 'Country' },
  ]

  useEffect(() => {
    if (!isEditorOpen || !window.matchMedia('(max-width: 1180px)').matches) {
      return
    }

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      editorSlotRef.current?.scrollIntoView({ behavior: 'instant', block: 'start' })
      return
    }

    const timeout = window.setTimeout(() => {
      editorSlotRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }, 80)
    return () => window.clearTimeout(timeout)
  }, [isEditorOpen])

  return (
    <section className="section-layout">
      <div className="workspace">
        <div className="clients-panel panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Directory</p>
              <h2>Clients</h2>
            </div>
            <button className="ghost-button" data-testid="new-client-button" onClick={onResetForm} type="button">
              New client
            </button>
          </div>

          <div className="compact-list-controls" aria-label="Client list controls">
            <div className="compact-list-main-controls">
              <label className="search-field compact-search-field">
                <span>Search</span>
                <input
                  data-testid="client-search-input"
                  type="search"
                  placeholder="Name, email, city..."
                  value={searchQuery}
                  onChange={(event) => onSearchQueryChange(event.target.value)}
                />
              </label>
              <label>
                <span>Sort by</span>
                <select
                  value={clientSort.key}
                  onChange={(event) =>
                    onSortChange({ ...clientSort, key: event.target.value as ClientSortKey })
                  }
                >
                  {clientSortOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>
              <button
                className="compact-sort-direction"
                type="button"
                aria-label={
                  clientSort.direction === 'asc'
                    ? 'Sort ascending. Click to sort descending.'
                    : 'Sort descending. Click to sort ascending.'
                }
                title={clientSort.direction === 'asc' ? 'Ascending' : 'Descending'}
                onClick={() =>
                  onSortChange({
                    ...clientSort,
                    direction: clientSort.direction === 'asc' ? 'desc' : 'asc',
                  })
                }
              >
                {clientSort.direction === 'asc' ? '↑' : '↓'}
              </button>
            </div>
          </div>

          <div className="compact-record-list client-record-list" aria-label="Clients">
            <div className="compact-record-header client-record-row">
              <span>Client</span>
              <span>Email</span>
              <span>City</span>
              <span>Country</span>
            </div>
            {filteredClients.map((client) => (
              <button
                key={client.id}
                className={`compact-record-row client-record-row ${selectedClient?.id === client.id ? 'selected' : ''}`}
                data-testid="client-card"
                onClick={() => onSelectClient(client.id)}
                type="button"
              >
                <div className="compact-primary-cell">
                  <strong>{client.name}</strong>
                  <span>{client.email}</span>
                </div>
                <span>{client.email}</span>
                <span>{client.billingAddress.city || 'No city set'}</span>
                <span>{client.billingAddress.country || 'No country set'}</span>
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

        <div className="detail-panel panel record-overview">
          <div className="panel-heading">
            <div>
              <p className="section-label">Overview</p>
              <h2 tabIndex={-1}>{selectedClient?.name ?? 'No client selected'}</h2>
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
                className={`ghost-button editor-toggle ${isEditorOpen ? 'active' : ''}`}
                data-testid="client-edit-button"
                onClick={isEditorOpen ? onCloseEditor : onStartEditing}
                type="button"
                disabled={!selectedClient}
                aria-expanded={isEditorOpen}
              >
                Edit
              </button>
              <button
                className="danger-button"
                onClick={onDelete}
                type="button"
                disabled={!canDeleteSelectedClient || isLoading}
                title={clientDeleteHelperText}
              >
                <TrashIcon />
                Delete
              </button>
            </div>
          </div>

          {selectedClient ? (
            <>
              <RecordVisualOverview key={`${selectedClient.id}:${selectedClient.mapPreviewSourceFingerprint}`} factsColumns={1}
                visual={clientLocationQuery(selectedClient.billingAddress) && selectedClient.mapPreviewSourceFingerprint ? (bucket, unavailable) =>
                  <RecordMapPreview kind="clients" id={selectedClient.id} source={selectedClient.mapPreviewSourceFingerprint!}
                    query={clientLocationQuery(selectedClient.billingAddress)}
                    bucket={bucket} onUnavailable={unavailable} onSessionExpired={onSessionExpired} /> : undefined}>
              <dl className="overview-facts">
                <div><dt>Email</dt><dd>{selectedClient.email || 'Not set'}</dd></div>
                <div><dt>City</dt><dd>{selectedClient.billingAddress.city || 'Not set'}</dd></div>
                <div><dt>Country</dt><dd>{selectedClient.billingAddress.country || 'Not set'}</dd></div>
                <div className="full-width overview-address"><dt>Billing address</dt><dd><GoogleMapsLocationLink query={clientLocationQuery(selectedClient.billingAddress)}>{[
                  selectedClient.billingAddress.line1, selectedClient.billingAddress.line2,
                  [selectedClient.billingAddress.city, selectedClient.billingAddress.stateOrCounty].filter(Boolean).join(', '),
                  selectedClient.billingAddress.postalCode, selectedClient.billingAddress.country,
                ].filter(Boolean).join('\n') || 'Not set'}</GoogleMapsLocationLink></dd></div>
              </dl>
              </RecordVisualOverview>
              <RecordActionDock title="Monthly invoice run">
                <div className="invoice-adjustment-form">
                  <label>
                    Month
                    <input
                      data-testid="monthly-invoice-month-input"
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
                    data-testid="generate-monthly-invoice-button"
                    onClick={onGenerateMonthlyInvoice}
                    type="button"
                    disabled={isInvoiceLoading || !isMonthlyInvoiceReady}
                  >
                    Generate monthly invoice
                  </button>
                </div>
                <span data-testid="monthly-invoice-helper">{monthlyInvoiceHelperText}</span>
              </RecordActionDock>

            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a client to see billing details.</strong>
              <p>Billing details and contact information will appear here.</p>
            </div>
          )}
        </div>

        <div ref={editorSlotRef} className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
          <form
            aria-hidden={!isEditorOpen}
            className="editor-panel panel"
            data-testid="client-form"
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
                  data-testid="client-name-input"
                  required
                  value={form.name}
                  onChange={(event) => onUpdateField('name', event.target.value)}
                  placeholder="Fox & Finch Events"
                />
              </label>

              <label>
                <span>Email</span>
                <input
                  data-testid="client-email-input"
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
                  data-testid="client-address-line1-input"
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
                  data-testid="client-city-input"
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
                  data-testid="client-postal-code-input"
                  value={form.billingAddress.postalCode ?? ''}
                  onChange={(event) => onUpdateAddressField('postalCode', event.target.value)}
                  placeholder="M3 5JZ"
                />
              </label>

              <label>
                <span>Country</span>
                <input
                  data-testid="client-country-input"
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
                data-testid="client-save-close-button"
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
