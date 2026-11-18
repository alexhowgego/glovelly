export const administrativePermissionOptions = [
  { value: 'UserAdministrator', label: 'User Administrator' },
  { value: 'LicenseAdministrator', label: 'Licence Administrator' },
  { value: 'SuperAdministrator', label: 'Super Administrator' },
] as const

export function formatAdministrativePermission(value: string) {
  return administrativePermissionOptions.find(option => option.value === value)?.label
    ?? value.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
}

export function formatAdministrativePermissions(values: readonly string[] | undefined) {
  return values?.length ? values.map(formatAdministrativePermission).join(', ') : 'User'
}

export function describePermissionChange(previous: string, current: string) {
  try {
    const before: unknown = JSON.parse(previous)
    const after: unknown = JSON.parse(current)
    if (!Array.isArray(before) || !Array.isArray(after) ||
        !before.every(value => typeof value === 'string') ||
        !after.every(value => typeof value === 'string')) return 'Change details unavailable'
    const added = after.filter(value => !before.includes(value)).map(formatAdministrativePermission)
    const removed = before.filter(value => !after.includes(value)).map(formatAdministrativePermission)
    return [
      added.length ? `${added.join(', ')} added` : '',
      removed.length ? `${removed.join(', ')} removed` : '',
    ].filter(Boolean).join('; ') || 'No permission change'
  } catch {
    return 'Change details unavailable'
  }
}
