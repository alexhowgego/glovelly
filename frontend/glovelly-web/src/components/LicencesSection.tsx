import { useId } from 'react'
import type { SessionExpiredHandler } from '../api'
import { formatDateTime } from '../formatters'
import { useLicencesWorkspace } from '../hooks/useLicencesWorkspace'
import { RecordActionDock } from './RecordActionDock'
import { RecordEditorActions } from './RecordEditorActions'
import { RecordEditorFrame } from './RecordEditorFrame'
import { RecordWorkspace } from './RecordWorkspace'

function assignmentLabel(json: string | null) {
  if (!json) return 'Free (default)'
  try {
    const value = JSON.parse(json) as { planCode: string; state: string; endsAtUtc: string | null }
    const planNames: Record<string, string> = { free: 'Free', solo: 'Solo', pro: 'Pro' }
    const plan = planNames[value.planCode] ?? value.planCode
    if (value.state === 'Cancelled') return `${plan} (revoked)`
    if (value.planCode === 'free') return plan
    return `${plan} (${value.endsAtUtc ? `until ${formatDateTime(value.endsAtUtc)}` : 'permanent'})`
  } catch { return 'Historical assignment' }
}

export function LicencesSection({ onSessionExpired }: { onSessionExpired: SessionExpiredHandler }) {
  const planId = useId()
  const workspace = useLicencesWorkspace(onSessionExpired)
  const { selected, directory, isEditorOpen } = workspace
  const access = selected?.entitlements
  const usage = access?.invoiceIssuance
  const assignmentStatus = access ? {
    Default: 'Free by default', Active: 'Active', Expired: 'Expired', Cancelled: 'Revoked', NotStarted: 'Not started',
  }[access.assignmentStatus] : ''

  return <RecordWorkspace className="admin-workspace" sectionClassName="admin-zone licences-zone" label="Licence management">
      <div className="panel">
        <div className="panel-heading">
          <div>
            <p className="section-label">Product access directory</p>
            <h2>Licences</h2>
          </div>
          <span className="status-pill">{directory.total} {directory.total === 1 ? 'user' : 'users'}</span>
        </div>

        <div className="compact-list-controls" aria-label="Licence user list controls">
          <div className="compact-list-main-controls licence-list-main-controls">
            <label className="search-field compact-search-field">
              <span>Search</span>
              <input type="search" placeholder="Name or email..." value={workspace.search} disabled={workspace.isSaving}
                onChange={event => workspace.setSearch(event.target.value)} />
            </label>
            <button className="ghost-button" type="button" disabled={workspace.busy}
              onClick={() => void workspace.reload()}>Reload</button>
          </div>
        </div>
        {!isEditorOpen && workspace.error && <p className="settings-note" role="alert">{workspace.error}</p>}

        <div className="compact-record-list admin-record-list" aria-label="People and licences" aria-busy={workspace.busy}>
          <div className="compact-record-header admin-record-row">
            <span>User</span><span>Email</span><span>Plan</span><span>Issued</span><span>Access</span><span>Grant ends</span>
          </div>
          {directory.users.map(user => <button key={user.id} type="button"
            className={`compact-record-row admin-record-row ${selected?.id === user.id ? 'selected' : ''}`}
            aria-label={user.displayName || user.email} aria-pressed={selected?.id === user.id}
            disabled={workspace.isSaving} onClick={() => void workspace.select(user)}>
            <div className="compact-primary-cell"><strong>{user.displayName || user.email}</strong><span>{user.email}</span></div>
            <span>{user.email}</span>
            <span className="compact-status-cell">{user.entitlements.effectivePlan.displayName}</span>
            <span>{user.entitlements.invoiceIssuance.used} / {user.entitlements.invoiceIssuance.limit ?? 'Unlimited'}</span>
            <span>{user.isActive ? user.isEnrolled ? 'Active · Bound' : 'Active · Invited' : 'Inactive'}</span>
            <span>{user.entitlements.assignmentStatus === 'Cancelled' ? 'Revoked'
              : user.entitlements.endsAtUtc ? formatDateTime(user.entitlements.endsAtUtc)
              : user.entitlements.assignedPlan ? 'Permanent' : '—'}</span>
          </button>)}
          {directory.users.length === 0 && <div className="empty-state">
            <strong>{workspace.busy ? 'Loading licences…' : 'No users match that search.'}</strong>
            {!workspace.busy && <p>Try another name or email.</p>}
          </div>}
        </div>
        {directory.total > directory.pageSize && <div className="actions licence-pagination" aria-label="Licence directory pages">
          <button className="ghost-button" type="button" disabled={workspace.busy || workspace.page <= 1}
            onClick={() => workspace.setPage(workspace.page - 1)}>Previous</button>
          <span className="auth-note">Page {workspace.page} of {Math.ceil(directory.total / directory.pageSize)}</span>
          <button className="ghost-button" type="button" disabled={workspace.busy || workspace.page * directory.pageSize >= directory.total}
            onClick={() => workspace.setPage(workspace.page + 1)}>Next</button>
        </div>}
      </div>

      <div className="panel record-overview" id="licence-overview">
        <div className="panel-heading">
          <div>
            <p className="section-label">Licence overview</p>
            <h2 tabIndex={-1}>{selected?.displayName || selected?.email || 'No user selected'}</h2>
          </div>
          {access && <span className="status-pill">{access.effectivePlan.displayName}</span>}
        </div>
        {selected && access && usage ? <>
          <div className="overview-body" key={selected.id}>
            <dl className="overview-facts">
              <div className="full-width"><dt>Email</dt><dd>{selected.email}</dd></div>
              <div><dt>Effective plan</dt><dd>{access.effectivePlan.displayName}</dd></div>
              <div><dt>Assigned plan</dt><dd>{access.assignedPlan?.displayName ?? 'Free (default)'}</dd></div>
              <div><dt>Grant status</dt><dd>{assignmentStatus}</dd></div>
              <div><dt>Grant expiry</dt><dd>{access.assignmentStatus === 'Cancelled' ? 'No active grant'
                : access.endsAtUtc ? formatDateTime(access.endsAtUtc)
                : access.assignedPlan ? 'Permanent' : 'No manual grant'}</dd></div>
              <div><dt>First-issued this month</dt><dd>{usage.used} / {usage.limit ?? 'Unlimited'}</dd></div>
              <div><dt>Remaining allowance</dt><dd>{usage.remaining ?? 'Unlimited'}</dd></div>
              <div><dt>Next reset (UK time)</dt><dd>{new Date(usage.resetsAtUtc).toLocaleString('en-GB', {
                timeZone: 'Europe/London', dateStyle: 'medium', timeStyle: 'short',
              })}</dd></div>
              <div><dt>Account access</dt><dd>{selected.isActive ? selected.isEnrolled ? 'Active · Ready to sign in'
                : 'Active · Waiting for first sign-in' : 'Inactive'}</dd></div>
            </dl>
            <details className="permission-history">
              <summary>Recent licence changes</summary>
              {workspace.isAuditLoading ? <p>Loading licence history…</p>
                : workspace.audit.length ? <ul>{workspace.audit.slice(0, 20).map(entry => <li key={entry.id}>
                  <strong>{assignmentLabel(entry.previousAssignment)} → {assignmentLabel(entry.newAssignment)}</strong>
                  <span>{entry.actorIdentity} · {formatDateTime(entry.changedAtUtc)}</span>
                  {entry.reason && <span>{entry.reason}</span>}
                </li>)}</ul> : <p>No recorded licence changes.</p>}
            </details>
          </div>
          <RecordActionDock title="Product access">
            <div className="actions">
              <button className={`ghost-button editor-toggle ${isEditorOpen ? 'active' : ''}`} type="button"
                aria-expanded={isEditorOpen} aria-controls="licence-editor"
                disabled={workspace.busy || (!isEditorOpen && !!workspace.error)}
                onClick={isEditorOpen ? workspace.closeEditor : workspace.startEditing}>Edit licence</button>
            </div>
            <p className="auth-note">Plan changes never reset usage or change administrative permissions.</p>
          </RecordActionDock>
        </> : <div className="empty-state roomy">
          <strong>Select a user to review their licence.</strong>
          <p>Review their plan, allowance and grant history before making changes.</p>
        </div>}
      </div>

      <RecordEditorFrame id="licence-editor" isOpen={isEditorOpen} label="Update licence" onSubmit={event => {
        event.preventDefault()
        const submitter = (event.nativeEvent as SubmitEvent).submitter
        void workspace.save(!(submitter instanceof HTMLElement && submitter.dataset.closeAfterSave === 'false'))
      }}>
        <div className="panel-heading">
          <div><p className="section-label">Management pane</p><h2>Update licence</h2></div>
          <span className="status-pill">{workspace.isSaving ? 'Saving…' : 'Product access'}</span>
        </div>
        {selected && <p className="auth-note">{selected.email}</p>}
        {workspace.error && <p className="settings-note" role="alert">{workspace.error}</p>}
        {workspace.editorMessage && <p className="settings-note" role="status">{workspace.editorMessage}</p>}
        <div className="form-grid">
          <label htmlFor={planId}><span id={`${planId}-label`}>Plan</span>
            <select id={planId} aria-labelledby={`${planId}-label`} value={workspace.plan}
              onChange={event => workspace.setPlan(event.target.value)} disabled={workspace.busy}>
              <option value="free">Free</option><option value="solo">Solo</option><option value="pro">Pro</option>
            </select>
          </label>
          <label><span>Expiry (UTC, optional)</span>
            <input type="text" placeholder="2026-12-31T23:00:00Z" value={workspace.expires}
              disabled={workspace.busy || workspace.plan === 'free' || workspace.cancelled}
              onChange={event => workspace.setExpires(event.target.value)} />
          </label>
          <label className="full-width"><span>Reason (optional)</span>
            <input maxLength={1000} value={workspace.reason} disabled={workspace.busy} placeholder="Founder, tester, goodwill…"
              onChange={event => workspace.setReason(event.target.value)} />
          </label>
          <label className="checkbox-field full-width">
            <input type="checkbox" checked={workspace.cancelled} disabled={workspace.busy}
              onChange={event => workspace.setCancelled(event.target.checked)} />
            <span>Revoke paid access and fall back to Free</span>
          </label>
        </div>
        <RecordEditorActions isSaving={workspace.busy || !selected} onDiscard={workspace.closeEditor} />
        <p className="auth-note">Leave expiry blank for a permanent grant. Expiry or revocation preserves all records and current-month usage.</p>
      </RecordEditorFrame>
  </RecordWorkspace>
}
