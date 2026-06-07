import { useCallback, useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
  throwIfSessionExpired,
} from '../api'
import { defaultAdminStatus, emptyAdminForm } from '../forms'
import type { AdminSort, AdminUser, AdminUserForm } from '../types'

type UseAdminWorkspaceOptions = {
  onSessionExpired: (message: string) => void
}

function shouldCloseAfterSave(event: FormEvent<HTMLFormElement>) {
  const submitter = (event.nativeEvent as SubmitEvent).submitter as
    | HTMLButtonElement
    | null

  return submitter?.dataset.closeAfterSave !== 'false'
}

function toEditableAdminForm(user: AdminUser): AdminUserForm {
  return {
    email: user.email,
    displayName: user.displayName ?? '',
    googleSubject: user.googleSubject ?? '',
    role: user.role === 'Admin' ? 'Admin' : 'User',
    isActive: user.isActive,
  }
}

export function useAdminWorkspace({ onSessionExpired }: UseAdminWorkspaceOptions) {
  const [adminUsers, setAdminUsers] = useState<AdminUser[]>([])
  const [selectedAdminUserId, setSelectedAdminUserId] = useState<string>('')
  const [adminSearchQuery, setAdminSearchQuery] = useState('')
  const [adminSort, setAdminSort] = useState<AdminSort>({
    key: 'displayName',
    direction: 'asc',
  })
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
    setAdminSort({ key: 'displayName', direction: 'asc' })
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
    const sortDirection = adminSort.direction === 'asc' ? 1 : -1
    const compareText = (left: string, right: string) => left.localeCompare(right)
    const compareBoolean = (left: boolean, right: boolean) => Number(left) - Number(right)
    const getDisplayName = (user: AdminUser) => user.displayName || user.email
    const compareByKey = (left: AdminUser, right: AdminUser) => {
      switch (adminSort.key) {
        case 'access':
          return compareBoolean(left.isActive, right.isActive)
        case 'email':
          return compareText(left.email, right.email)
        case 'enrolment':
          return compareBoolean(left.isEnrolled, right.isEnrolled)
        case 'lastLogin':
          return compareText(left.lastLoginUtc ?? '', right.lastLoginUtc ?? '')
        case 'role':
          return compareText(left.role, right.role)
        case 'displayName':
        default:
          return compareText(getDisplayName(left), getDisplayName(right))
      }
    }
    const sortedAdminUsers = [...adminUsers].sort((left, right) => {
      const primaryComparison = compareByKey(left, right)
      if (primaryComparison !== 0) {
        return primaryComparison * sortDirection
      }

      const nameComparison = getDisplayName(left).localeCompare(getDisplayName(right))
      if (nameComparison !== 0) {
        return nameComparison
      }

      return left.id.localeCompare(right.id)
    })

    if (!query) {
      return sortedAdminUsers
    }

    return sortedAdminUsers.filter((user) =>
      [user.email, user.displayName ?? '', user.role, user.googleSubject ?? '']
        .join(' ')
        .toLowerCase()
        .includes(query)
    )
  }, [adminSort, adminUsers, deferredAdminSearchQuery])

  const selectedAdminUser =
    adminUsersById.get(selectedAdminUserId) ?? filteredAdminUsers[0] ?? null

  const hasUnsavedAdminEditorChanges = () => {
    if (!isAdminEditorOpen) {
      return false
    }

    const baseline =
      adminMode === 'edit' && selectedAdminUser
        ? toEditableAdminForm(selectedAdminUser)
        : emptyAdminForm()

    return JSON.stringify(adminForm) !== JSON.stringify(baseline)
  }

  useEffect(() => {
    if (selectedAdminUser) {
      setSelectedAdminUserId(selectedAdminUser.id)
    }
  }, [selectedAdminUser])

  const activeUsersCount = adminUsers.filter((user) => user.isActive).length
  const totalAdmins = adminUsers.filter((user) => user.role === 'Admin').length

  const startAdminCreate = () => {
    if (
      hasUnsavedAdminEditorChanges() &&
      !window.confirm('Discard unsaved access changes and add a new user?')
    ) {
      return
    }

    setAdminMode('create')
    setAdminForm(emptyAdminForm())
    setIsAdminEditorOpen(true)
  }

  const startAdminEdit = () => {
    if (!selectedAdminUser) {
      return
    }

    setAdminMode('edit')
    setAdminForm(toEditableAdminForm(selectedAdminUser))
    setIsAdminEditorOpen(true)
  }

  const selectAdminUser = (userId: string) => {
    if (userId === selectedAdminUser?.id) {
      return
    }

    const nextUser = adminUsersById.get(userId)
    if (!nextUser) {
      return
    }

    if (isAdminEditorOpen) {
      if (
        hasUnsavedAdminEditorChanges() &&
        !window.confirm('Discard unsaved access changes and edit the selected user?')
      ) {
        return
      }

      setAdminMode('edit')
      setAdminForm(toEditableAdminForm(nextUser))
    }

    setSelectedAdminUserId(userId)
  }

  const closeAdminEditor = () => {
    if (
      hasUnsavedAdminEditorChanges() &&
      !window.confirm('Discard unsaved access changes and close the editor?')
    ) {
      return
    }

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

      const response = await fetchWithSession(
        endpoint,
        jsonRequestInit(isEdit ? 'PUT' : 'POST', payload)
      )

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
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to save enrolment.')
        )
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

  const deleteAdminUser = async () => {
    if (!selectedAdminUser) {
      return
    }

    if (selectedAdminUser.isActive) {
      setAdminStatus('Only inactive users can be deleted.')
      return
    }

    if (
      !window.confirm(
        `Delete ${selectedAdminUser.displayName || selectedAdminUser.email}? This cannot be undone.`
      )
    ) {
      return
    }

    setIsAdminLoading(true)

    try {
      const response = await fetchWithSession(
        buildApiUrl(`/admin/users/${selectedAdminUser.id}`),
        {
          method: 'DELETE',
        }
      )

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
        throw new Error(
          await getResponseErrorMessage(response, 'Unable to delete user.')
        )
      }

      const nextUsers = adminUsers.filter((user) => user.id !== selectedAdminUser.id)
      setAdminUsers(nextUsers)
      setSelectedAdminUserId(nextUsers[0]?.id ?? '')
      setIsAdminEditorOpen(false)
      setAdminMode('create')
      setAdminForm(emptyAdminForm())
      setAdminStatus('User deleted.')
    } catch (error) {
      setAdminStatus(error instanceof Error ? error.message : 'Unable to delete user.')
    } finally {
      setIsAdminLoading(false)
    }
  }

  return {
    activeUsersCount,
    adminForm,
    adminMode,
    adminSearchQuery,
    adminSort,
    adminStatus,
    adminUsers,
    closeAdminEditor,
    deleteAdminUser,
    filteredAdminUsers,
    handleAdminSubmit,
    isAdminEditorOpen,
    isAdminLoading,
    loadAdminUsers,
    markAdminLoadFailed,
    resetAdminWorkspace,
    selectedAdminUser,
    selectAdminUser,
    setAdminSearchQuery,
    setAdminSort,
    setSelectedAdminUserId,
    startAdminCreate,
    startAdminEdit,
    totalAdmins,
    updateAdminField,
  }
}
