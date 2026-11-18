import { useEffect, useState } from 'react'
import { buildApiUrl, fetchWithSession, handleSessionExpired, jsonRequestInit } from '../api'
import type { SessionExpiredHandler } from '../api'
import { administrativePermissionOptions, describePermissionChange } from '../administrativePermissions'
import { formatDateTime } from '../formatters'

type PermissionUser = { id: string, permissionSets: string[] }
type AuditEntry = {
  id: string, targetUserId: string, targetIdentity: string, actorIdentity: string,
  changedAtUtc: string, previousSets: string, newSets: string, reason: string | null,
}

export function PermissionManagement({ userId, onSessionExpired, onPermissionsChanged }: {
  userId: string
  onSessionExpired: SessionExpiredHandler
  onPermissionsChanged: () => Promise<void>
}) {
  const [selected, setSelected] = useState<string[]>([])
  const [saved, setSaved] = useState<string[]>([])
  const [audit, setAudit] = useState<AuditEntry[]>([])
  const [reason, setReason] = useState('')
  const [status, setStatus] = useState('Loading permissions…')
  const [busy, setBusy] = useState(false)
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    let cancelled = false
    async function load() {
      try {
        const [usersResponse, auditResponse] = await Promise.all([
          fetchWithSession(buildApiUrl('/admin/permissions/users')),
          fetchWithSession(buildApiUrl(`/admin/permissions/audit?targetUserId=${encodeURIComponent(userId)}`)),
        ])
        if (handleSessionExpired(usersResponse, onSessionExpired, 'Sign in again to manage permissions.') ||
            handleSessionExpired(auditResponse, onSessionExpired, 'Sign in again to manage permissions.')) return
        if (!usersResponse.ok || !auditResponse.ok) throw new Error('Unable to load permissions or audit history.')
        const users = await usersResponse.json() as PermissionUser[]
        const entries = await auditResponse.json() as AuditEntry[]
        const user = users.find(value => value.id === userId)
        if (!user) throw new Error('The selected user is no longer available.')
        if (!cancelled) {
          setSelected(user.permissionSets)
          setSaved(user.permissionSets)
          setAudit(entries.filter(entry => entry.targetUserId === userId))
          setLoaded(true)
          setStatus('')
        }
      } catch (error) {
        if (!cancelled) setStatus(error instanceof Error ? error.message : 'Unable to load permissions.')
      }
    }
    setStatus('Loading permissions…')
    setLoaded(false)
    void load()
    return () => { cancelled = true }
  }, [userId, onSessionExpired])

  const changed = selected.length !== saved.length || selected.some(value => !saved.includes(value))

  async function save() {
    setBusy(true)
    setStatus('')
    try {
      const response = await fetchWithSession(buildApiUrl(`/admin/permissions/users/${userId}`),
        jsonRequestInit('PUT', { permissionSets: selected, reason: reason.trim() || null }))
      if (handleSessionExpired(response, onSessionExpired, 'Sign in again to manage permissions.')) return
      if (response.status === 409) throw new Error('At least one active, enrolled Super Administrator must remain.')
      if (response.status === 403) throw new Error('You no longer have permission to manage administrative access.')
      if (!response.ok) throw new Error('Unable to update permissions.')
      setSaved([...selected])
      setReason('')
      setStatus('Permissions saved.')
      let refreshFailed = false
      try {
        const auditResponse = await fetchWithSession(buildApiUrl(`/admin/permissions/audit?targetUserId=${encodeURIComponent(userId)}`))
        if (!auditResponse.ok) throw new Error('Unable to refresh history.')
        const entries = await auditResponse.json() as AuditEntry[]
        setAudit(entries.filter(entry => entry.targetUserId === userId))
      } catch {
        refreshFailed = true
      }
      try {
        await onPermissionsChanged()
      } catch {
        refreshFailed = true
      }
      if (refreshFailed) {
        setStatus('Permissions saved, but the display could not be refreshed.')
      }
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Unable to update permissions.')
    } finally {
      setBusy(false)
    }
  }

  return <details className="permission-management">
    <summary>Administrative permissions</summary>
    <div className="permission-management-body">
      <div className="permission-options">
        {administrativePermissionOptions.map(option => <label className="permission-option" key={option.value}>
          <input type="checkbox" checked={selected.includes(option.value)} disabled={busy || !loaded}
            onChange={event => setSelected(current => event.target.checked
              ? [...current, option.value] : current.filter(value => value !== option.value))} />
          <span>{option.label}</span>
        </label>)}
      </div>
      <label className="permission-reason">Reason (optional)
        <input value={reason} maxLength={1000} onChange={event => setReason(event.target.value)} />
      </label>
      <div className="permission-actions">
        <button type="button" className="ghost-button" disabled={busy || !loaded || (!changed && !reason)}
          onClick={() => { setSelected([...saved]); setReason(''); setStatus('') }}>Reset</button>
        <button type="button" className="primary-button" disabled={busy || !loaded || !changed}
          onClick={() => void save()}>Save permissions</button>
      </div>
      {status && <p className="permission-status" role="status">{status}</p>}
      <details className="permission-history">
        <summary>Permission history</summary>
        {!loaded ? null : audit.length === 0 ? <p>No permission changes recorded.</p> : <ul>
          {audit.map(entry => <li key={entry.id}>
            <strong>{describePermissionChange(entry.previousSets, entry.newSets)}</strong>
            <span>{entry.actorIdentity} · {formatDateTime(entry.changedAtUtc)}</span>
            {entry.reason && <span>{entry.reason}</span>}
          </li>)}
        </ul>}
      </details>
    </div>
  </details>
}
