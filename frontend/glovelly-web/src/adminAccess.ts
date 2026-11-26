import type { AdminUser } from './types'

export function canManageUsers(permissions: readonly string[] | undefined) {
  return permissions?.includes('ManageUsers') ?? false
}

export function adminAccessRestriction(user: AdminUser, currentUserId: string | undefined) {
  return user.id === currentUserId
    ? 'You cannot deactivate your own administrator account.'
    : undefined
}
