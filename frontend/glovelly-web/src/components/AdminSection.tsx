import type { FormEvent } from 'react'
import { formatDateTime } from '../formatters'
import type { AdminUser, AdminUserForm } from '../types'

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
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
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
