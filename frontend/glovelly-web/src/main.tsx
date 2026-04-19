import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

const THEME_STORAGE_KEY = 'glovelly.theme-preference'

const applyInitialTheme = () => {
  const rawPreference = window.localStorage.getItem(THEME_STORAGE_KEY)
  const preference =
    rawPreference === 'light' || rawPreference === 'dark' || rawPreference === 'system'
      ? rawPreference
      : 'system'

  const systemTheme = window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light'
  const resolvedTheme = preference === 'system' ? systemTheme : preference

  document.documentElement.setAttribute('data-theme', resolvedTheme)
}

applyInitialTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js')
  })
}
