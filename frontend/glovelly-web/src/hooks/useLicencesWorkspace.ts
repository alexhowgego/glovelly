import { useCallback, useEffect, useRef, useState } from 'react'
import {
  buildApiUrl, fetchWithSession, getResponseErrorMessage, handleSessionExpired,
  jsonRequestInit,
} from '../api'
import type { SessionExpiredHandler } from '../api'
import type { LicenceAudit, LicenceUser } from '../types'
import { notifications } from '../notifications'

type Directory = { users: LicenceUser[]; total: number; page: number; pageSize: number }

export function useLicencesWorkspace(onSessionExpired: SessionExpiredHandler) {
  const [directory, setDirectory] = useState<Directory>({ users: [], total: 0, page: 1, pageSize: 25 })
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<LicenceUser | null>(null)
  const [audit, setAudit] = useState<LicenceAudit[]>([])
  const [plan, setPlan] = useState('free')
  const [expires, setExpires] = useState('')
  const [reason, setReason] = useState('')
  const [cancelled, setCancelled] = useState(false)
  const [busy, setBusy] = useState(false)
  const [auditLoading, setAuditLoading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [isEditorOpen, setIsEditorOpen] = useState(false)
  const [editorMessage, setEditorMessage] = useState('')
  const [error, setError] = useState('')
  const generation = useRef(0)
  const selectionGeneration = useRef(0)
  const originalForm = useRef('')
  const savedReason = useRef('')
  const saveInFlight = useRef(false)

  const load = useCallback(async () => {
    const current = ++generation.current
    setBusy(true)
    setError('')
    try {
      const query = new URLSearchParams({ search, page: String(page), pageSize: '25' })
      const response = await fetchWithSession(buildApiUrl(`/admin/licences/users?${query}`))
      if (current !== generation.current) return
      if (handleSessionExpired(response, onSessionExpired, 'Sign in again to manage licences.')) return
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to load licences.'))
      const next = await response.json() as Directory
      if (current !== generation.current) return
      setDirectory(next)
      return next
    } catch (failure) {
      if (current === generation.current) setError(failure instanceof Error ? failure.message : 'Unable to load licences.')
    } finally {
      if (current === generation.current) setBusy(false)
    }
  }, [search, page, onSessionExpired])

  const invalidateRequests = useCallback(() => {
    generation.current++
    selectionGeneration.current++
  }, [])
  useEffect(() => {
    void load()
    return invalidateRequests
  }, [load, invalidateRequests])

  const resetForm = (user: LicenceUser, assignmentReason: string) => {
    const nextPlan = user.entitlements.assignedPlan?.code ?? 'free'
    const nextCancelled = user.entitlements.assignmentStatus === 'Cancelled'
    const nextExpires = user.entitlements.endsAtUtc?.replace(/\+00:00$/, 'Z') ?? ''
    setPlan(nextPlan)
    setCancelled(nextCancelled)
    setExpires(nextExpires)
    setReason(assignmentReason)
    savedReason.current = assignmentReason
    originalForm.current = JSON.stringify([nextPlan, nextExpires, assignmentReason, nextCancelled])
  }

  const canDiscard = () => !isEditorOpen ||
    originalForm.current === JSON.stringify([plan, expires, reason, cancelled]) ||
    window.confirm('Discard unsaved licence changes?')

  const loadSelection = async (user: LicenceUser, openEditor: boolean, fallbackReason = '') => {
    const current = ++selectionGeneration.current
    setAuditLoading(true)
    setSelected(user)
    resetForm(user, fallbackReason)
    setIsEditorOpen(openEditor)
    setEditorMessage('')
    setAudit([])
    setError('')
    try {
      const response = await fetchWithSession(buildApiUrl(`/admin/licences/audit?targetUserId=${user.id}`))
      if (current !== selectionGeneration.current) return
      if (handleSessionExpired(response, onSessionExpired, 'Sign in again to view licence history.')) return
      if (!response.ok) throw new Error(await getResponseErrorMessage(response, 'Unable to load licence history.'))
      const history = await response.json() as LicenceAudit[]
      if (current === selectionGeneration.current) {
        setAudit(history)
        resetForm(user, history.length ? history[0].reason ?? '' : fallbackReason)
      }
    } catch (failure) {
      if (current === selectionGeneration.current) setError(failure instanceof Error ? failure.message : 'Unable to load licence history.')
    } finally {
      if (current === selectionGeneration.current) setAuditLoading(false)
    }
  }

  const select = async (user: LicenceUser) => {
    if (saveInFlight.current || user.id === selected?.id || !canDiscard()) return
    await loadSelection(user, isEditorOpen)
  }

  const startEditing = () => {
    if (!selected || busy || auditLoading || error || saveInFlight.current) return
    resetForm(selected, savedReason.current)
    setEditorMessage('')
    setIsEditorOpen(true)
  }

  const closeEditor = () => {
    if (saveInFlight.current || !canDiscard()) return
    setIsEditorOpen(false)
    setEditorMessage('')
    setError('')
  }

  const clearSelection = () => {
    selectionGeneration.current++
    setSelected(null)
    setIsEditorOpen(false)
    setAuditLoading(false)
    setEditorMessage('')
    setError('')
  }

  const reload = async () => {
    if (saveInFlight.current || !canDiscard()) return
    clearSelection()
    await load()
  }

  const save = async (closeAfterSave = true) => {
    if (!selected || busy || auditLoading || saveInFlight.current) return
    saveInFlight.current = true
    setIsSaving(true)
    setError('')
    setEditorMessage('')
    try {
      if (plan !== 'free' && !cancelled && expires &&
        (!expires.endsWith('Z') || Number.isNaN(new Date(expires).getTime()))) {
        throw new Error('Enter a UTC date/time ending in Z, or leave expiry blank for a permanent grant.')
      }
      const response = await fetchWithSession(buildApiUrl(`/admin/licences/users/${selected.id}`), jsonRequestInit('PUT', {
        planCode: plan, endsAtUtc: plan === 'free' || cancelled || !expires ? null : new Date(expires).toISOString(),
        cancelled, version: selected.version, reason: reason.trim() || null,
      }))
      if (handleSessionExpired(response, onSessionExpired, 'Sign in again to change licences.')) return
      if (!response.ok) throw new Error(response.status === 409
        ? 'This assignment changed. Reload the directory and select the user again before saving.'
        : await getResponseErrorMessage(response, 'Unable to save the licence.'))
      window.dispatchEvent(new Event('glovelly:entitlements-changed'))
      const refreshed = await load()
      const refreshedUser = refreshed?.users.find(user => user.id === selected.id)
      if (refreshedUser) {
        await loadSelection(refreshedUser, !closeAfterSave, reason.trim())
        if (!closeAfterSave) setEditorMessage('Licence saved. Usage and administrative permissions are unchanged.')
      } else {
        // A successful write must not leave a stale assignment version editable
        // if its follow-up read failed or the user is no longer in the directory.
        setSelected(null)
        setIsEditorOpen(false)
      }
      if (closeAfterSave || !refreshedUser) {
        notifications.success('Licence saved. Usage and administrative permissions are unchanged.')
      }
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to save the licence.')
    } finally {
      saveInFlight.current = false
      setIsSaving(false)
    }
  }

  return {
    directory, search, setSearch: (value: string) => {
      if (saveInFlight.current || !canDiscard()) return
      clearSelection(); setSearch(value); setPage(1)
    },
    page, setPage: (value: number) => {
      if (saveInFlight.current || !canDiscard()) return
      clearSelection(); setPage(value)
    },
    selected, select, audit, plan, setPlan: (value: string) => { setPlan(value); setEditorMessage('') },
    expires, setExpires: (value: string) => { setExpires(value); setEditorMessage('') },
    reason, setReason: (value: string) => { setReason(value); setEditorMessage('') },
    cancelled, setCancelled: (value: boolean) => { setCancelled(value); setEditorMessage('') },
    busy: busy || auditLoading || isSaving,
    isSaving, isEditorOpen, editorMessage, startEditing, closeEditor, error, save, reload,
    isAuditLoading: auditLoading,
  }
}
