import { useCallback, useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  handleSessionExpired,
  parseProblemDetails,
  throwIfSessionExpired,
} from '../api'
import { defaultAdminStatus, emptyAdminForm } from '../forms'
import type { AdminUser, AdminUserForm } from '../types'

type UseAdminWorkspaceOptions = {
  onSessionExpired: (message: string) => void
}

function shouldCloseAfterSave(event: FormEvent<HTMLFormElement>) {
  const submitter = (event.nativeEvent as SubmitEvent).submitter as
    | HTMLButtonElement
    | null

  return submitter?.dataset.closeAfterSave !== 'false'
}

export function useAdminWorkspace({ onSessionExpired }: UseAdminWorkspaceOptions) {
  const [adminUsers, setAdminUsers] = useState<AdminUser[]>([])
  const [selectedAdminUserId, setSelectedAdminUserId] = useState<string>('')
  const [adminSearchQuery, setAdminSearchQuery] = useState('')
  const [isAdminEditorOpen, setIsAdminEditorOpen] = useState(false)
  const [adminMode, setAdminMode] = useState<'create' | 'edit'>('create')
  const [adminForm, setAdminForm] = useState<AdminUserForm>(emptyAdminForm)
  const [adminStatus, setAdminStatus] = useState(defaultAdminStatus)
  const [isAdminLoading, setIsAdminLoading] = useState(false)
  const deferredAdminSearchQuery = useDeferredValue(adminSearchQuery)

  const resetAdminWorkspace = useCallback(() => {
    setAdminUsers([])
    setSelectedAdminUserId('')
    setAdminSearchQuery('')
    setIsAdminEditorOpen(false)
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
    setAdminStatus(defaultAdminStatus)
    setIsAdminLoading(false)
  }, [])

  const loadAdminUsers = useCallback(async () => {
    const response = await fetchWithSession(buildApiUrl('/admin/users'))
    throwIfSessionExpired(response)

    if (response.status === 403) {
      return
    }

    if (!response.ok) {
      throw new Error('Unable to load user enrolments.')
    }

    const users = (await response.json()) as AdminUser[]
    setAdminUsers(users)
    setSelectedAdminUserId((current) => current || users[0]?.id || '')
    setAdminStatus(
      users.length > 0
        ? 'Manage access, roles and account status.'
        : 'No users added yet. Add the first one below.'
    )
  }, [])

  const adminUsersById = useMemo(
    () => new Map(adminUsers.map((user) => [user.id, user])),
    [adminUsers]
  )
  const filteredAdminUsers = useMemo(() => {
    const query = deferredAdminSearchQuery.trim().toLowerCase()
    if (!query) {
      return adminUsers
    }

    return adminUsers.filter((user) =>
      [user.email, user.displayName ?? '', user.role, user.googleSubject ?? '']
        .join(' ')
        .toLowerCase()
        .includes(query)
    )
  }, [adminUsers, deferredAdminSearchQuery])

  const selectedAdminUser =
    adminUsersById.get(selectedAdminUserId) ?? filteredAdminUsers[0] ?? null

  useEffect(() => {
    if (selectedAdminUser) {
      setSelectedAdminUserId(selectedAdminUser.id)
    }
  }, [selectedAdminUser])

  const activeUsersCount = adminUsers.filter((user) => user.isActive).length
  const totalAdmins = adminUsers.filter((user) => user.role === 'Admin').length

  const startAdminCreate = () => {
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
    setIsAdminEditorOpen(true)
  }

  const startAdminEdit = () => {
    if (!selectedAdminUser) {
      return
    }

    setAdminMode('edit')
    setAdminForm({
      email: selectedAdminUser.email,
      displayName: selectedAdminUser.displayName ?? '',
      googleSubject: selectedAdminUser.googleSubject ?? '',
      role: selectedAdminUser.role === 'Admin' ? 'Admin' : 'User',
      isActive: selectedAdminUser.isActive,
    })
    setIsAdminEditorOpen(true)
  }

  const closeAdminEditor = () => {
    setIsAdminEditorOpen(false)
    setAdminMode('create')
    setAdminForm(emptyAdminForm())
  }

  const updateAdminField = (
    field: keyof AdminUserForm,
    value: string | boolean
  ) => {
    setAdminForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  const markAdminLoadFailed = useCallback(() => {
    setAdminUsers([])
    setSelectedAdminUserId('')
    setAdminStatus('The admin area could not be loaded right now.')
  }, [])

  const handleAdminSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const closeAfterSave = shouldCloseAfterSave(event)

    const payload: AdminUserForm = {
      email: adminForm.email.trim(),
      displayName: adminForm.displayName.trim(),
      googleSubject: adminForm.googleSubject.trim(),
      role: adminForm.role,
      isActive: adminForm.isActive,
    }

    if (!payload.email) {
      setAdminStatus('Email is required.')
      return
    }

    setIsAdminLoading(true)

    try {
      const isEdit = adminMode === 'edit' && selectedAdminUser
      const endpoint = isEdit
        ? buildApiUrl(`/admin/users/${selectedAdminUser.id}`)
        : buildApiUrl('/admin/users')

      const response = await fetchWithSession(endpoint, {
        method: isEdit ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      })

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing access.'
        )
      ) {
        return
      }

      if (response.status === 403) {
        setAdminStatus('Administrator access is required to manage users.')
        return
      }

      if (!response.ok) {
        const problem = await parseProblemDetails(response)
        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : problem?.detail ?? problem?.title

        throw new Error(validationMessages || 'Unable to save enrolment.')
      }

      const savedUser = (await response.json()) as AdminUser

      setAdminUsers((current) => {
        if (isEdit) {
          return current.map((user) => (user.id === savedUser.id ? savedUser : user))
        }

        return [savedUser, ...current]
      })

      setSelectedAdminUserId(savedUser.id)
      setAdminMode('edit')
      setAdminForm({
        email: savedUser.email,
        displayName: savedUser.displayName ?? '',
        googleSubject: savedUser.googleSubject ?? '',
        role: savedUser.role === 'Admin' ? 'Admin' : 'User',
        isActive: savedUser.isActive,
      })
      setAdminStatus(isEdit ? 'User updated.' : 'User added.')
      setIsAdminEditorOpen(!closeAfterSave)
    } catch (error) {
      setAdminStatus(
        error instanceof Error ? error.message : 'Unable to save right now.'
      )
    } finally {
      setIsAdminLoading(false)
    }
  }

  return {
    activeUsersCount,
    adminForm,
    adminMode,
    adminSearchQuery,
    adminStatus,
    adminUsers,
    closeAdminEditor,
    filteredAdminUsers,
    handleAdminSubmit,
    isAdminEditorOpen,
    isAdminLoading,
    loadAdminUsers,
    markAdminLoadFailed,
    resetAdminWorkspace,
    selectedAdminUser,
    setAdminSearchQuery,
    setSelectedAdminUserId,
    startAdminCreate,
    startAdminEdit,
    totalAdmins,
    updateAdminField,
  }
}
