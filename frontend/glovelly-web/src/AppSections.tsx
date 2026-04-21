import type {
  AdminUser,
  AdminUserForm,
  AppMetadata,
  AuthUser,
  Client,
  ClientForm,
  ClientSettingsForm,
  Gig,
  GigExpenseForm,
  GigForm,
  GigStatus,
  Invoice,
  InvoiceStatus,
  UserSettingsForm,
} from './appShared'
import {
  formatBuildMetadata,
  formatCurrency,
  formatDate,
  formatDateTime,
  formatGigStatus,
  formatRate,
  getAllowedInvoiceStatusTransitions,
} from './appShared'

type SessionCheckingScreenProps = {
  appMetadata: AppMetadata
  status: string
}

export function SessionCheckingScreen({
  appMetadata,
  status,
}: SessionCheckingScreenProps) {
  return (
    <main className="app-shell auth-shell">
      <section className="hero-panel auth-panel">
        <div className="hero-copy">
          <p className="eyebrow">Security</p>
          <h1>Checking your sign-in.</h1>
          <p className="hero-text">Just a moment while we open your workspace.</p>
        </div>
        <span className="status-pill">{status}</span>
      </section>
      <p className="build-meta">{formatBuildMetadata(appMetadata.commitId, appMetadata.buildTimestamp)}</p>
    </main>
  )
}

type SignInScreenProps = {
  appMetadata: AppMetadata
  onSignIn: () => void
  shouldCloseBrowserNotice: boolean
  status: string
}

export function SignInScreen({
  appMetadata,
  onSignIn,
  shouldCloseBrowserNotice,
  status,
}: SignInScreenProps) {
  return (
    <main className="app-shell auth-shell">
      <section className="hero-panel auth-panel">
        <div className="hero-copy">
          <p className="eyebrow">Secure Sign-In</p>
          <h1>Sign in to open Glovelly.</h1>
          <p className="hero-text">
            Use your usual account to access clients, gigs and invoices.
          </p>
        </div>

        <div className="auth-actions">
          <span className="status-pill">{status}</span>
          {shouldCloseBrowserNotice && (
            <p className="auth-note">
              You have been signed out of Glovelly. Close your browser too if you are
              using a shared device.
            </p>
          )}
          <button className="primary-button" onClick={onSignIn} type="button">
            Continue with Google
          </button>
        </div>
      </section>
      <p className="build-meta">{formatBuildMetadata(appMetadata.commitId, appMetadata.buildTimestamp)}</p>
    </main>
  )
}

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
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void
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
              <button className="primary-button" type="submit" disabled={isLoading}>
                {mode === 'create' ? 'Save client' : 'Update client'}
              </button>
              <button className="ghost-button" onClick={onCloseEditor} type="button">
                Done
              </button>
            </div>
          </form>
        </div>
      </div>
    </section>
  )
}

type AdminSectionProps = {
  adminForm: AdminUserForm
  isEditorOpen: boolean
  adminMode: 'create' | 'edit'
  adminSearchQuery: string
  adminStatus: string
  adminUsers: AdminUser[]
  activeUsersCount: number
  filteredAdminUsers: AdminUser[]
  isAdminLoading: boolean
  onCloseEditor: () => void
  onResetForm: () => void
  onSearchQueryChange: (value: string) => void
  onSelectUser: (userId: string) => void
  onStartEditing: () => void
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof AdminUserForm, value: string | boolean) => void
  selectedAdminUser: AdminUser | null
  totalAdmins: number
}

