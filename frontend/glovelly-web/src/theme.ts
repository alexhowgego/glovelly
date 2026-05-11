import type { ThemePreference } from './types'

export const themeStorageKey = 'glovelly.theme-preference'

export function getStoredThemePreference(): ThemePreference {
  const rawPreference = window.localStorage.getItem(themeStorageKey)
  if (
    rawPreference === 'system' ||
    rawPreference === 'light' ||
    rawPreference === 'dark'
  ) {
    return rawPreference
  }

  return 'system'
}
