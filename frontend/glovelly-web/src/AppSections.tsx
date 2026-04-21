import type {
  AdminUser,
  AdminUserForm,
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
  formatCurrency,
  formatDate,
  formatDateTime,
  formatGigStatus,
  formatRate,
  getAllowedInvoiceStatusTransitions,
} from './appShared'

type SessionCheckingScreenProps = {
  status: string
}

export function SessionCheckingScreen({
  status,
}: SessionCheckingScreenProps) {
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

type SignInScreenProps = {
  onSignIn: () => void
  shouldCloseBrowserNotice: boolean
  status: string
}

export function SignInScreen({
  onSignIn,
  shouldCloseBrowserNotice,
  status,
}: SignInScreenProps) {
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
          <button className="primary-button" onClick={onSignIn} type="button">
            Continue with Google
          </button>
        </div>
      </section>
    </main>
  )
}

type ClientsSectionProps = {
  clients: Client[]
  filteredClients: Client[]
  form: ClientForm
  isApiConnected: boolean
  isInvoiceLoading: boolean
  isLoading: boolean
  monthlyInvoiceClientId: string
  monthlyInvoiceMonth: string
  mode: 'create' | 'edit'
  onDelete: () => void
  onGenerateMonthlyInvoice: () => void
  onMonthlyInvoiceClientChange: (value: string) => void
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
  clients,
  filteredClients,
  form,
  isApiConnected,
  isInvoiceLoading,
  isLoading,
  monthlyInvoiceClientId,
  monthlyInvoiceMonth,
  mode,
  onDelete,
  onGenerateMonthlyInvoice,
  onMonthlyInvoiceClientChange,
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
                    Client
                    <select
                      value={monthlyInvoiceClientId}
                      onChange={(event) =>
                        onMonthlyInvoiceClientChange(event.target.value)
                      }
                      disabled={isInvoiceLoading}
                    >
                      <option value="">Select client</option>
                      {clients.map((client) => (
                        <option key={client.id} value={client.id}>
                          {client.name}
                        </option>
                      ))}
                    </select>
                  </label>
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
                    disabled={isInvoiceLoading}
                  >
                    Generate monthly invoice
                  </button>
                </div>
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
              <p>The right-hand panel is set up for a fuller CRM-style summary later on.</p>
            </div>
          )}
        </div>

        <form className="editor-panel panel" onSubmit={onSubmit}>
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
                value={form.billingAddress.line2}
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
                value={form.billingAddress.stateOrCounty}
                onChange={(event) =>
                  onUpdateAddressField('stateOrCounty', event.target.value)
                }
                placeholder="Greater Manchester"
              />
            </label>

            <label>
              <span>Postal code</span>
              <input
                value={form.billingAddress.postalCode}
                onChange={(event) => onUpdateAddressField('postalCode', event.target.value)}
                placeholder="M3 5JZ"
              />
            </label>

            <label>
              <span>Country</span>
              <input
                value={form.billingAddress.country}
                onChange={(event) => onUpdateAddressField('country', event.target.value)}
                placeholder="United Kingdom"
              />
            </label>
          </div>

          <div className="form-actions">
            <button className="primary-button" type="submit" disabled={isLoading}>
              {mode === 'create' ? 'Save client' : 'Update client'}
            </button>
            <button className="ghost-button" onClick={onResetForm} type="button">
              Reset form
            </button>
          </div>
        </form>
      </div>
    </section>
  )
}

type AdminSectionProps = {
  adminForm: AdminUserForm
  adminMode: 'create' | 'edit'
  adminSearchQuery: string
  adminStatus: string
  adminUsers: AdminUser[]
  activeUsersCount: number
  filteredAdminUsers: AdminUser[]
  isAdminLoading: boolean
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
  adminMode,
  adminSearchQuery,
  adminStatus,
  adminUsers,
  activeUsersCount,
  filteredAdminUsers,
  isAdminLoading,
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
            <button className="ghost-button" onClick={onResetForm} type="button">
              New enrolment
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
                <strong>
                  {selectedAdminUser.isEnrolled
                    ? 'Bound to Google subject'
                    : 'Invited by email'}
                </strong>
              </article>
              <article className="full-width">
                <p className="detail-label">Google subject</p>
                <strong>
                  {selectedAdminUser.googleSubject ?? 'Pending first Google sign-in'}
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
              <strong>Select an enrolled user to review their access.</strong>
              <p>The admin area stays hidden from standard users and only appears for admins.</p>
            </div>
          )}
        </div>

        <form className="panel" onSubmit={onSubmit}>
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
              <span>Google subject</span>
              <input
                value={adminForm.googleSubject}
                onChange={(event) => onUpdateField('googleSubject', event.target.value)}
                placeholder="Optional until first Google sign-in"
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
              {adminMode === 'create' ? 'Enrol user' : 'Save enrolment'}
            </button>
            <button className="ghost-button" onClick={onResetForm} type="button">
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
}