export function AdminSection({
  adminForm,
  isEditorOpen,
  adminMode,
  adminSearchQuery,
  adminStatus,
  adminUsers,
  activeUsersCount,
  filteredAdminUsers,
  isAdminLoading,
  onCloseEditor,
  onResetForm,
  onSearchQueryChange,
  onSelectUser,
  onStartEditing,
  onSubmit,
  onUpdateField,
  selectedAdminUser,
  totalAdmins,
}: AdminSectionProps) {
  return (
    <section className="section-layout admin-zone">
      <div className="admin-banner panel">
        <div>
          <p className="section-label">Administrator Area</p>
          <h2>User access</h2>
          <p className="hero-text">
            Manage who can sign in, which role they have, and whether their account is active.
          </p>
        </div>

        <div className="hero-metrics admin-metrics">
          <article>
            <span>{adminUsers.length}</span>
            <p>users with access</p>
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
              <h2>People with access</h2>
            </div>
            <button className="ghost-button" onClick={onResetForm} type="button">
              Add user
            </button>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              type="search"
              placeholder="Name, email, role..."
              value={adminSearchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
            />
          </label>

          <div className="client-list">
            {filteredAdminUsers.map((user) => (
              <button
                key={user.id}
                className={`client-card ${selectedAdminUser?.id === user.id ? 'selected' : ''}`}
                onClick={() => onSelectUser(user.id)}
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
                <strong>No users match that search.</strong>
                <p>Try another term or add someone new.</p>
              </div>
            )}
          </div>
        </div>

        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Access Overview</p>
              <h2>
                {selectedAdminUser?.displayName ||
                  selectedAdminUser?.email ||
                  'No user selected'}
              </h2>
            </div>
            <div className="actions">
              <button
                className="ghost-button"
                onClick={onStartEditing}
                type="button"
                disabled={!selectedAdminUser}
              >
                Edit access
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
                <p className="detail-label">Sign-in status</p>
                <strong>
                  {selectedAdminUser.isEnrolled ? 'Ready to sign in' : 'Waiting for first sign-in'}
                </strong>
              </article>
              <article className="full-width">
                <p className="detail-label">Account reference</p>
                <strong>
                  {selectedAdminUser.googleSubject ?? 'Added by email only so far'}
                </strong>
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
              <strong>Select a user to review their access.</strong>
              <p>You can update their role, status and sign-in details here.</p>
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
                <p className="section-label">Management Pane</p>
                <h2>{adminMode === 'create' ? 'Add user' : 'Update access'}</h2>
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
                  onChange={(event) => onUpdateField('email', event.target.value)}
                  placeholder="performer@example.com"
                />
              </label>

              <label>
                <span>Display name</span>
                <input
                  value={adminForm.displayName}
                  onChange={(event) => onUpdateField('displayName', event.target.value)}
                  placeholder="Optional"
                />
              </label>

              <label className="full-width">
                <span>Account reference</span>
                <input
                  value={adminForm.googleSubject}
                  onChange={(event) => onUpdateField('googleSubject', event.target.value)}
                  placeholder="Optional"
                  disabled={adminMode === 'edit' && selectedAdminUser?.isEnrolled === true}
                />
              </label>

              <label>
                <span>Role</span>
                <select
                  value={adminForm.role}
                  onChange={(event) =>
                    onUpdateField('role', event.target.value as AdminUserForm['role'])
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
                  onChange={(event) => onUpdateField('isActive', event.target.checked)}
                />
                <span>Account is active and allowed to sign in</span>
              </label>
            </div>

            <div className="form-actions">
              <button className="primary-button" type="submit" disabled={isAdminLoading}>
                {adminMode === 'create' ? 'Add user' : 'Save changes'}
              </button>
              <button className="ghost-button" onClick={onCloseEditor} type="button">
                Done
              </button>
            </div>
            <p className="auth-note">
              Adding an email address is enough to let someone get started. You can fill in
              the account reference later if needed.
            </p>
          </form>
        </div>
      </div>
    </section>
  )
}

type GigsSectionProps = {
  clientNamesById: ReadonlyMap<string, string>
  clients: Client[]
  completedGigCount: number
  filteredGigs: Gig[]
  gigExpenseAmount: string
  gigExpenseDescription: string
  gigForm: GigForm
  isEditorOpen: boolean
  gigMode: 'create' | 'edit'
  gigSearchQuery: string
  gigStatus: string
  gigs: Gig[]
  isGigLoading: boolean
  isInvoiceLoading: boolean
  onAddGigExpense: () => void
  onCloseEditor: () => void
  onExpenseAmountChange: (value: string) => void
  onExpenseDescriptionChange: (value: string) => void
  onGenerateInvoice: () => void
  onOpenLinkedInvoice: () => void
  onRemoveGigExpense: (index: number) => void
  onResetForm: () => void
  onSearchQueryChange: (value: string) => void
  onSelectGig: (gigId: string) => void
  onToggleGigSelection: (gigId: string) => void
  onStartEditing: () => void
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void
  onUpdateGigExpenseField: (
    index: number,
    field: keyof Pick<GigExpenseForm, 'description' | 'amount'>,
    value: string
  ) => void
  onUpdateGigField: (
    field: keyof GigForm,
    value: string | boolean | GigExpenseForm[]
  ) => void
  plannedGigCount: number
  selectedGig: Gig | null
  selectedGigIds: string[]
  selectedGigs: Gig[]
}

export function GigsSection({
  clientNamesById,
  clients,
  completedGigCount,
  filteredGigs,
  gigExpenseAmount,
  gigExpenseDescription,
  gigForm,
  isEditorOpen,
  gigMode,
  gigSearchQuery,
  gigStatus,
  gigs,
  isGigLoading,
  isInvoiceLoading,
  onAddGigExpense,
  onCloseEditor,
  onExpenseAmountChange,
  onExpenseDescriptionChange,
  onGenerateInvoice,
  onOpenLinkedInvoice,
  onRemoveGigExpense,
  onResetForm,
  onSearchQueryChange,
  onSelectGig,
  onToggleGigSelection,
  onStartEditing,
  onSubmit,
  onUpdateGigExpenseField,
  onUpdateGigField,
  plannedGigCount,
  selectedGig,
  selectedGigIds,
  selectedGigs,
}: GigsSectionProps) {
  const selectedGigClientName =
    (selectedGig ? clientNamesById.get(selectedGig.clientId) : null) ?? 'Unknown client'
  const hasCrossClientSelection = new Set(selectedGigs.map((gig) => gig.clientId)).size > 1

  return (
    <section className="section-layout">
      <div className="gig-workspace">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Bookings</p>
              <h2>Gigs</h2>
            </div>
            <button className="ghost-button" onClick={onResetForm} type="button">
              New gig
            </button>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              type="search"
              placeholder="Client, title, venue..."
              value={gigSearchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
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
              <span>{completedGigCount}</span>
              <p>completed</p>
            </article>
          </div>

          <div className="client-list">
            {filteredGigs.map((gig) => {
              const clientName = clientNamesById.get(gig.clientId) ?? 'Unknown client'

              return (
                <button
                  key={gig.id}
                  className={`client-card ${selectedGig?.id === gig.id ? 'selected' : ''}`}
                  onClick={() => onSelectGig(gig.id)}
                  type="button"
                >
                  <label
                    className="gig-select-toggle"
                    onClick={(event) => event.stopPropagation()}
                  >
                    <input
                      type="checkbox"
                      checked={selectedGigIds.includes(gig.id)}
                      disabled={gig.isInvoiced}
                      onChange={() => onToggleGigSelection(gig.id)}
                    />
                    <span>{gig.isInvoiced ? 'Invoiced' : 'Select'}</span>
                  </label>
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
                className="primary-button"
                onClick={onGenerateInvoice}
                type="button"
                disabled={
                  isInvoiceLoading ||
                  (selectedGigIds.length === 0 &&
                    (!selectedGig || selectedGig.isInvoiced)) ||
                  hasCrossClientSelection
                }
              >
                {selectedGigIds.length > 0
                  ? `Generate invoice (${selectedGigIds.length})`
                  : selectedGig?.isInvoiced
                    ? 'Already invoiced'
                    : 'Generate invoice'}
              </button>
              <button
                className="ghost-button"
                onClick={onStartEditing}
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
                  <strong>{selectedGigClientName}</strong>
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
                  {selectedGig.isInvoiced ? (
                    <button className="ghost-button" onClick={onOpenLinkedInvoice} type="button">
                      Open invoice
                    </button>
                  ) : (
                    <strong>Not invoiced yet</strong>
                  )}
                </article>
                <article className="full-width">
                  <p className="detail-label">Notes</p>
                  <span>{selectedGig.notes?.trim() || 'No notes yet.'}</span>
                </article>
              </div>

              <div className="gig-timeline-note">
                <p className="detail-label">Invoice workflow</p>
                <span>
                  {selectedGig.isInvoiced
                    ? 'This gig is already linked to an invoice. Open Invoices to review or download it.'
                    : 'This gig has everything needed to create an invoice when you are ready.'}
                </span>
                {selectedGigIds.length > 0 && (
                  <span>
                    {hasCrossClientSelection
                      ? ' Selected gigs need to belong to the same client before they can be invoiced together.'
                      : ` ${selectedGigIds.length} gig(s) selected for a combined invoice.`}
                  </span>
                )}
              </div>
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a gig to review its details.</strong>
              <p>Key booking and billing details will appear here.</p>
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
                  onChange={(event) => onUpdateGigField('clientId', event.target.value)}
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
                  onChange={(event) => onUpdateGigField('date', event.target.value)}
                />
              </label>

              <label className="full-width">
                <span>Title / description</span>
                <input
                  required
                  value={gigForm.title}
                  onChange={(event) => onUpdateGigField('title', event.target.value)}
                  placeholder="Spring product launch"
                />
              </label>

              <label className="full-width">
                <span>Location / venue</span>
                <input
                  required
                  value={gigForm.venue}
                  onChange={(event) => onUpdateGigField('venue', event.target.value)}
                  placeholder="Albert Hall, Manchester"
                />
              </label>

              <label>
                <span>Fee</span>
                <input
                  required
                  inputMode="decimal"
                  value={gigForm.fee}
                  onChange={(event) => onUpdateGigField('fee', event.target.value)}
                  placeholder="650"
                />
              </label>

              <label>
                <span>Status</span>
                <select
                  value={gigForm.status}
                  onChange={(event) =>
                    onUpdateGigField('status', event.target.value as GigStatus)
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
                  onChange={(event) => onUpdateGigField('wasDriving', event.target.checked)}
                />
                <span>I was driving for this gig</span>
              </label>

              <label className="full-width">
                <span>Notes</span>
                <textarea
                  rows={5}
                  value={gigForm.notes}
                  onChange={(event) => onUpdateGigField('notes', event.target.value)}
                  placeholder="Optional commercial or logistics notes"
                />
              </label>
            </div>

            <div className="gig-timeline-note">
              <p className="detail-label">Expenses</p>
              <span>
                Add chargeable out-of-pocket costs here and they will flow through when the gig is invoiced.
              </span>
            </div>

            <div className="invoice-adjustment-form">
              <label>
                Amount
                <input
                  inputMode="decimal"
                  value={gigExpenseAmount}
                  onChange={(event) => onExpenseAmountChange(event.target.value)}
                  placeholder="45.00"
                  disabled={isGigLoading}
                />
              </label>
              <label>
                Description
                <input
                  value={gigExpenseDescription}
                  onChange={(event) => onExpenseDescriptionChange(event.target.value)}
                  placeholder="Parking, hotel, equipment hire..."
                  disabled={isGigLoading}
                />
              </label>
              <button
                className="ghost-button"
                onClick={onAddGigExpense}
                type="button"
                disabled={isGigLoading}
              >
                Add expense
              </button>
            </div>

            {gigForm.expenses.length > 0 ? (
              <div className="gig-expense-list">
                {gigForm.expenses.map((expense, index) => (
                  <div className="gig-expense-item" key={`${expense.id || 'new'}-${index}`}>
                    <label>
                      <span>Description</span>
                      <input
                        value={expense.description}
                        onChange={(event) =>
                          onUpdateGigExpenseField(index, 'description', event.target.value)
                        }
                        disabled={isGigLoading}
                      />
                    </label>
                    <label>
                      <span>Amount</span>
                      <input
                        inputMode="decimal"
                        value={expense.amount}
                        onChange={(event) =>
                          onUpdateGigExpenseField(index, 'amount', event.target.value)
                        }
                        disabled={isGigLoading}
                      />
                    </label>
                    <button
                      className="ghost-button"
                      onClick={() => onRemoveGigExpense(index)}
                      type="button"
                      disabled={isGigLoading}
                    >
                      Remove
                    </button>
                  </div>
                ))}
              </div>
            ) : (
              <div className="empty-state">
                <strong>No expenses added yet.</strong>
                <p>Use the fields above to add chargeable costs to this gig.</p>
              </div>
            )}

            <div className="form-actions">
              <button
                className="primary-button"
                type="submit"
                disabled={isGigLoading || clients.length === 0}
              >
                {gigMode === 'create' ? 'Save gig' : 'Update gig'}
              </button>
              <button className="ghost-button" onClick={onCloseEditor} type="button">
                Done
              </button>
            </div>

            {clients.length === 0 && (
              <p className="auth-note">
                Add a client first so this gig can be linked to the right account.
              </p>
            )}
          </form>
        </div>
      </div>
    </section>
  )
}

type InvoicesSectionProps = {
  adjustmentAmount: string
  adjustmentReason: string
  clientNamesById: ReadonlyMap<string, string>
  draftInvoiceCount: number
  filteredInvoices: Invoice[]
  isEditorOpen: boolean
  invoiceSearchQuery: string
  invoiceStatus: string
  invoices: Invoice[]
  issuedInvoiceCount: number
  isInvoiceLoading: boolean
  onAdjustmentAmountChange: (value: string) => void
  onAdjustmentReasonChange: (value: string) => void
  onAddAdjustment: (invoice: Invoice) => Promise<void>
  onCloseEditor: () => void
  onDeleteInvoice: (invoice: Invoice) => Promise<void>
  onDownloadPdf: (invoice: Invoice) => Promise<void>
  onInvoiceStatusChange: (invoice: Invoice, status: InvoiceStatus) => Promise<void>
  onReissue: (invoice: Invoice) => Promise<void>
  onSearchQueryChange: (value: string) => void
  onSelectInvoice: (invoiceId: string) => void
  onStartEditing: () => void
  selectedInvoice: Invoice | null
}

export function InvoicesSection({
  adjustmentAmount,
  adjustmentReason,
  clientNamesById,
  draftInvoiceCount,
  filteredInvoices,
  isEditorOpen,
  invoiceSearchQuery,
  invoiceStatus,
  invoices,
  issuedInvoiceCount,
  isInvoiceLoading,
  onAdjustmentAmountChange,
  onAdjustmentReasonChange,
  onAddAdjustment,
  onCloseEditor,
  onDeleteInvoice,
  onDownloadPdf,
  onInvoiceStatusChange,
  onReissue,
  onSearchQueryChange,
  onSelectInvoice,
  onStartEditing,
  selectedInvoice,
}: InvoicesSectionProps) {
  const selectedInvoiceClientName =
    (selectedInvoice ? clientNamesById.get(selectedInvoice.clientId) : null) ??
    'Unknown client'

  return (
    <section className="section-layout">
      <div className="gig-workspace">
        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Billing</p>
              <h2>Invoices</h2>
            </div>
            <span className="status-pill">{invoiceStatus}</span>
          </div>

          <label className="search-field">
            <span>Search</span>
            <input
              type="search"
              placeholder="Invoice number, client or description..."
              value={invoiceSearchQuery}
              onChange={(event) => onSearchQueryChange(event.target.value)}
            />
          </label>

          <div className="gig-summary-grid">
            <article>
              <span>{invoices.length}</span>
              <p>saved invoices</p>
            </article>
            <article>
              <span>{draftInvoiceCount}</span>
              <p>draft</p>
            </article>
            <article>
              <span>{issuedInvoiceCount}</span>
              <p>issued</p>
            </article>
          </div>

          <div className="client-list">
            {filteredInvoices.map((invoice) => {
              const clientName = clientNamesById.get(invoice.clientId) ?? 'Unknown client'

              return (
                <button
                  key={invoice.id}
                  className={`client-card ${selectedInvoice?.id === invoice.id ? 'selected' : ''}`}
                  onClick={() => onSelectInvoice(invoice.id)}
                  type="button"
                >
                  <div>
                    <strong>{invoice.invoiceNumber}</strong>
                    <span>{clientName}</span>
                  </div>
                  <small className="gig-card-meta">
                    {formatDate(invoice.invoiceDate)} · {invoice.status}
                  </small>
                  <small className="gig-card-meta">{formatCurrency(invoice.total)}</small>
                </button>
              )
            })}

            {filteredInvoices.length === 0 && (
              <div className="empty-state">
                <strong>No invoices yet.</strong>
                <p>Generate one from a gig and it will appear here immediately.</p>
              </div>
            )}
          </div>
        </div>

        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Invoice Overview</p>
              <h2>{selectedInvoice?.invoiceNumber ?? 'No invoice selected'}</h2>
            </div>
            <div className="actions">
              <button
                className="ghost-button"
                onClick={onStartEditing}
                type="button"
                disabled={!selectedInvoice}
              >
                Line items
              </button>
              <button
                className="ghost-button"
                onClick={() => selectedInvoice && void onReissue(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading}
              >
                Re-issue
              </button>
              <button
                className="danger-button"
                onClick={() => selectedInvoice && void onDeleteInvoice(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading || selectedInvoice?.status !== 'Draft'}
                title={
                  selectedInvoice?.status !== 'Draft'
                    ? 'Only Draft invoices can be deleted.'
                    : 'Delete draft invoice'
                }
              >
                Delete draft
              </button>
              <button
                className="primary-button"
                onClick={() => selectedInvoice && void onDownloadPdf(selectedInvoice)}
                type="button"
                disabled={!selectedInvoice || isInvoiceLoading}
              >
                Download PDF
              </button>
            </div>
          </div>

          {selectedInvoice ? (
            <>
              <div className="detail-grid">
                <article>
                  <p className="detail-label">Client</p>
                  <strong>{selectedInvoiceClientName}</strong>
                </article>
                <article>
                  <p className="detail-label">Status</p>
                  <div className="field-with-inline-help">
                    <select
                      value={selectedInvoice.status}
                      onChange={(event) =>
                        void onInvoiceStatusChange(
                          selectedInvoice,
                          event.target.value as InvoiceStatus
                        )
                      }
                      disabled={isInvoiceLoading}
                    >
                      <option value={selectedInvoice.status}>{selectedInvoice.status}</option>
                      {getAllowedInvoiceStatusTransitions(selectedInvoice.status).map(
                        (statusOption) => (
                          <option key={statusOption} value={statusOption}>
                            {statusOption}
                          </option>
                        )
                      )}
                    </select>
                  </div>
                </article>
                <article>
                  <p className="detail-label">Invoice date</p>
                  <strong>{formatDate(selectedInvoice.invoiceDate)}</strong>
                </article>
                <article>
                  <p className="detail-label">Due date</p>
                  <strong>{formatDate(selectedInvoice.dueDate)}</strong>
                </article>
                <article className="full-width">
                  <p className="detail-label">In respect of</p>
                  <span>{selectedInvoice.description?.trim() || 'No description set.'}</span>
                </article>
                <article>
                  <p className="detail-label">Total</p>
                  <strong>{formatCurrency(selectedInvoice.total)}</strong>
                </article>
                <article>
                  <p className="detail-label">Line items</p>
                  <strong>{selectedInvoice.lines.length}</strong>
                </article>
                <article>
                  <p className="detail-label">Re-issued</p>
                  <strong>{selectedInvoice.reissueCount} times</strong>
                </article>
                <article>
                  <p className="detail-label">Last re-issue</p>
                  <strong>{formatDateTime(selectedInvoice.lastReissuedUtc)}</strong>
                </article>
              </div>

              <div className="gig-timeline-note">
                <p className="detail-label">Invoice workflow</p>
                <span>
                  Review invoice details here, then open line items when you need to make changes.
                </span>
              </div>
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select an invoice to review its details.</strong>
              <p>The generated PDF and line items will be available here.</p>
            </div>
          )}
        </div>

        <div className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
          <div aria-hidden={!isEditorOpen} className="panel editor-panel">
            <div className="panel-heading">
              <div>
                <p className="section-label">Management Pane</p>
                <h2>{selectedInvoice ? 'Line items' : 'No invoice selected'}</h2>
              </div>
              <div className="actions">
                <button className="ghost-button" onClick={onCloseEditor} type="button">
                  Done
                </button>
              </div>
            </div>

            {selectedInvoice ? (
              <>
                <div className="gig-timeline-note">
                  <p className="detail-label">Adjustments</p>
                  <span>
                    Add adjustments as separate line items so the invoice stays clear and easy to follow.
                  </span>
                </div>

                <form
                  className="invoice-adjustment-form"
                  onSubmit={(event) => {
                    event.preventDefault()
                    void onAddAdjustment(selectedInvoice)
                  }}
                >
                  <label>
                    Amount
                    <input
                      type="number"
                      step="0.01"
                      value={adjustmentAmount}
                      onChange={(event) => onAdjustmentAmountChange(event.target.value)}
                      placeholder="-25.00"
                      disabled={isInvoiceLoading}
                    />
                  </label>
                  <label>
                    Reason
                    <input
                      value={adjustmentReason}
                      onChange={(event) => onAdjustmentReasonChange(event.target.value)}
                      placeholder="Goodwill discount, surcharge, correction..."
                      disabled={isInvoiceLoading}
                    />
                  </label>
                  <button className="ghost-button" type="submit" disabled={isInvoiceLoading}>
                    Add adjustment
                  </button>
                </form>

                <div className="invoice-line-list">
                  {selectedInvoice.lines
                    .slice()
                    .sort((left, right) => left.sortOrder - right.sortOrder)
                    .map((line) => (
                      <div className="invoice-line-item" key={line.id}>
                        <div>
                          <strong>{line.description}</strong>
                          <span>
                            {line.type} · {line.quantity} x {formatCurrency(line.unitPrice)}
                          </span>
                          {line.type === 'ManualAdjustment' ? (
                            <span>Audit: {formatDateTime(line.createdUtc)}</span>
                          ) : null}
                        </div>
                        <strong>{formatCurrency(line.lineTotal)}</strong>
                      </div>
                    ))}
                </div>
              </>
            ) : (
              <div className="empty-state roomy">
                <strong>Select an invoice to inspect its line items.</strong>
                <p>Line items and adjustments will appear here.</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  )
}

type UserSettingsModalProps = {
  form: UserSettingsForm
  isOpen: boolean
  isSaving: boolean
  onClose: () => void
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof UserSettingsForm, value: string) => void
  status: string
}

export function UserSettingsModal({
  form,
  isOpen,
  isSaving,
  onClose,
  onSubmit,
  onUpdateField,
  status,
}: UserSettingsModalProps) {
  if (!isOpen) {
    return null
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
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
          <button className="ghost-button" onClick={onClose} type="button">
            Close
          </button>
        </div>

        <p className="hero-text settings-intro">
          These rates are used as your personal fallback when a client does not
          have custom mileage pricing configured.
        </p>

        <form className="settings-form" onSubmit={onSubmit}>
          <div className="form-grid">
            <label>
              <span>Mileage rate</span>
              <input
                inputMode="decimal"
                placeholder="0.45"
                type="text"
                value={form.mileageRate}
                onChange={(event) => onUpdateField('mileageRate', event.target.value)}
              />
            </label>

            <label>
              <span>Passenger mileage rate</span>
              <input
                inputMode="decimal"
                placeholder="0.10"
                type="text"
                value={form.passengerMileageRate}
                onChange={(event) =>
                  onUpdateField('passengerMileageRate', event.target.value)
                }
              />
            </label>
          </div>

          <div className="settings-note">
            Leave a field blank if you do not want a default for that rate.
          </div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save settings'}
            </button>
            <span className="status-pill">{status}</span>
          </div>
        </form>
      </section>
    </div>
  )
}

type ClientSettingsModalProps = {
  authUser: AuthUser | null
  form: ClientSettingsForm
  isOpen: boolean
  isSaving: boolean
  onClose: () => void
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof ClientSettingsForm, value: string) => void
  selectedClient: Client | null
  status: string
}

export function ClientSettingsModal({
  authUser,
  form,
  isOpen,
  isSaving,
  onClose,
  onSubmit,
  onUpdateField,
  selectedClient,
  status,
}: ClientSettingsModalProps) {
  if (!isOpen || !selectedClient) {
    return null
  }

  return (
    <div className="settings-overlay" onClick={onClose} role="presentation">
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
          <button className="ghost-button" onClick={onClose} type="button">
            Close
          </button>
        </div>

        <p className="hero-text settings-intro">
          Leave a field blank to inherit the default from your own user settings.
          Add a value here only when this client needs a special rate.
        </p>

        <div className="detail-grid client-settings-preview">
          <article
            className={
              selectedClient.mileageRate === null
                ? 'setting-card inherited'
                : 'setting-card override'
            }
          >
            <p className="detail-label">Current mileage rule</p>
            <strong>
              {formatRate(selectedClient.mileageRate ?? authUser?.mileageRate ?? null)}
            </strong>
            <span>
              {selectedClient.mileageRate === null
                ? 'Inherited from your user settings'
                : 'Overriding your default'}
            </span>
          </article>
          <article
            className={
              selectedClient.passengerMileageRate === null
                ? 'setting-card inherited'
                : 'setting-card override'
            }
          >
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

        <form className="settings-form" onSubmit={onSubmit}>
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
                value={form.mileageRate}
                onChange={(event) => onUpdateField('mileageRate', event.target.value)}
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
                value={form.passengerMileageRate}
                onChange={(event) =>
                  onUpdateField('passengerMileageRate', event.target.value)
                }
              />
            </label>
          </div>

          <div className="settings-note">
            Blank means inherited. A filled value becomes a client-specific override.
          </div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save client settings'}
            </button>
            <span className="status-pill">{status}</span>
          </div>
        </form>
      </section>
    </div>
  )
}
