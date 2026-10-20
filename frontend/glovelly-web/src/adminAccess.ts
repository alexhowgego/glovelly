import type { AdminUser } from './types'

export function adminAccessRestriction(user: AdminUser, currentUserId: string | undefined) {
  return user.id === currentUserId
    ? 'You cannot deactivate your own administrator account or remove your own administrator role.'
    : undefined
}
