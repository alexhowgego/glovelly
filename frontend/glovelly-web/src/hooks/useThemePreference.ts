import { useEffect, useState } from 'react'
import { getStoredThemePreference, themeStorageKey } from '../theme'
import type { ThemePreference } from '../types'

export function useThemePreference() {
  const [themePreference, setThemePreference] =
    useState<ThemePreference>(getStoredThemePreference)

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')

    const applyTheme = () => {
      const nextResolvedTheme =
        themePreference === 'system'
          ? mediaQuery.matches
            ? 'dark'
            : 'light'
          : themePreference

      document.documentElement.setAttribute('data-theme', nextResolvedTheme)
    }

    applyTheme()
    window.localStorage.setItem(themeStorageKey, themePreference)

    if (themePreference !== 'system') {
      return
    }

    const handleSystemThemeChange = () => {
      applyTheme()
    }

    mediaQuery.addEventListener('change', handleSystemThemeChange)
    return () => {
      mediaQuery.removeEventListener('change', handleSystemThemeChange)
    }
  }, [themePreference])

  return {
    setThemePreference,
    themePreference,
  }
}
