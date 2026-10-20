import { useEffect, useRef } from 'react'
import type { FormEvent } from 'react'
import { formatDateTime } from '../formatters'
import type { AdminSort, AdminSortKey, AdminUser, AdminUserForm } from '../types'
import { TrashIcon } from './TrashIcon'
import { RecordActionDock } from './RecordActionDock'
import { adminAccessRestriction } from '../adminAccess'

type AdminSectionProps = {
  currentUserId?: string
  onChangeAccess: (role: AdminUserForm['role'], isActive: boolean) => Promise<void>
  adminForm: AdminUserForm
  isEditorOpen: boolean
  adminMode: 'create' | 'edit'
  adminSearchQuery: string
  adminSort: AdminSort
  adminStatus: string
  filteredAdminUsers: AdminUser[]
  isAdminLoading: boolean
  onCloseEditor: () => void
  onDeleteUser: () => void
  onResetForm: () => void
  onSearchQueryChange: (value: string) => void
  onSelectUser: (userId: string) => void
  onSortChange: (sort: AdminSort) => void
  onStartEditing: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof AdminUserForm, value: string | boolean) => void
  selectedAdminUser: AdminUser | null
}

export function AdminSection({
  currentUserId,
  onChangeAccess,
  adminForm,
  isEditorOpen,
  adminMode,
  adminSearchQuery,
  adminSort,
  adminStatus,
  filteredAdminUsers,
  isAdminLoading,
  onCloseEditor,
  onDeleteUser,
  onResetForm,
  onSearchQueryChange,
  onSelectUser,
  onSortChange,
  onStartEditing,
  onSubmit,
  onUpdateField,
  selectedAdminUser,
}: AdminSectionProps) {
  const editorSlotRef = useRef<HTMLDivElement | null>(null)
  const selfRestriction = selectedAdminUser ? adminAccessRestriction(selectedAdminUser, currentUserId) : undefined
  const accessRestriction = selfRestriction ?? (isEditorOpen ? 'Save or discard editor changes before changing access here.' : undefined)
  const adminSortOptions: { value: AdminSortKey; label: string }[] = [
    { value: 'displayName', label: 'User' },
    { value: 'email', label: 'Email' },
    { value: 'role', label: 'Role' },
    { value: 'access', label: 'Access' },
    { value: 'enrolment', label: 'Sign-in' },
    { value: 'lastLogin', label: 'Last login' },
  ]

  useEffect(() => {
    if (!isEditorOpen || !window.matchMedia('(max-width: 1180px)').matches) {
      return
    }

    window.setTimeout(() => {
      editorSlotRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }, 80)
  }, [isEditorOpen])

  return (
    <section className="section-layout admin-zone">
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

          <div className="compact-list-controls" aria-label="Admin user list controls">
            <div className="compact-list-main-controls">
              <label className="search-field compact-search-field">
                <span>Search</span>
                <input
                  type="search"
                  placeholder="Name, email, role..."
                  value={adminSearchQuery}
                  onChange={(event) => onSearchQueryChange(event.target.value)}
                />
              </label>
              <label>
                <span>Sort by</span>
                <select
                  value={adminSort.key}
                  onChange={(event) =>
                    onSortChange({ ...adminSort, key: event.target.value as AdminSortKey })
                  }
                >
                  {adminSortOptions.map((option) => (
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
                  adminSort.direction === 'asc'
                    ? 'Sort ascending. Click to sort descending.'
                    : 'Sort descending. Click to sort ascending.'
                }
                title={adminSort.direction === 'asc' ? 'Ascending' : 'Descending'}
                onClick={() =>
                  onSortChange({
                    ...adminSort,
                    direction: adminSort.direction === 'asc' ? 'desc' : 'asc',
                  })
                }
              >
                {adminSort.direction === 'asc' ? '↑' : '↓'}
              </button>
            </div>
          </div>

          <div className="compact-record-list admin-record-list" aria-label="People with access">
            <div className="compact-record-header admin-record-row">
              <span>User</span>
              <span>Email</span>
              <span>Role</span>
              <span>Access</span>
              <span>Sign-in</span>
              <span>Last login</span>
            </div>
            {filteredAdminUsers.map((user) => (
              <button
                key={user.id}
                className={`compact-record-row admin-record-row ${selectedAdminUser?.id === user.id ? 'selected' : ''}`}
                onClick={() => onSelectUser(user.id)}
                type="button"
              >
                <div className="compact-primary-cell">
                  <strong>{user.displayName || user.email}</strong>
                  <span>{user.email}</span>
                </div>
                <span>{user.email}</span>
                <span className="compact-status-cell">{user.role}</span>
                <span>{user.isActive ? 'Active' : 'Inactive'}</span>
                <span>{user.isEnrolled ? 'Bound' : 'Invited'}</span>
                <span>{formatDateTime(user.lastLoginUtc)}</span>
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

        <div className="panel record-overview">
          <div className="panel-heading">
            <div>
              <p className="section-label">Access Overview</p>
              <h2 tabIndex={-1}>
                {selectedAdminUser?.displayName ||
                  selectedAdminUser?.email ||
                  'No user selected'}
              </h2>
            </div>
            <div className="actions">
              <button
                className={`ghost-button editor-toggle ${isEditorOpen ? 'active' : ''}`}
                onClick={isEditorOpen ? onCloseEditor : onStartEditing}
                type="button"
                disabled={!selectedAdminUser}
                aria-expanded={isEditorOpen}
              >
                Edit access
              </button>
              <button
                className="danger-button"
                onClick={onDeleteUser}
                type="button"
                disabled={
                  isAdminLoading ||
                  !selectedAdminUser ||
                  selectedAdminUser.isActive
                }
                title={
                  selectedAdminUser?.isActive
                    ? 'Only inactive users can be deleted.'
                    : 'Delete inactive user'
                }
              >
                <TrashIcon />
                Delete user
              </button>
            </div>
          </div>

          {selectedAdminUser ? (
            <>
            <div className="overview-body" key={selectedAdminUser.id}>
            <dl className="overview-facts">
              <div className="full-width"><dt>Email</dt><dd>{selectedAdminUser.email}</dd></div>
              <div><dt>Role</dt><dd>{selectedAdminUser.role}</dd></div>
              <div><dt>Access</dt><dd>{selectedAdminUser.isActive ? 'Active' : 'Inactive'}</dd></div>
              <div><dt>Sign-in</dt><dd>{selectedAdminUser.isEnrolled ? 'Ready to sign in' : 'Waiting for first sign-in'}</dd></div>
              <div><dt>Last login</dt><dd>{formatDateTime(selectedAdminUser.lastLoginUtc)}</dd></div>
              <div><dt>Created</dt><dd>{formatDateTime(selectedAdminUser.createdUtc)}</dd></div>
              <div className="full-width"><dt>Account reference</dt><dd>{selectedAdminUser.googleSubject ?? 'Added by email only so far'}</dd></div>
            </dl>
            </div>
            <RecordActionDock title="Access and role">
              <div className="actions access-control-group" tabIndex={accessRestriction ? 0 : undefined}
                aria-label={accessRestriction ? 'Access controls unavailable' : undefined}
                aria-describedby={accessRestriction ? 'admin-access-restriction' : undefined} title={accessRestriction}>
                {accessRestriction && <span className="access-control-tooltip" role="tooltip">{accessRestriction}</span>}
                <label>
                  <select aria-label="User access role" value={selectedAdminUser.role}
                    disabled={isAdminLoading || !!accessRestriction} title={accessRestriction}
                    aria-describedby={accessRestriction ? 'admin-access-restriction' : undefined}
                    onChange={event => void onChangeAccess(event.target.value as AdminUserForm['role'], selectedAdminUser.isActive)}>
                    <option value="User">User</option>
                    <option value="Admin">Admin</option>
                  </select>
                </label>
                <button type="button" className={selectedAdminUser.isActive ? 'danger-button' : 'primary-button'}
                  disabled={isAdminLoading || !!accessRestriction} title={accessRestriction}
                  aria-describedby={accessRestriction ? 'admin-access-restriction' : undefined}
                  onClick={() => void onChangeAccess(selectedAdminUser.role === 'Admin' ? 'Admin' : 'User', !selectedAdminUser.isActive)}>
                  {selectedAdminUser.isActive ? 'Deactivate access' : 'Enable access'}
                </button>
              </div>
              {accessRestriction && <details className="access-restriction-help">
                <summary title={accessRestriction}>Why are these controls unavailable?</summary>
                <p id="admin-access-restriction">{accessRestriction}</p>
              </details>}
            </RecordActionDock>
            </>
          ) : (
            <div className="empty-state roomy">
              <strong>Select a user to review their access.</strong>
              <p>You can update their role, status and sign-in details here.</p>
            </div>
          )}
        </div>

        <div ref={editorSlotRef} className={`editor-slot ${isEditorOpen ? 'open' : ''}`}>
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
                  disabled={adminMode === 'edit' && !!selfRestriction}
                  title={adminMode === 'edit' ? selfRestriction : undefined}
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
                  disabled={adminMode === 'edit' && !!selfRestriction}
                  title={adminMode === 'edit' ? selfRestriction : undefined}
                  onChange={(event) => onUpdateField('isActive', event.target.checked)}
                />
                <span>Account is active and allowed to sign in</span>
              </label>

              {adminMode === 'create' ? (
                <label className="checkbox-field full-width">
                  <input
                    type="checkbox"
                    checked={adminForm.sendInvitationEmail}
                    onChange={(event) =>
                      onUpdateField('sendInvitationEmail', event.target.checked)
                    }
                  />
                  <span>Email this user an invitation to sign in</span>
                </label>
              ) : null}
            </div>

            <div className="form-actions">
              <button
                className="primary-button"
                data-close-after-save="true"
                type="submit"
                disabled={isAdminLoading}
              >
                Save and close
              </button>
              <button
                className="ghost-button"
                data-close-after-save="false"
                type="submit"
                disabled={isAdminLoading}
              >
                Save
              </button>
              <button className="ghost-button" onClick={onCloseEditor} type="button">
                Discard changes
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
