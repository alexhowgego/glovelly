import { describe, expect, it } from 'vitest'
import { describePermissionChange, formatAdministrativePermissions } from './administrativePermissions'

describe('administrative permission labels', () => {
  it('formats known and future permission sets without exposing enum identifiers', () => {
    expect(formatAdministrativePermissions(['UserAdministrator', 'LicenseAdministrator', 'FutureAdministrator']))
      .toBe('User Administrator, Licence Administrator, Future Administrator')
    expect(formatAdministrativePermissions([])).toBe('User')
  })

  it('summarizes additions and removals and handles malformed history', () => {
    expect(describePermissionChange('["UserAdministrator"]', '["LicenseAdministrator"]'))
      .toBe('Licence Administrator added; User Administrator removed')
    expect(describePermissionChange('invalid', '[]')).toBe('Change details unavailable')
    expect(describePermissionChange('[]', '[1]')).toBe('Change details unavailable')
  })
})
