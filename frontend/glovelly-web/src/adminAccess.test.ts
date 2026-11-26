import { describe, expect, it } from 'vitest'
import { canManageUsers } from './adminAccess'

describe('administrative navigation capability', () => {
  it('shows user administration only to users with ManageUsers', () => {
    expect(canManageUsers(undefined)).toBe(false)
    expect(canManageUsers([])).toBe(false)
    expect(canManageUsers(['ManageLicences'])).toBe(false)
    expect(canManageUsers(['ManageUsers'])).toBe(true)
    expect(canManageUsers(['ManageUsers', 'ManagePermissions'])).toBe(true)
  })
})
