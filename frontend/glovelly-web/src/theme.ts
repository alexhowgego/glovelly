import type { ThemePreference } from './types'

export const themeStorageKey = 'glovelly.theme-preference'

export const themePreferences = [
  'system',
  'light',
  'dark',
  'neon',
  'mahogany',
  'candy',
  'blue-note',
  'parchment',
  'studio-tape',
  'synthwave',
  'sunset-soundcheck',
  'velvet-rope',
] as const

export function isThemePreference(value: string | null): value is ThemePreference {
  return themePreferences.some((preference) => preference === value)
}

export function getStoredThemePreference(): ThemePreference {
  const rawPreference = window.localStorage.getItem(themeStorageKey)
  if (isThemePreference(rawPreference)) {
    return rawPreference
  }

  return 'system'
}