type GigsSectionProps = {
  clients: Client[]
  filteredGigs: Gig[]
  gigExpenseAmount: string
  gigExpenseDescription: string
  gigForm: GigForm
  gigMode: 'create' | 'edit'
  gigSearchQuery: string
  gigStatus: string
  gigs: Gig[]
  isGigLoading: boolean
  isInvoiceLoading: boolean
  onAddGigExpense: () => void
  onExpenseAmountChange: (value: string) => void
  onExpenseDescriptionChange: (value: string) => void
  onGenerateInvoice: () => void
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
  clients,
  filteredGigs,
  gigExpenseAmount,
  gigExpenseDescription,
  gigForm,
  gigMode,
  gigSearchQuery,
  gigStatus,
  gigs,
  isGigLoading,
  isInvoiceLoading,
  onAddGigExpense,
  onExpenseAmountChange,
  onExpenseDescriptionChange,
  onGenerateInvoice,
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
  const selectedGigClient =
    clients.find((client) => client.id === selectedGig?.clientId) ?? null
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
                  onClick={() => onSelectGig(gig.id)}
                  type="button"
                >
                  <label onClick={(event) => event.stopPropagation()}>
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
                <p className="detail-label">Invoice workflow</p>
                <span>
                  {selectedGig.isInvoiced
                    ? 'This gig is already linked to an invoice. Open the invoices workspace to review or download it.'
                    : 'This record now carries the client, date, venue and fee needed to generate a one-off invoice.'}
                </span>
                {selectedGigIds.length > 0 && (
                  <span>
                    {hasCrossClientSelection
                      ? 'Selected gigs must share the same client before invoice generation is allowed.'
                      : `${selectedGigIds.length} gig(s) selected for a combined invoice.`}
                  </span>
                )}
              </div>
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a gig to review its details.</strong>
              <p>The browse pane shows the commercial snapshot that matters later.</p>
            </div>
          )}
        </div>

        <form className="panel" onSubmit={onSubmit}>
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
            <button className="ghost-button" onClick={onResetForm} type="button">
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

type InvoicesSectionProps = {
  adjustmentAmount: string
  adjustmentReason: string
  clients: Client[]
  draftInvoiceCount: number
  filteredInvoices: Invoice[]
  invoiceSearchQuery: string
  invoiceStatus: string
  invoices: Invoice[]
  isInvoiceLoading: boolean
  onAdjustmentAmountChange: (value: string) => void
  onAdjustmentReasonChange: (value: string) => void
  onAddAdjustment: (invoice: Invoice) => Promise<void>
  onDeleteInvoice: (invoice: Invoice) => Promise<void>
  onDownloadPdf: (invoice: Invoice) => Promise<void>
  onInvoiceStatusChange: (invoice: Invoice, status: InvoiceStatus) => Promise<void>
  onReissue: (invoice: Invoice) => Promise<void>
  onSearchQueryChange: (value: string) => void
  onSelectInvoice: (invoiceId: string) => void
  selectedInvoice: Invoice | null
}

export function InvoicesSection({
  adjustmentAmount,
  adjustmentReason,
  clients,
  draftInvoiceCount,
  filteredInvoices,
  invoiceSearchQuery,
  invoiceStatus,
  invoices,
  isInvoiceLoading,
  onAdjustmentAmountChange,
  onAdjustmentReasonChange,
  onAddAdjustment,
  onDeleteInvoice,
  onDownloadPdf,
  onInvoiceStatusChange,
  onReissue,
  onSearchQueryChange,
  onSelectInvoice,
  selectedInvoice,
}: InvoicesSectionProps) {
  const selectedInvoiceClient =
    clients.find((client) => client.id === selectedInvoice?.clientId) ?? null

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
              <span>{invoices.filter((invoice) => invoice.status === 'Issued').length}</span>
              <p>issued</p>
            </article>
          </div>

          <div className="client-list">
            {filteredInvoices.map((invoice) => {
              const clientName =
                clients.find((client) => client.id === invoice.clientId)?.name ??
                'Unknown client'

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
                  <strong>{selectedInvoiceClient?.name ?? 'Unknown client'}</strong>
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
                  Re-issue and PDF actions stay in the overview pane, while line-level changes now live in
                  the docked side pane for a steadier browse flow.
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

        <div className="panel">
          <div className="panel-heading">
            <div>
              <p className="section-label">Management Pane</p>
              <h2>{selectedInvoice ? 'Line items' : 'No invoice selected'}</h2>
            </div>
          </div>

          {selectedInvoice ? (
            <>
              <div className="gig-timeline-note">
                <p className="detail-label">Adjustments</p>
                <span>
                  Manual adjustments append a separate line item so the original invoice breakdown stays intact.
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
              <p>The right-hand pane is reserved for line breakdowns and manual adjustments.</p>
            </div>
          )}
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
